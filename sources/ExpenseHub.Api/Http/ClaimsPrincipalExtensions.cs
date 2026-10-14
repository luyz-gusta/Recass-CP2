using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using ExpenseHub.Api.Services;

namespace ExpenseHub.Api.Http;

/// <summary>
/// Converte o usuário autenticado (claims do token) no <see cref="Actor"/> usado pelos serviços.
/// </summary>
internal static class ClaimsPrincipalExtensions
{
    public static Actor ToActor(this ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        string userId = principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("O usuário autenticado não possui a claim NameIdentifier.");

        HashSet<string> roles = principal.FindAll(ClaimTypes.Role)
            .Select(claim => claim.Value)
            .ToHashSet(StringComparer.Ordinal);

        return new Actor(userId, roles);
    }
}
