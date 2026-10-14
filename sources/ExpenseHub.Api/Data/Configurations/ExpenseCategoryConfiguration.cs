using ExpenseHub.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExpenseHub.Api.Data.Configurations;

internal sealed class ExpenseCategoryConfiguration : IEntityTypeConfiguration<ExpenseCategory>
{
    public void Configure(EntityTypeBuilder<ExpenseCategory> builder)
    {
        builder.ToTable("ExpenseCategories");
        builder.HasKey(category => category.Id);

        builder.Property(category => category.Id).ValueGeneratedNever();
        builder.Property(category => category.Name).IsRequired().HasMaxLength(100);
        builder.HasIndex(category => category.Name).IsUnique();

        // Dados de referência (não são usuários): IDs fixos, documentados no README.
        builder.HasData(
            new ExpenseCategory(1, "Alimentação"),
            new ExpenseCategory(2, "Transporte"),
            new ExpenseCategory(3, "Hospedagem"),
            new ExpenseCategory(4, "Material de escritório"),
            new ExpenseCategory(5, "Outros"));
    }
}
