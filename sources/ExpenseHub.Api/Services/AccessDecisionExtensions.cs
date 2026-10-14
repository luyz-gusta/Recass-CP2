using System;

namespace ExpenseHub.Api.Services;

/// <summary>
/// Converte uma <see cref="AccessDecision"/> negada no <see cref="ServiceResult{T}"/> correspondente.
/// </summary>
internal static class AccessDecisionExtensions
{
    public static ServiceResult<T> ToDenied<T>(this AccessDecision decision, string notFoundMessage, string forbiddenMessage)
    {
        return decision switch
        {
            AccessDecision.NotFound => ServiceResult.NotFound<T>(notFoundMessage),
            AccessDecision.Forbidden => ServiceResult.Forbidden<T>(forbiddenMessage),
            _ => throw new InvalidOperationException("Somente uma decisão negada pode virar um resultado de falha."),
        };
    }
}
