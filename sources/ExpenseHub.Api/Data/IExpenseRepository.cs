using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using ExpenseHub.Api.Domain;

namespace ExpenseHub.Api.Data;

/// <summary>
/// Único acesso a dados dos serviços de reembolso. Os filtros de escopo são aplicados na consulta,
/// antes de materializar os dados, e cada operação lógica é confirmada com um único <see cref="SaveChangesAsync"/>.
/// </summary>
internal interface IExpenseRepository
{
    Task<bool> CategoryExistsAsync(int categoryId, CancellationToken cancellationToken);

    /// <summary>
    /// Busca um reembolso rastreado (com categoria e pagamento) que também satisfaça o <paramref name="scope"/>.
    /// </summary>
    Task<Expense?> FindAsync(Guid id, Expression<Func<Expense, bool>> scope, CancellationToken cancellationToken);

    /// <summary>
    /// Lista, sem rastreamento, os reembolsos que satisfazem o <paramref name="scope"/>, do mais recente para o mais antigo.
    /// </summary>
    Task<IReadOnlyList<Expense>> ListAsync(Expression<Func<Expense, bool>> scope, CancellationToken cancellationToken);

    /// <summary>
    /// Lista o histórico de um reembolso em ordem cronológica.
    /// </summary>
    Task<IReadOnlyList<ExpenseHistory>> ListHistoryAsync(Guid expenseId, CancellationToken cancellationToken);

    void Add(Expense expense);

    void AddHistory(ExpenseHistory entry);

    void AddPayment(PaymentRecord payment);

    /// <summary>
    /// Confirma, numa única transação, todas as alterações pendentes (reembolso, histórico e pagamento).
    /// </summary>
    /// <exception cref="ConcurrencyConflictException">O reembolso foi alterado por outra operação.</exception>
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
