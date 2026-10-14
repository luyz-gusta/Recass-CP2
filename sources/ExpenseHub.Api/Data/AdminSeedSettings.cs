using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Configuration;

namespace ExpenseHub.Api.Data;

/// <summary>
/// Configuração da única conta criada pelo seed: o Admin inicial.
/// O e-mail fica no appsettings.json; a senha vem de user-secrets ou da variável de ambiente <c>AdminSeed__Password</c>,
/// nunca de arquivo versionado.
/// </summary>
internal sealed class AdminSeedSettings
{
    public const string SectionName = "AdminSeed";

    private AdminSeedSettings(string email, string password)
    {
        Email = email;
        Password = password;
    }

    public string Email { get; }

    public string Password { get; }

    /// <summary>
    /// Lê e valida a configuração. Devolve <see langword="null"/> e o motivo em <paramref name="problem"/> quando ela está incompleta.
    /// </summary>
    public static AdminSeedSettings? FromConfiguration(IConfiguration configuration, out string problem)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        IConfigurationSection section = configuration.GetSection(SectionName);
        string? email = section["Email"]?.Trim();
        string? password = section["Password"];

        if (string.IsNullOrWhiteSpace(email) || !new EmailAddressAttribute().IsValid(email))
        {
            problem = "a chave AdminSeed:Email está ausente ou não é um e-mail válido.";
            return null;
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            problem = "a senha do Admin (AdminSeed:Password) não foi configurada.";
            return null;
        }

        problem = string.Empty;
        return new AdminSeedSettings(email, password);
    }
}
