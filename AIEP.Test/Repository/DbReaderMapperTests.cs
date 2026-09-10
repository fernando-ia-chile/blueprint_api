using System.Data;
using AIEP.Application.Repository.Infrastructure;
using AIEP.Domain.DataBlueprint.Entities;

namespace AIEP.Test.Repository
{
    [TestFixture]
    public class DbReaderMapperTests
    {
        private static DataTable BuildRecursoTable(Type activoType)
        {
            var table = new DataTable();
            table.Columns.Add("CODIGO_RECURSO", typeof(string));
            table.Columns.Add("NOMBRE_RECURSO", typeof(string));
            table.Columns.Add("ACTIVO", activoType);
            table.Columns.Add("CUPOS", typeof(int));
            table.Columns.Add("FECHA_ALTA", typeof(DateTime));
            table.Columns.Add("VALOR", typeof(decimal));
            table.Columns.Add("COLUMNA_NO_MAPEADA", typeof(string));
            return table;
        }

        [Test]
        public async Task MapReaderToListAsync_MapeaColumnasYDejaLosNulosEnDefault()
        {
            var table = BuildRecursoTable(typeof(string));
            table.Rows.Add("R1", "Recurso 1", "Y", 30, new DateTime(2026, 1, 15), 1999.90m, "ignorar");
            table.Rows.Add("R2", DBNull.Value, "N", DBNull.Value, DBNull.Value, DBNull.Value, "ignorar");

            using var reader = table.CreateDataReader();
            var list = await DbReaderMapper.MapReaderToListAsync<RecursoEntity>(reader);

            Assert.AreEqual(2, list.Count);

            Assert.AreEqual("R1", list[0].CodigoRecurso);
            Assert.AreEqual("Recurso 1", list[0].NombreRecurso);
            Assert.IsTrue(list[0].Activo);
            Assert.AreEqual(30, list[0].Cupos);
            Assert.AreEqual(new DateTime(2026, 1, 15), list[0].FechaAlta);
            Assert.AreEqual(1999.90m, list[0].Valor);

            Assert.AreEqual("R2", list[1].CodigoRecurso);
            Assert.IsNull(list[1].NombreRecurso);   // DBNull => default
            Assert.IsFalse(list[1].Activo);          // "N" => false
            Assert.IsNull(list[1].Cupos);
            Assert.IsNull(list[1].FechaAlta);
            Assert.IsNull(list[1].Valor);
        }

        [Test]
        public async Task MapReaderToListAsync_CoercionaBooleanoNumericoYUsaCacheEstable()
        {
            var table = BuildRecursoTable(typeof(int));
            table.Rows.Add("R1", "Uno", 1, 10, new DateTime(2026, 3, 1), 0m, null);
            table.Rows.Add("R2", "Dos", 0, 20, new DateTime(2026, 3, 2), 0m, null);

            using (var reader1 = table.CreateDataReader())
            {
                var first = await DbReaderMapper.MapReaderToListAsync<RecursoEntity>(reader1);
                Assert.IsTrue(first[0].Activo);
                Assert.IsFalse(first[1].Activo);
            }

            // Segunda pasada: la cache estatica de setters no debe alterar el resultado.
            using var reader2 = table.CreateDataReader();
            var second = await DbReaderMapper.MapReaderToListAsync<RecursoEntity>(reader2);
            Assert.IsTrue(second[0].Activo);
            Assert.IsFalse(second[1].Activo);
            Assert.AreEqual("Uno", second[0].NombreRecurso);
        }

        [Test]
        public void MapReaderToList_Sincrono_MapeaResultSet()
        {
            var table = BuildRecursoTable(typeof(string));
            table.Rows.Add("R9", "Nueve", "Y", 5, new DateTime(2026, 6, 6), 12.34m, null);

            using var reader = table.CreateDataReader();
            var list = DbReaderMapper.MapReaderToList<RecursoDetalleEntity>(reader);

            // RecursoDetalleEntity solo mapea CODIGO_RECURSO; el resto de columnas se ignora.
            Assert.AreEqual(1, list.Count);
            Assert.AreEqual("R9", list[0].CodigoRecurso);
            Assert.IsNull(list[0].CodigoDetalle);
        }
    }
}
