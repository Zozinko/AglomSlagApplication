using DBStructure.DbContexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;

namespace DBStructure
{
    public static class DBInitializer
    {
       public static async Task InitializeBases(IServiceProvider serviceProvider)
       {
            using var scope = serviceProvider.CreateScope();
            var contexts = GetAllDbContexts(scope.ServiceProvider);

            foreach (var context in contexts)
            {
                var contextName = context.GetType().Name;
                try
                {
                    await context.Database.MigrateAsync();
                    Console.WriteLine($"База данных {contextName} мигрировала.");

                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка при миграции базы данных {contextName}: {ex.Message}");
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
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка при проверке базы данных {contextName}: {ex.Message}");
                }
            }
            return allAvailable;
        }

        public static async Task InitializeDatabases(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var contexts = GetAllDbContexts(scope.ServiceProvider);
            foreach (var context in contexts)
            {
                var contextName = context.GetType().Name;
                try
                {
                    await context.Database.EnsureCreatedAsync();
                    Console.WriteLine($"База данных {contextName} создана.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка при создании базы данных {contextName}: {ex.Message}");
                }
            }
        }

        private static List<DbContext> GetAllDbContexts(IServiceProvider serviceProvider)
        {
            var contexts = new List<DbContext>();
            // Получаем все зарегистрированные DbContext
            var dbContextTypes = serviceProvider.GetServices<DbContext>();
            foreach (var context in dbContextTypes)
            {
                contexts.Add(context);
            }
            return contexts;
        }
    }
}
