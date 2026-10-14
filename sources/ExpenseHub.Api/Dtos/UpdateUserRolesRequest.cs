using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ExpenseHub.Api.Dtos;

/// <summary>
/// Novo conjunto de roles de um usuário (<c>PUT /api/admin/users/{id}/roles</c>).
/// </summary>
public sealed class UpdateUserRolesRequest
{
    /// <summary>
    /// Lista completa de roles, que substitui as atuais (<c>[]</c> remove todas).
    /// Valores aceitos: Admin, Employee, Approver, Finance e Auditor.
    /// </summary>
    [Required]
    public IReadOnlyList<string>? Roles { get; init; }
}
