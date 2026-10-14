using System.Globalization;
using Microsoft.AspNetCore.Identity;

namespace ExpenseHub.Api.Services;

/// <summary>
/// Mensagens de validação do Identity em português. Os códigos de erro continuam os originais.
/// </summary>
internal sealed class PortugueseIdentityErrorDescriber : IdentityErrorDescriber
{
    public override IdentityError DefaultError()
    {
        return Create(nameof(DefaultError), "Não foi possível concluir a operação.");
    }

    public override IdentityError DuplicateEmail(string email)
    {
        return Create(nameof(DuplicateEmail), $"O e-mail '{email}' já está cadastrado.");
    }

    public override IdentityError DuplicateUserName(string userName)
    {
        return Create(nameof(DuplicateUserName), $"O usuário '{userName}' já está cadastrado.");
    }

    public override IdentityError InvalidEmail(string? email)
    {
        return Create(nameof(InvalidEmail), $"O e-mail '{email}' é inválido.");
    }

    public override IdentityError InvalidUserName(string? userName)
    {
        return Create(nameof(InvalidUserName), $"O nome de usuário '{userName}' é inválido.");
    }

    public override IdentityError PasswordTooShort(int length)
    {
        return Create(nameof(PasswordTooShort), string.Format(CultureInfo.InvariantCulture, "A senha deve ter pelo menos {0} caracteres.", length));
    }

    public override IdentityError PasswordRequiresUniqueChars(int uniqueChars)
    {
        return Create(nameof(PasswordRequiresUniqueChars), string.Format(CultureInfo.InvariantCulture, "A senha deve ter pelo menos {0} caracteres diferentes.", uniqueChars));
    }

    public override IdentityError PasswordRequiresNonAlphanumeric()
    {
        return Create(nameof(PasswordRequiresNonAlphanumeric), "A senha deve ter pelo menos um símbolo (caractere não alfanumérico).");
    }

    public override IdentityError PasswordRequiresDigit()
    {
        return Create(nameof(PasswordRequiresDigit), "A senha deve ter pelo menos um dígito (0-9).");
    }

    public override IdentityError PasswordRequiresLower()
    {
        return Create(nameof(PasswordRequiresLower), "A senha deve ter pelo menos uma letra minúscula (a-z).");
    }

    public override IdentityError PasswordRequiresUpper()
    {
        return Create(nameof(PasswordRequiresUpper), "A senha deve ter pelo menos uma letra maiúscula (A-Z).");
    }

    private static IdentityError Create(string code, string description)
    {
        return new IdentityError { Code = code, Description = description };
    }
}
