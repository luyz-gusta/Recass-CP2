using System;
using System.Threading.Tasks;
using ExpenseHub.Api.Contracts;
using ExpenseHub.Api.Dtos;
using Microsoft.AspNetCore.Identity;

namespace ExpenseHub.Api.Services;

internal sealed class AccountService : IAccountService
{
    private readonly UserManager<IdentityUser> _userManager;

    public AccountService(UserManager<IdentityUser> userManager)
    {
        _userManager = userManager;
    }

    public async Task<ServiceResult<UserResponse>> RegisterAsync(RegisterRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        string email = request.Email.Trim();
        IdentityUser user = new() { UserName = email, Email = email };

        // Nenhuma role é atribuída aqui: somente um Admin concede roles.
        IdentityResult result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            return IdentityErrorMapper.ToFailure<UserResponse>(result.Errors);
        }

        return ServiceResult.Success(new UserResponse(user.Id, email, []));
    }
}
