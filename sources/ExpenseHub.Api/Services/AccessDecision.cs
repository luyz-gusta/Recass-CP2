namespace ExpenseHub.Api.Services;

/// <summary>
/// Resultado de uma decisão de acesso de <see cref="ExpenseAccessPolicy"/>.
/// </summary>
internal enum AccessDecision
{
    /// <summary>A operação é permitida.</summary>
    Allowed = 0,

    /// <summary>O recurso não existe para este usuário (HTTP 404), sem revelar que existe.</summary>
    NotFound = 1,

    /// <summary>O usuário não pode executar a operação (HTTP 403).</summary>
    Forbidden = 2,
}
