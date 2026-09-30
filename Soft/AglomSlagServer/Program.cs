using AglomCalculator.Services;
using AglomGraphQL.Api.Services;
using AglomSlagServer.Mutations;
using AglomSlagServer.Queries;
using DBStructure.DbContexts;
using Microsoft.EntityFrameworkCore;


var builder = WebApplication.CreateBuilder(args);

#region Databases
builder.Services.AddDbContext<SlagModeContext>(opt =>
{
    var connectionString = builder.Configuration.GetConnectionString("SlagModeContext");
    opt.UseNpgsql(connectionString);
});

builder.Services.AddDbContext<AuthContext>(opt =>
{
    var connectionString = builder.Configuration.GetConnectionString("AuthContext");
    opt.UseNpgsql(connectionString);
});
#endregion

builder.Services.AddScoped<AglomCalculationService>();
builder.Services.AddScoped<AglomCalculatorService>();

builder.Services
    .AddGraphQLServer()
    .AddQueryType<Query>()
    .AddMutationType<Mutation>();

var app = builder.Build();

// Просто проверяем, что API может подключиться (опционально, для логов)
Console.WriteLine("\nЗапуск GraphQL API...");
Console.WriteLine("Проверка подключения к БД...");

try 
{
    using var scope = app.Services.CreateScope();
    var slagContext = scope.ServiceProvider.GetRequiredService<SlagModeContext>();
    var authContext = scope.ServiceProvider.GetRequiredService<AuthContext>();
    
    await slagContext.Database.CanConnectAsync();
    await authContext.Database.CanConnectAsync();
    Console.WriteLine("Подключение к базам данных успешно!\n");
}
catch (Exception ex)
{
    Console.WriteLine($"Ошибка подключения к БД: {ex.Message}\n");
}

app.MapGraphQL();
Console.WriteLine("GraphQL API запущен и слушает порт 8080");
app.Run();