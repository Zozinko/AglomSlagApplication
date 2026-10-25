using AglomCalculator.Services;
using AglomGraphQL.Api.Services;
using AglomSlagServer.Errors;
using AglomSlagServer.Mutations;
using AglomSlagServer.Queries;
using AglomSlagServer.Startup;
using DBStructure.DbContexts;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Provider failure logs can contain SQL, credentials or arbitrary exception messages.
// Application diagnostics use only public reason codes, types and SQLSTATE.
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.None);
builder.Logging.AddFilter("Npgsql", LogLevel.None);
builder.Logging.AddFilter("HotChocolate", LogLevel.None);
builder.Logging.AddFilter("Microsoft.AspNetCore.Diagnostics", LogLevel.None);
builder.Logging.AddFilter("Microsoft.Extensions.Hosting.Internal.Host", LogLevel.None);

builder.Services.AddPooledDbContextFactory<SlagModeContext>(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("SlagModeContext"))
        .EnableSensitiveDataLogging(false).EnableDetailedErrors(false), poolSize: 128);
builder.Services.AddPooledDbContextFactory<AuthContext>(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("AuthContext"))
        .EnableSensitiveDataLogging(false).EnableDetailedErrors(false), poolSize: 128);

builder.Services.AddScoped<AglomCalculationService>();
builder.Services.AddScoped<AglomCalculatorService>();
builder.Services.AddScoped<SlagModeQueryService>();
builder.Services.AddScoped<SlagModeCalculationService>();

builder.Services.AddSingleton<PublicErrorFilter>();

builder.Services.AddGraphQLServer()
    .ModifyRequestOptions(opt => opt.IncludeExceptionDetails = builder.Environment.IsDevelopment())
    .AddErrorFilter<PublicErrorFilter>(services => services.GetRootServiceProvider().GetRequiredService<PublicErrorFilter>())
    .AddQueryType<Query>()
    .AddMutationType<Mutation>();

var app = builder.Build();

// EF tools build the host to resolve contexts; only the running API checks availability.
// Migrations remain exclusively the responsibility of the migrator/explicit EF CLI.
if (!EF.IsDesignTime && !await DatabaseStartupCheck.CheckAsync(app.Services, app.Logger))
{
    app.Logger.LogCritical("API startup aborted: required database unavailable or misconfigured");
    Environment.ExitCode = 1;
    return;
}

app.MapGraphQL();
app.Lifetime.ApplicationStarted.Register(() =>
{
    foreach (var address in app.Urls)
        app.Logger.LogInformation("GraphQL API listening at {Address}/graphql", address);
});
try
{
    await app.RunAsync();
}
catch (Exception exception) when (!EF.IsDesignTime)
{
    app.Logger.LogCritical("API stopped: startup or host failure; exception type {ExceptionType}; SQLSTATE {SqlState}",
        exception.GetType().Name, SafeErrorDiagnostics.SqlState(exception));
    Environment.ExitCode = 1;
}
