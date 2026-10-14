using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ExpenseHub.Api.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ExpenseHub.Api.Data;

/// <summary>
/// Executado ao iniciar a API: aplica as migrations pendentes e faz o seed idempotente
/// das roles e da única conta inicial, o Admin. Reexecutar não duplica nada.
/// </summary>
internal static class DatabaseInitializer
{
    public static async Task InitializeAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        IServiceProvider provider = scope.ServiceProvider;

        ExpenseHubDbContext context = provider.GetRequiredService<ExpenseHubDbContext>();
        await context.Database.MigrateAsync(cancellationToken);

        await EnsureRolesAsync(provider.GetRequiredService<RoleManager<IdentityRole>>());

        ILogger logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(DatabaseInitializer));
        AdminSeedSettings? settings = AdminSeedSettings.FromConfiguration(provider.GetRequiredService<IConfiguration>(), out string problem);
        if (settings is null)
        {
            logger.LogWarning(
                "Conta Admin não criada: {Problem} Configure a senha via user-secrets ou pela variável de ambiente AdminSeed__Password e reinicie a API (veja o README).",
                problem);
            return;
        }

        await EnsureAdminAsync(provider.GetRequiredService<UserManager<IdentityUser>>(), settings, logger);
    }

    private static async Task EnsureRolesAsync(RoleManager<IdentityRole> roleManager)
    {
        foreach (string role in AppRoles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                EnsureSucceeded(await roleManager.CreateAsync(new IdentityRole(role)), $"criar a role {role}");
            }
        }
    }

    private static async Task EnsureAdminAsync(UserManager<IdentityUser> userManager, AdminSeedSettings settings, ILogger logger)
    {
        IdentityUser? admin = await userManager.FindByEmailAsync(settings.Email);
        if (admin is null)
        {
            admin = new IdentityUser { UserName = settings.Email, Email = settings.Email, EmailConfirmed = true };
            EnsureSucceeded(await userManager.CreateAsync(admin, settings.Password), "criar a conta Admin");

            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("Conta Admin {Email} criada pelo seed.", settings.Email);
            }
        }

        if (!await userManager.IsInRoleAsync(admin, AppRoles.Admin))
        {
            EnsureSucceeded(await userManager.AddToRoleAsync(admin, AppRoles.Admin), "vincular a conta Admin à role Admin");
        }
    }

    private static void EnsureSucceeded(IdentityResult result, string operation)
    {
        if (!result.Succeeded)
        {
            string codes = string.Join(", ", result.Errors.Select(error => error.Code));
            throw new InvalidOperationException($"Seed: não foi possível {operation} ({codes}).");
        }
    }
}
