using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ExpenseHub.Api.Contracts;
using ExpenseHub.Api.Dtos;

namespace ExpenseHub.Api.Services;

/// <summary>
/// Administração de usuários e roles, exclusiva da role Admin.
/// </summary>
public interface IUserAdminService
{
    /// <summary>
    /// Lista os usuários com as respectivas roles, ordenados por e-mail.
    /// </summary>
    /// <param name="actor">Usuário autenticado.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>Os usuários; Forbidden se o ator não for Admin.</returns>
    Task<ServiceResult<IReadOnlyList<UserResponse>>> ListUsersAsync(Actor actor, CancellationToken cancellationToken);

    /// <summary>
    /// Substitui o conjunto de roles de um usuário. As novas roles só valem após um novo login dele.
    /// </summary>
    /// <param name="actor">Usuário autenticado.</param>
    /// <param name="userId">Identificador do usuário alterado.</param>
    /// <param name="request">Conjunto completo de roles.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>
    /// O usuário com as novas roles; Forbidden se o ator não for Admin; Validation para role desconhecida;
    /// NotFound para usuário inexistente; Conflict se o Admin tentar remover a própria role Admin.
    /// </returns>
    Task<ServiceResult<UserResponse>> UpdateRolesAsync(Actor actor, string userId, UpdateUserRolesRequest request, CancellationToken cancellationToken);
}
