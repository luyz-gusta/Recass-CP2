using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ExpenseHub.Api.Contracts;
using ExpenseHub.Api.Data;
using ExpenseHub.Api.Domain;
using ExpenseHub.Api.Dtos;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace ExpenseHub.Api.Services;

internal sealed class UserAdminService : IUserAdminService
{
    private const string AdminOnlyMessage = "Somente usuários com a role Admin administram usuários e roles.";

    private readonly ExpenseHubDbContext _context;
    private readonly UserManager<IdentityUser> _userManager;

    public UserAdminService(ExpenseHubDbContext context, UserManager<IdentityUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public async Task<ServiceResult<IReadOnlyList<UserResponse>>> ListUsersAsync(Actor actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);

        if (!actor.IsInRole(AppRoles.Admin))
        {
            return ServiceResult.Forbidden<IReadOnlyList<UserResponse>>(AdminOnlyMessage);
        }

        // Duas consultas fixas (sem N+1) e somente os campos públicos: nada de hash, stamps ou tokens.
        var users = await _context.Users
            .AsNoTracking()
            .OrderBy(user => user.NormalizedEmail)
            .Select(user => new { user.Id, user.Email })
            .ToListAsync(cancellationToken);

        var assignments = await (
                from userRole in _context.UserRoles
                join role in _context.Roles on userRole.RoleId equals role.Id
                select new { userRole.UserId, role.Name })
            .ToListAsync(cancellationToken);

        ILookup<string, string> rolesByUser = assignments
            .Where(assignment => assignment.Name is not null)
            .ToLookup(assignment => assignment.UserId, assignment => assignment.Name!, StringComparer.Ordinal);

        List<UserResponse> response = users
            .Select(user => new UserResponse(
                user.Id,
                user.Email ?? string.Empty,
                rolesByUser[user.Id].Order(StringComparer.Ordinal).ToList()))
            .ToList();

        return ServiceResult.Success<IReadOnlyList<UserResponse>>(response);
    }

    public async Task<ServiceResult<UserResponse>> UpdateRolesAsync(Actor actor, string userId, UpdateUserRolesRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(request);

        if (!actor.IsInRole(AppRoles.Admin))
        {
            return ServiceResult.Forbidden<UserResponse>(AdminOnlyMessage);
        }

        bool isOwnAccount = string.Equals(userId, actor.UserId, StringComparison.Ordinal);
        ServiceResult<IReadOnlyList<string>> evaluation = RoleChangePolicy.Evaluate(request.Roles, isOwnAccount);
        if (!evaluation.Succeeded)
        {
            return evaluation.ToFailure<UserResponse>();
        }

        IdentityUser? user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return ServiceResult.NotFound<UserResponse>("Usuário não encontrado.");
        }

        IReadOnlyList<string> desiredRoles = evaluation.Value!;
        IList<string> currentRoles = await _userManager.GetRolesAsync(user);
        string[] rolesToRemove = currentRoles.Except(desiredRoles, StringComparer.Ordinal).ToArray();
        string[] rolesToAdd = desiredRoles.Except(currentRoles, StringComparer.Ordinal).ToArray();

        if (rolesToRemove.Length > 0 || rolesToAdd.Length > 0)
        {
            // Remoção, inclusão e novo carimbo de segurança: tudo ou nada.
            await using IDbContextTransaction transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            EnsureSucceeded(await _userManager.RemoveFromRolesAsync(user, rolesToRemove));
            EnsureSucceeded(await _userManager.AddToRolesAsync(user, rolesToAdd));
            EnsureSucceeded(await _userManager.UpdateSecurityStampAsync(user));
            await transaction.CommitAsync(cancellationToken);
        }

        return ServiceResult.Success(new UserResponse(user.Id, user.Email ?? string.Empty, desiredRoles));
    }

    private static void EnsureSucceeded(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            string codes = string.Join(", ", result.Errors.Select(error => error.Code));
            throw new InvalidOperationException($"Falha ao atualizar as roles no Identity ({codes}).");
        }
    }
}
