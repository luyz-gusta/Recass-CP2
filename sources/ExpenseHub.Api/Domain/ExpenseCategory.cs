namespace ExpenseHub.Api.Domain;

/// <summary>
/// Categoria de despesa. As categorias são cadastradas pela migration (seed de dados de referência).
/// </summary>
internal sealed class ExpenseCategory
{
    public ExpenseCategory(int id, string name)
    {
        Id = id;
        Name = name;
    }

    public int Id { get; private set; }

    public string Name { get; private set; }
}
