using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using ExpenseHub.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace ExpenseHub.Api.Data;

internal sealed class ExpenseRepository : IExpenseRepository
{
    private readonly ExpenseHubDbContext _context;

    public ExpenseRepository(ExpenseHubDbContext context)
    {
        _context = context;
    }

    public Task<bool> CategoryExistsAsync(int categoryId, CancellationToken cancellationToken)
    {
        return _context.ExpenseCategories.AnyAsync(category => category.Id == categoryId, cancellationToken);
    }

    public Task<Expense?> FindAsync(Guid id, Expression<Func<Expense, bool>> scope, CancellationToken cancellationToken)
    {
        return _context.Expenses
            .Include(expense => expense.Category)
            .Include(expense => expense.Payment)
            .Where(scope)
            .FirstOrDefaultAsync(expense => expense.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Expense>> ListAsync(Expression<Func<Expense, bool>> scope, CancellationToken cancellationToken)
    {
        return await _context.Expenses
            .AsNoTracking()
            .Include(expense => expense.Category)
            .Include(expense => expense.Payment)
            .Where(scope)
            .OrderByDescending(expense => expense.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ExpenseHistory>> ListHistoryAsync(Guid expenseId, CancellationToken cancellationToken)
    {
        return await _context.ExpenseHistory
            .AsNoTracking()
            .Where(entry => entry.ExpenseId == expenseId)
            .OrderBy(entry => entry.OccurredAtUtc)
            .ThenBy(entry => entry.Id)
            .ToListAsync(cancellationToken);
    }

    public void Add(Expense expense)
    {
        _context.Expenses.Add(expense);
    }

    public void AddHistory(ExpenseHistory entry)
    {
        _context.ExpenseHistory.Add(entry);
    }

    public void AddPayment(PaymentRecord payment)
    {
        _context.PaymentRecords.Add(payment);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new ConcurrencyConflictException("O reembolso foi alterado por outra operação. Consulte-o novamente e repita a ação.", exception);
        }
    }
}
