using System.ComponentModel.DataAnnotations;

namespace ExpenseHub.Api.Dtos;

/// <summary>
/// Credenciais para <c>POST /login</c>.
/// </summary>
public sealed class LoginRequest
{
    /// <summary>E-mail cadastrado.</summary>
    [Required]
    [EmailAddress]
    public string Email { get; init; } = string.Empty;

    /// <summary>Senha da conta.</summary>
    [Required]
    public string Password { get; init; } = string.Empty;
}
