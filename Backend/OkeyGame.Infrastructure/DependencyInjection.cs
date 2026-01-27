using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OkeyGame.Application.Interfaces;
using OkeyGame.Infrastructure.Persistence;
using OkeyGame.Infrastructure.Repositories;
using OkeyGame.Infrastructure.Services;
using StackExchange.Redis;

namespace OkeyGame.Infrastructure;

/// <summary>
/// Infrastructure katmanı DI kayıtları.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Infrastructure servislerini DI container'a ekler.
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="connectionString">SQL Server connection string</param>
    /// <param name="redisConnectionString">Redis connection string</param>
    /// <returns>Service collection</returns>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        string connectionString,
        string redisConnectionString)
    {
        // Entity Framework Core
        services.AddDbContext<OkeyGameDbContext>(options =>
        {
            options.UseSqlServer(connectionString, sqlOptions =>
            {
                sqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(30),
                    errorNumbersToAdd: null);
                
                sqlOptions.CommandTimeout(30);
            });
        });

        // Redis
        services.AddSingleton<IConnectionMultiplexer>(provider =>
        {
            var configuration = ConfigurationOptions.Parse(redisConnectionString);
            configuration.AbortOnConnectFail = false;
            configuration.ConnectRetry = 3;
            configuration.ConnectTimeout = 5000;
            return ConnectionMultiplexer.Connect(configuration);
        });

        // Unit of Work
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // Repositories
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IGameHistoryRepository, GameHistoryRepository>();
        services.AddScoped<IChipTransactionRepository, ChipTransactionRepository>();

        // Services
        services.AddScoped<IChipTransactionService, ChipTransactionService>();
        services.AddScoped<ILeaderboardService, RedisLeaderboardService>();
        services.AddSingleton<IEloCalculationService, EloCalculationService>();

        return services;
    }

    /// <summary>
    /// Veritabanı migration'larını uygular.
    /// </summary>
    public static async Task ApplyMigrationsAsync(this IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<OkeyGameDbContext>();
        await context.Database.MigrateAsync();
    }

    /// <summary>
    /// Leaderboard'u Redis'e senkronize eder.
    /// </summary>
    public static async Task SyncLeaderboardAsync(this IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var leaderboardService = scope.ServiceProvider.GetRequiredService<ILeaderboardService>();
        await leaderboardService.SyncFromDatabaseAsync();
    }
}
