namespace ExpenseHub.Api.Services;

/// <summary>
/// Categorias de falha devolvidas pelos serviços e traduzidas para status HTTP pelos controllers.
/// </summary>
public enum ServiceError
{
    /// <summary>Operação concluída sem erro.</summary>
    None = 0,

    /// <summary>Entrada inválida (HTTP 400).</summary>
    Validation = 1,

    /// <summary>Recurso inexistente ou fora do escopo de leitura do usuário (HTTP 404).</summary>
    NotFound = 2,

    /// <summary>Usuário autenticado sem permissão para a operação (HTTP 403).</summary>
    Forbidden = 3,

    /// <summary>Transição incompatível, repetida ou conflito de concorrência (HTTP 409).</summary>
    Conflict = 4,
}
