using System.Collections;
using AIEP.Application.Model.Models.Recurso;

namespace AIEP.Test.TestData
{
    /// <summary>Casos de prueba para <c>RecursoLogic.GetRecursos</c> (equivalente NUnit del ClassData de Sedes).</summary>
    public class RecursoRequestData : IEnumerable
    {
        public IEnumerator GetEnumerator()
        {
            yield return new TestCaseData(new RecursoRequest { codigo = "1" });
            yield return new TestCaseData(new RecursoRequest { codigo = "2" });
            yield return new TestCaseData(new RecursoRequest { codigo = "3" });
        }
    }
}
