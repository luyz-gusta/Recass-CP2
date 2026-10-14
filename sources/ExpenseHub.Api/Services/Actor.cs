using System.Collections.Generic;

namespace ExpenseHub.Api.Services;

/// <summary>
/// Usuário autenticado que executa uma operação. É montado a partir das claims do token, nunca do corpo da requisição.
/// </summary>
/// <param name="UserId">Identificador do usuário no Identity.</param>
/// <param name="Roles">Roles presentes no token (concedidas no momento do login).</param>
public sealed record Actor(string UserId, IReadOnlySet<string> Roles)
{
    /// <summary>
    /// Indica se o ator possui a role informada. A comparação é exata, com os nomes canônicos das roles.
    /// </summary>
    /// <param name="role">Nome canônico da role.</param>
    /// <returns><see langword="true"/> quando o ator possui a role.</returns>
    public bool IsInRole(string role)
    {
        return Roles.Contains(role);
    }
}
