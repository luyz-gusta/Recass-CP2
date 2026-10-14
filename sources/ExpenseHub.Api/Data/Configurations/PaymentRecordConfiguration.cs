using ExpenseHub.Api.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExpenseHub.Api.Data.Configurations;

internal sealed class PaymentRecordConfiguration : IEntityTypeConfiguration<PaymentRecord>
{
    public void Configure(EntityTypeBuilder<PaymentRecord> builder)
    {
        builder.ToTable("PaymentRecords");
        builder.HasKey(payment => payment.Id);

        builder.Property(payment => payment.Id).ValueGeneratedNever();
        builder.Property(payment => payment.Amount).HasPrecision(18, 2);
        builder.Property(payment => payment.PaidById).IsRequired();

        builder.HasOne<IdentityUser>()
            .WithMany()
            .HasForeignKey(payment => payment.PaidById)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(payment => payment.ExpenseId).IsUnique();
    }
}
