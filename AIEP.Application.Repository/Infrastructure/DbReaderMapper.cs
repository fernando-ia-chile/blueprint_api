using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;

namespace AIEP.Application.Repository.Infrastructure
{
    /// <summary>
    /// Mapeo por reflexion de <see cref="IDataReader"/> a entidades, compatible con
    /// <see cref="ColumnAttribute"/> (mismo criterio que el repositorio de referencia
    /// <c>ModoRepository.txt</c>), pero:
    /// <list type="bullet">
    ///   <item>el escaneo de atributos y la compilacion de setters se hacen UNA vez por
    ///   tipo y se cachean (<see cref="Cache"/>), no en cada llamada ni por result set;</item>
    ///   <item>cada fila se lee completa con <see cref="IDataReader.GetValues(object[])"/>;</item>
    ///   <item>existe variante asincrona real sobre <see cref="DbDataReader"/>.</item>
    /// </list>
    /// Se conserva la semantica de conversion original: <c>"Y"/"N"</c> y <c>1/0</c> a
    /// <see cref="bool"/>, valores nulos dejan el <c>default</c> de la propiedad.
    /// </summary>
    public static class DbReaderMapper
    {
        private static readonly ConcurrentDictionary<Type, IReadOnlyDictionary<string, ColumnSetter>> Cache = new();

        /// <summary>Mapea todos los registros del result set actual (version sincrona).</summary>
        public static List<T> MapReaderToList<T>(IDataReader reader) where T : new()
        {
            ArgumentNullException.ThrowIfNull(reader);

            var setters = GetSetters(typeof(T));
            var plan = BuildPlan(reader, setters);
            var buffer = new object[reader.FieldCount];
            var list = new List<T>();

            while (reader.Read())
            {
                reader.GetValues(buffer);
                list.Add(MapRow<T>(plan, buffer));
            }

            return list;
        }

        /// <summary>Mapea todos los registros del result set actual (version asincrona).</summary>
        public static async Task<List<T>> MapReaderToListAsync<T>(DbDataReader reader, CancellationToken cancellationToken = default)
            where T : new()
        {
            ArgumentNullException.ThrowIfNull(reader);

            var setters = GetSetters(typeof(T));
            var plan = BuildPlan(reader, setters);
            var buffer = new object[reader.FieldCount];
            var list = new List<T>();

            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                reader.GetValues(buffer);
                list.Add(MapRow<T>(plan, buffer));
            }

            return list;
        }

        private static T MapRow<T>(IReadOnlyList<(int Ordinal, ColumnSetter Setter)> plan, object[] buffer) where T : new()
        {
            var item = new T();
            for (var i = 0; i < plan.Count; i++)
            {
                var (ordinal, setter) = plan[i];
                var raw = buffer[ordinal];
                if (raw is null || raw is DBNull)
                {
                    continue; // deja el default de la propiedad, igual que la referencia
                }

                setter.Set(item, Coerce(raw, setter));
            }

            return item;
        }

        private static List<(int Ordinal, ColumnSetter Setter)> BuildPlan(
            IDataReader reader, IReadOnlyDictionary<string, ColumnSetter> setters)
        {
            var plan = new List<(int, ColumnSetter)>(reader.FieldCount);
            for (var i = 0; i < reader.FieldCount; i++)
            {
                var name = reader.GetName(i).ToUpperInvariant();
                if (setters.TryGetValue(name, out var setter))
                {
                    plan.Add((i, setter));
                }
            }

            return plan;
        }

        private static IReadOnlyDictionary<string, ColumnSetter> GetSetters(Type type)
            => Cache.GetOrAdd(type, BuildSetters);

        private static IReadOnlyDictionary<string, ColumnSetter> BuildSetters(Type type)
        {
            var map = new Dictionary<string, ColumnSetter>(StringComparer.Ordinal);

            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                                 .Where(p => p.CanWrite && p.IsDefined(typeof(ColumnAttribute), inherit: false));

            foreach (var property in properties)
            {
                var column = property.GetCustomAttribute<ColumnAttribute>(inherit: false);
                if (column is null || string.IsNullOrWhiteSpace(column.Name))
                {
                    continue;
                }

                var key = column.Name.ToUpperInvariant();
                var targetType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;

                map[key] = new ColumnSetter(
                    targetType,
                    targetType == typeof(bool),
                    CompileSetter(property));
            }

            return map;
        }

        private static Action<object, object?> CompileSetter(PropertyInfo property)
        {
            // (target, value) => ((TDeclaring)target).Prop = (TProp)value;
            var targetParam = Expression.Parameter(typeof(object), "target");
            var valueParam = Expression.Parameter(typeof(object), "value");

            var body = Expression.Assign(
                Expression.Property(Expression.Convert(targetParam, property.DeclaringType!), property),
                Expression.Convert(valueParam, property.PropertyType));

            return Expression.Lambda<Action<object, object?>>(body, targetParam, valueParam).Compile();
        }

        /// <summary>
        /// Convierte el valor crudo del reader al tipo destino conservando la semantica de
        /// <c>ModoRepository.txt</c> y añadiendo casos frecuentes (Guid, enum, DateTimeOffset, TimeSpan).
        /// </summary>
        private static object Coerce(object raw, ColumnSetter setter)
        {
            var targetType = setter.TargetType;

            if (setter.IsBoolean)
            {
                return raw switch
                {
                    string s => s.Equals("Y", StringComparison.OrdinalIgnoreCase)
                                || s.Equals("1", StringComparison.OrdinalIgnoreCase)
                                || s.Equals("true", StringComparison.OrdinalIgnoreCase),
                    bool b => b,
                    decimal or long or int or short or byte => Convert.ToInt64(raw, CultureInfo.InvariantCulture) == 1,
                    _ => Convert.ToBoolean(raw, CultureInfo.InvariantCulture)
                };
            }

            // Fast path: el proveedor ya devolvio el tipo correcto.
            if (raw.GetType() == targetType)
            {
                return raw;
            }

            if (targetType == typeof(string))
            {
                return raw as string ?? Convert.ToString(raw, CultureInfo.InvariantCulture) ?? string.Empty;
            }

            if (targetType.IsEnum)
            {
                return raw is string es
                    ? Enum.Parse(targetType, es, ignoreCase: true)
                    : Enum.ToObject(targetType, Convert.ToInt64(raw, CultureInfo.InvariantCulture));
            }

            if (targetType == typeof(Guid))
            {
                return raw switch
                {
                    Guid g => g,
                    byte[] bytes => new Guid(bytes),
                    string gs => Guid.Parse(gs),
                    _ => Guid.Parse(Convert.ToString(raw, CultureInfo.InvariantCulture)!)
                };
            }

            if (targetType == typeof(DateTimeOffset))
            {
                return raw is DateTimeOffset dto
                    ? dto
                    : new DateTimeOffset(Convert.ToDateTime(raw, CultureInfo.InvariantCulture));
            }

            if (targetType == typeof(TimeSpan))
            {
                return raw is TimeSpan ts ? ts : TimeSpan.Parse(Convert.ToString(raw, CultureInfo.InvariantCulture)!, CultureInfo.InvariantCulture);
            }

            return Convert.ChangeType(raw, targetType, CultureInfo.InvariantCulture);
        }

        private sealed class ColumnSetter
        {
            public ColumnSetter(Type targetType, bool isBoolean, Action<object, object?> set)
            {
                TargetType = targetType;
                IsBoolean = isBoolean;
                Set = set;
            }

            public Type TargetType { get; }

            public bool IsBoolean { get; }

            public Action<object, object?> Set { get; }
        }
    }
}
