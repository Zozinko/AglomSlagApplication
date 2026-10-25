using AglomSlagServer.Errors;
using DBStructure.DbContexts;
using Microsoft.EntityFrameworkCore;

namespace AglomSlagServer.Startup;

internal static class DatabaseStartupCheck
{
    internal static async Task<bool> CheckAsync(IServiceProvider services, ILogger logger)
    {
        using var scope = services.CreateScope();
        // Do not short circuit: report both required databases.
        var slag = await CheckAsync<SlagModeContext>(scope.ServiceProvider, logger);
        var auth = await CheckAsync<AuthContext>(scope.ServiceProvider, logger);
        return slag && auth;
    }

    private static async Task<bool> CheckAsync<TContext>(IServiceProvider services, ILogger logger)
        where TContext : DbContext
    {
        var identifier = typeof(TContext).Name;
        try
        {
            var factory = services.GetRequiredService<IDbContextFactory<TContext>>();
            await using var context = await factory.CreateDbContextAsync();
            if (await context.Database.CanConnectAsync())
            {
                logger.LogInformation("Database check succeeded for {Database}", identifier);
                return true;
            }
            logger.LogError("Database check failed for {Database}: connection unavailable", identifier);
        }
        catch (Exception exception)
        {
            logger.LogError("Database check failed for {Database}: configuration or connection failure; exception type {ExceptionType}; SQLSTATE {SqlState}",
                identifier, exception.GetType().Name, SafeErrorDiagnostics.SqlState(exception));
        }
        return false;
    }
}
