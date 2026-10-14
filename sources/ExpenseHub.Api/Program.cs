using System;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using ExpenseHub.Api.Data;
using ExpenseHub.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ExpenseHub.Api;

internal static class Program
{
    public static async Task Main(string[] args)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

        builder.Services.AddOpenApi();
        builder.Services.AddProblemDetails();
        builder.Services
            .AddControllers()
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());

                // Campos desconhecidos (ownerId, status, roles...) são rejeitados com 400: sem mass assignment.
                options.JsonSerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;
            });

        builder.Services.AddDbContext<ExpenseHubDbContext>(options =>
            options.UseSqlite(builder.Configuration.GetConnectionString("ExpenseHub")));
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddScoped<IExpenseRepository, ExpenseRepository>();
        builder.Services.AddScoped<IAccountService, AccountService>();
        builder.Services.AddScoped<IUserAdminService, UserAdminService>();
        builder.Services.AddScoped<IExpenseService, ExpenseService>();
        builder.Services.AddScoped<IExpenseWorkflowService, ExpenseWorkflowService>();

        builder.Services
            .AddIdentityCore<IdentityUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
            })
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<ExpenseHubDbContext>()
            .AddSignInManager()
            .AddErrorDescriber<PortugueseIdentityErrorDescriber>();

        // Token bearer nativo do Identity: não depende de chave de assinatura configurada.
        builder.Services
            .AddAuthentication(IdentityConstants.BearerScheme)
            .AddBearerToken(IdentityConstants.BearerScheme);

        // Toda rota exige usuário autenticado, exceto as marcadas com [AllowAnonymous].
        builder.Services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build());

        WebApplication app = builder.Build();

        app.UseExceptionHandler();
        app.UseStatusCodePages();
        app.UseAuthentication();
        app.UseAuthorization();

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi().AllowAnonymous();
        }

        app.MapControllers();
        app.MapGet("/health", () => Results.Ok(new { status = "ok" }))
            .WithName("GetHealth")
            .AllowAnonymous();

        await DatabaseInitializer.InitializeAsync(app.Services, app.Lifetime.ApplicationStopping);
        await app.RunAsync();
    }
}
