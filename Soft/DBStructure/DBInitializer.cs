using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DBStructure
{
    public static class DBInitializer
    {
        public static async Task MigrateBasesAsync(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var contexts = GetAllDbContexts(scope.ServiceProvider);
            
            foreach (var context in contexts)
            {
                var contextName = context.GetType().Name;
                try
                {
                    // MigrateAsync сам создаст БД и применит миграции
                    await context.Database.MigrateAsync();
                    Console.WriteLine($"Миграции для базы данных {contextName} успешно применены.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка при миграции базы данных {contextName}: {ex.Message}");
                    throw;
                }
            }
        }

        public static async Task<bool> CheckAllDatabasesAsync(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var contexts = GetAllDbContexts(scope.ServiceProvider);
            var allAvailable = true;
            
            foreach (var context in contexts)
            {
                var contextName = context.GetType().Name;
                try
                {
                    if (!await context.Database.CanConnectAsync())
                    {
                        Console.WriteLine($"База данных {contextName} недоступна.");
                        allAvailable = false;
                    }
                    else
                    {
                        Console.WriteLine($"База данных {contextName} доступна.");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка при проверке базы данных {contextName}: {ex.Message}");
                    allAvailable = false;
                }
            }
            return allAvailable;
        }

        private static List<DbContext> GetAllDbContexts(IServiceProvider serviceProvider)
        {
            var contexts = new List<DbContext>();
            var dbContextTypes = serviceProvider.GetServices<DbContext>();
            foreach (var context in dbContextTypes)
            {
                contexts.Add(context);
            }
            return contexts;
        }
    }
}