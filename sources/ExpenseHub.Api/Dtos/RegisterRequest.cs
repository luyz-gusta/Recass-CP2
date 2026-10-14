using System.ComponentModel.DataAnnotations;

namespace ExpenseHub.Api.Dtos;

/// <summary>
/// Dados para <c>POST /register</c>. O cadastro não aceita roles: elas são atribuídas depois por um Admin.
/// </summary>
public sealed class RegisterRequest
{
    /// <summary>E-mail, usado também como nome de usuário.</summary>
    [Required]
    [EmailAddress]
    [StringLength(256)]
    public string Email { get; init; } = string.Empty;

    /// <summary>Senha com 8 a 100 caracteres, contendo letra maiúscula, letra minúscula, dígito e símbolo.</summary>
    [Required]
    [StringLength(100, MinimumLength = 8)]
    public string Password { get; init; } = string.Empty;
}
