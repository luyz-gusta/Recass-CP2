using ExpenseHub.Api.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExpenseHub.Api.Data.Configurations;

internal sealed class ExpenseHistoryConfiguration : IEntityTypeConfiguration<ExpenseHistory>
{
    public void Configure(EntityTypeBuilder<ExpenseHistory> builder)
    {
        builder.ToTable("ExpenseHistory");
        builder.HasKey(entry => entry.Id);

        builder.Property(entry => entry.ActorId).IsRequired();
        builder.Property(entry => entry.Action).HasConversion<string>().HasMaxLength(20);
        builder.Property(entry => entry.PreviousStatus).HasConversion<string>().HasMaxLength(20);
        builder.Property(entry => entry.NewStatus).HasConversion<string>().HasMaxLength(20);
        builder.Property(entry => entry.Justification).HasMaxLength(500);
        builder.Property(entry => entry.Changes).HasMaxLength(2000);

        builder.HasOne<Expense>()
            .WithMany()
            .HasForeignKey(entry => entry.ExpenseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<IdentityUser>()
            .WithMany()
            .HasForeignKey(entry => entry.ActorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(entry => entry.ExpenseId);
    }
}
