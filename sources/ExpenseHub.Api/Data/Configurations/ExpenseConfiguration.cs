using ExpenseHub.Api.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExpenseHub.Api.Data.Configurations;

internal sealed class ExpenseConfiguration : IEntityTypeConfiguration<Expense>
{
    public void Configure(EntityTypeBuilder<Expense> builder)
    {
        builder.ToTable("Expenses");
        builder.HasKey(expense => expense.Id);

        builder.Property(expense => expense.Id).ValueGeneratedNever();
        builder.Property(expense => expense.OwnerId).IsRequired();
        builder.Property(expense => expense.Description).IsRequired().HasMaxLength(500);
        builder.Property(expense => expense.Amount).HasPrecision(18, 2);
        builder.Property(expense => expense.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(expense => expense.RejectionReason).HasMaxLength(500);
        builder.Property(expense => expense.ConcurrencyStamp).IsConcurrencyToken();

        builder.HasOne<IdentityUser>()
            .WithMany()
            .HasForeignKey(expense => expense.OwnerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<IdentityUser>()
            .WithMany()
            .HasForeignKey(expense => expense.DecidedById)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(expense => expense.Category)
            .WithMany()
            .HasForeignKey(expense => expense.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(expense => expense.Payment)
            .WithOne()
            .HasForeignKey<PaymentRecord>(payment => payment.ExpenseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(expense => expense.OwnerId);
        builder.HasIndex(expense => expense.Status);
    }
}
