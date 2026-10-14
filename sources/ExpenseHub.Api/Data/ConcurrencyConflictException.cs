using System;

namespace ExpenseHub.Api.Data;

/// <summary>
/// Indica que o registro foi alterado por outra operação entre a leitura e a gravação (HTTP 409).
/// </summary>
internal sealed class ConcurrencyConflictException : Exception
{
    public ConcurrencyConflictException()
    {
    }

    public ConcurrencyConflictException(string message)
        : base(message)
    {
    }

    public ConcurrencyConflictException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
