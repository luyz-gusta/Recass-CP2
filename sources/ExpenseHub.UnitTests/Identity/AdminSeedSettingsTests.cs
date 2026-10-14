using System.Collections.Generic;
using ExpenseHub.Api.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExpenseHub.UnitTests.Identity;

/// <summary>
/// O seed só cria o Admin quando e-mail e senha estão configurados; a senha nunca vem do código-fonte.
/// </summary>
[TestClass]
public sealed class AdminSeedSettingsTests
{
    private const string EmailKey = "AdminSeed:Email";
    private const string CredentialKey = "AdminSeed:Password";

    private static readonly string _configuredCredential = new('x', 16);

    /// <summary>Com e-mail e senha configurados, o seed recebe os dois valores.</summary>
    [TestMethod]
    public void FromConfiguration_EmailAndCredential_ReturnsSettings()
    {
        IConfiguration configuration = Build(new() { [EmailKey] = " admin@expensehub.local ", [CredentialKey] = _configuredCredential });

        AdminSeedSettings? settings = AdminSeedSettings.FromConfiguration(configuration, out string problem);

        Assert.IsNotNull(settings);
        Assert.AreEqual("admin@expensehub.local", settings.Email);
        Assert.AreEqual(_configuredCredential, settings.Password);
        Assert.AreEqual(string.Empty, problem);
    }

    /// <summary>Sem senha configurada, o Admin não é criado e o motivo é informado.</summary>
    [TestMethod]
    public void FromConfiguration_MissingCredential_ReturnsNullWithReason()
    {
        IConfiguration configuration = Build(new() { [EmailKey] = "admin@expensehub.local" });

        AdminSeedSettings? settings = AdminSeedSettings.FromConfiguration(configuration, out string problem);

        Assert.IsNull(settings);
        Assert.Contains("AdminSeed:Password", problem);
    }

    /// <summary>Senha só com espaços conta como ausente.</summary>
    [TestMethod]
    public void FromConfiguration_BlankCredential_ReturnsNull()
    {
        IConfiguration configuration = Build(new() { [EmailKey] = "admin@expensehub.local", [CredentialKey] = "   " });

        Assert.IsNull(AdminSeedSettings.FromConfiguration(configuration, out _));
    }

    /// <summary>E-mail ausente ou inválido impede o seed do Admin.</summary>
    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("admin")]
    public void FromConfiguration_InvalidEmail_ReturnsNullWithReason(string? email)
    {
        IConfiguration configuration = Build(new() { [EmailKey] = email, [CredentialKey] = _configuredCredential });

        AdminSeedSettings? settings = AdminSeedSettings.FromConfiguration(configuration, out string problem);

        Assert.IsNull(settings);
        Assert.Contains("AdminSeed:Email", problem);
    }

    private static IConfiguration Build(Dictionary<string, string?> values)
    {
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }
}
