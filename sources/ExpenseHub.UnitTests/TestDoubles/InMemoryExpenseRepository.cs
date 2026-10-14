using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using ExpenseHub.Api.Data;
using ExpenseHub.Api.Domain;

namespace ExpenseHub.UnitTests.TestDoubles;

/// <summary>
/// Repositório em memória que imita a unidade de trabalho do EF Core:
/// inclusões ficam pendentes até <see cref="SaveChangesAsync"/>, que as confirma juntas ou descarta todas em caso de falha.
/// Os filtros de escopo são aplicados como expressões, como no banco.
/// </summary>
internal sealed class InMemoryExpenseRepository : IExpenseRepository
{
    private const int SeededCategoryCount = 5;

    private readonly List<Expense> _expenses = [];
    private readonly List<ExpenseHistory> _history = [];
    private readonly List<PaymentRecord> _payments = [];
    private readonly List<Expense> _pendingExpenses = [];
    private readonly List<ExpenseHistory> _pendingHistory = [];
    private readonly List<PaymentRecord> _pendingPayments = [];

    /// <summary>Reembolsos confirmados.</summary>
    public IReadOnlyList<Expense> Expenses => _expenses;

    /// <summary>Entradas de histórico confirmadas.</summary>
    public IReadOnlyList<ExpenseHistory> History => _history;

    /// <summary>Pagamentos confirmados.</summary>
    public IReadOnlyList<PaymentRecord> Payments => _payments;

    /// <summary>Quantas vezes <see cref="SaveChangesAsync"/> foi chamado (inclusive as que falharam).</summary>
    public int SaveChangesCount { get; private set; }

    /// <summary>Faz o próximo <see cref="SaveChangesAsync"/> falhar com <see cref="InvalidOperationException"/>, descartando as pendências.</summary>
    public bool FailNextSave { get; set; }

    /// <summary>Faz o próximo <see cref="SaveChangesAsync"/> falhar com <see cref="ConcurrencyConflictException"/>, descartando as pendências.</summary>
    public bool ConflictOnNextSave { get; set; }

    /// <summary>Grava um reembolso diretamente como confirmado (arrange dos testes).</summary>
    public void Seed(Expense expense)
    {
        ArgumentNullException.ThrowIfNull(expense);
        _expenses.Add(expense);
    }

    public Task<bool> CategoryExistsAsync(int categoryId, CancellationToken cancellationToken)
    {
        return Task.FromResult(categoryId is >= 1 and <= SeededCategoryCount);
    }

    public Task<Expense?> FindAsync(Guid id, Expression<Func<Expense, bool>> scope, CancellationToken cancellationToken)
    {
        Expense? expense = _expenses.AsQueryable().Where(scope).FirstOrDefault(item => item.Id == id);
        return Task.FromResult(expense);
    }

    public Task<IReadOnlyList<Expense>> ListAsync(Expression<Func<Expense, bool>> scope, CancellationToken cancellationToken)
    {
        IReadOnlyList<Expense> expenses = _expenses.AsQueryable()
            .Where(scope)
            .OrderByDescending(item => item.CreatedAtUtc)
            .ToList();
        return Task.FromResult(expenses);
    }

    public Task<IReadOnlyList<ExpenseHistory>> ListHistoryAsync(Guid expenseId, CancellationToken cancellationToken)
    {
        IReadOnlyList<ExpenseHistory> entries = _history
            .Where(entry => entry.ExpenseId == expenseId)
            .OrderBy(entry => entry.OccurredAtUtc)
            .ToList();
        return Task.FromResult(entries);
    }

    public void Add(Expense expense)
    {
        _pendingExpenses.Add(expense);
    }

    public void AddHistory(ExpenseHistory entry)
    {
        _pendingHistory.Add(entry);
    }

    public void AddPayment(PaymentRecord payment)
    {
        _pendingPayments.Add(payment);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        SaveChangesCount++;

        if (FailNextSave)
        {
            FailNextSave = false;
            DiscardPending();
            throw new InvalidOperationException("Falha de persistência simulada.");
        }

        if (ConflictOnNextSave)
        {
            ConflictOnNextSave = false;
            DiscardPending();
            throw new ConcurrencyConflictException("Conflito de concorrência simulado.");
        }

        _expenses.AddRange(_pendingExpenses);
        _history.AddRange(_pendingHistory);
        _payments.AddRange(_pendingPayments);
        DiscardPending();
        return Task.CompletedTask;
    }

    private void DiscardPending()
    {
        _pendingExpenses.Clear();
        _pendingHistory.Clear();
        _pendingPayments.Clear();
    }
}
