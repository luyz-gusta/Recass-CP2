using System;
using System.Collections.Generic;
using System.Linq;
using ExpenseHub.Api.Dtos;
using Microsoft.AspNetCore.Identity;

namespace ExpenseHub.Api.Services;

/// <summary>
/// Traduz erros do Identity para o contrato da API: e-mail já cadastrado é conflito (409); o resto é validação (400).
/// </summary>
internal static class IdentityErrorMapper
{
    public static ServiceResult<T> ToFailure<T>(IEnumerable<IdentityError> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);

        List<IdentityError> errorList = errors.ToList();
        if (errorList.Exists(error => error.Code is nameof(IdentityErrorDescriber.DuplicateEmail) or nameof(IdentityErrorDescriber.DuplicateUserName)))
        {
            return ServiceResult.Conflict<T>("Já existe um usuário cadastrado com este e-mail.");
        }

        Dictionary<string, string[]> fieldErrors = errorList
            .GroupBy(error => FieldFor(error.Code), StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(error => error.Description).ToArray(),
                StringComparer.Ordinal);

        return ServiceResult.Invalid<T>(fieldErrors, "Os dados informados não atendem às regras de cadastro.");
    }

    private static string FieldFor(string code)
    {
        return code.StartsWith(nameof(RegisterRequest.Password), StringComparison.Ordinal)
            ? nameof(RegisterRequest.Password)
            : nameof(RegisterRequest.Email);
    }
}
