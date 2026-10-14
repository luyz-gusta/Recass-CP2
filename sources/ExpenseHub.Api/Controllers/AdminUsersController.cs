using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ExpenseHub.Api.Contracts;
using ExpenseHub.Api.Domain;
using ExpenseHub.Api.Dtos;
using ExpenseHub.Api.Http;
using ExpenseHub.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseHub.Api.Controllers;

/// <summary>
/// Administração de usuários e roles. Exclusivo da role Admin.
/// </summary>
[ApiController]
[Route("api/admin/users")]
[Authorize(Roles = AppRoles.Admin)]
public sealed class AdminUsersController : ControllerBase
{
    private readonly IUserAdminService _userAdminService;

    /// <summary>
    /// Cria o controller de administração de usuários.
    /// </summary>
    /// <param name="userAdminService">Serviço de administração de usuários.</param>
    public AdminUsersController(IUserAdminService userAdminService)
    {
        _userAdminService = userAdminService;
    }

    /// <summary>
    /// Lista os usuários e as respectivas roles.
    /// </summary>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>200 com os usuários.</returns>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<UserResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListAsync(CancellationToken cancellationToken)
    {
        ServiceResult<IReadOnlyList<UserResponse>> result = await _userAdminService.ListUsersAsync(User.ToActor(), cancellationToken);
        return this.ToActionResult(result, users => Ok(users));
    }

    /// <summary>
    /// Substitui as roles de um usuário. O usuário precisa fazer login novamente para receber as novas roles.
    /// </summary>
    /// <param name="id">Identificador do usuário.</param>
    /// <param name="request">Conjunto completo de roles.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>200 com o usuário atualizado; 400 para role desconhecida; 404 para usuário inexistente; 409 se o Admin remover a própria role Admin.</returns>
    [HttpPut("{id}/roles")]
    [ProducesResponseType<UserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateRolesAsync(string id, UpdateUserRolesRequest request, CancellationToken cancellationToken)
    {
        ServiceResult<UserResponse> result = await _userAdminService.UpdateRolesAsync(User.ToActor(), id, request, cancellationToken);
        return this.ToActionResult(result, user => Ok(user));
    }
}
