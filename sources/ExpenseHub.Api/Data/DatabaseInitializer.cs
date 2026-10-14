using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ExpenseHub.Api.Data;

/// <summary>
/// Cria ou atualiza o banco aplicando as migrations pendentes ao iniciar a API.
/// </summary>
internal static class DatabaseInitializer
{
    public static async Task InitializeAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        ExpenseHubDbContext context = scope.ServiceProvider.GetRequiredService<ExpenseHubDbContext>();
        await context.Database.MigrateAsync(cancellationToken);
    }
}
