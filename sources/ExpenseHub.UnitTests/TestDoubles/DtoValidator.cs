using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace ExpenseHub.UnitTests.TestDoubles;

/// <summary>
/// Executa as DataAnnotations de um DTO como o <c>[ApiController]</c> faz antes de chamar a action.
/// </summary>
internal static class DtoValidator
{
    public static IReadOnlyList<ValidationResult> Validate(object dto)
    {
        List<ValidationResult> results = [];
        Validator.TryValidateObject(dto, new ValidationContext(dto), results, validateAllProperties: true);
        return results;
    }

    /// <summary>Nomes das propriedades que falharam na validação.</summary>
    public static IReadOnlyList<string> InvalidMembers(object dto)
    {
        return Validate(dto).SelectMany(result => result.MemberNames).Distinct().ToList();
    }
}
