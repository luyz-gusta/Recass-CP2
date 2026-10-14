using System;
using ExpenseHub.Api.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ExpenseHub.Api.Data;

/// <summary>
/// Contexto do Entity Framework Core com as tabelas do Identity e do fluxo de reembolsos.
/// </summary>
internal sealed class ExpenseHubDbContext : IdentityDbContext<IdentityUser>
{
    public ExpenseHubDbContext(DbContextOptions<ExpenseHubDbContext> options)
        : base(options)
    {
    }

    public DbSet<Expense> Expenses => Set<Expense>();

    public DbSet<ExpenseCategory> ExpenseCategories => Set<ExpenseCategory>();

    public DbSet<ExpenseHistory> ExpenseHistory => Set<ExpenseHistory>();

    public DbSet<PaymentRecord> PaymentRecords => Set<PaymentRecord>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(ExpenseHubDbContext).Assembly);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Vale também para DateTime? — os horários são gravados e lidos sempre como UTC.
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
    }
}
