using System;
using System.Collections.Generic;
using System.Linq;
using ExpenseHub.Api.Domain;
using ExpenseHub.Api.Dtos;

namespace ExpenseHub.Api.Services;

/// <summary>
/// Regras para alterar as roles de um usuário: só roles conhecidas (nunca criadas implicitamente),
/// sem duplicatas, e o Admin não pode remover a própria role Admin.
/// </summary>
internal static class RoleChangePolicy
{
    private const string RolesField = nameof(UpdateUserRolesRequest.Roles);

    /// <summary>
    /// Valida e normaliza as roles pedidas para os nomes canônicos de <see cref="AppRoles"/>, em ordem alfabética.
    /// </summary>
    /// <param name="requestedRoles">Roles enviadas pelo Admin.</param>
    /// <param name="isOwnAccount">Indica se o Admin está alterando a própria conta.</param>
    public static ServiceResult<IReadOnlyList<string>> Evaluate(IReadOnlyCollection<string?>? requestedRoles, bool isOwnAccount)
    {
        if (requestedRoles is null)
        {
            return ServiceResult.Invalid<IReadOnlyList<string>>(RolesField, "Informe a lista de roles. Use [] para remover todas.");
        }

        List<string> roles = [];
        List<string> unknown = [];
        foreach (string? requested in requestedRoles)
        {
            string? canonical = AppRoles.All.FirstOrDefault(role => string.Equals(role, requested?.Trim(), StringComparison.OrdinalIgnoreCase));
            if (canonical is null)
            {
                unknown.Add(string.IsNullOrWhiteSpace(requested) ? "(vazio)" : requested.Trim());
            }
            else if (!roles.Contains(canonical, StringComparer.Ordinal))
            {
                roles.Add(canonical);
            }
        }

        if (unknown.Count > 0)
        {
            return ServiceResult.Invalid<IReadOnlyList<string>>(
                RolesField,
                $"Roles desconhecidas: {string.Join(", ", unknown)}. Valores aceitos: {string.Join(", ", AppRoles.All)}.");
        }

        if (isOwnAccount && !roles.Contains(AppRoles.Admin, StringComparer.Ordinal))
        {
            return ServiceResult.Conflict<IReadOnlyList<string>>("O Admin não pode remover a própria role Admin.");
        }

        roles.Sort(StringComparer.Ordinal);
        return ServiceResult.Success<IReadOnlyList<string>>(roles);
    }
}
