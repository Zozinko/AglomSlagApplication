using AglomCalculator.Services;
using AglomGraphQL.Api.Services;
using AglomSlagServer.Errors;
using AglomSlagServer.Mutations;
using AglomSlagServer.Queries;
using AglomSlagServer.Startup;
using DBStructure.DbContexts;
using Microsoft.EntityFrameworkCore;
using AglomSlagServer.Auth;
using DBStructure.Entities.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using HotChocolate.Execution;

var builder = WebApplication.CreateBuilder(args);

string slagConnection;
string authConnection;
AuthOptions authOptions;
try
{
    LocalEnvironment.Load(builder, args);
    slagConnection = DatabaseConfiguration.Connection(builder.Configuration, "SlagModeContext", "CONNECTION_SLAG", "SlagMode");
    authConnection = DatabaseConfiguration.Connection(builder.Configuration, "AuthContext", "CONNECTION_AUTH", "Auth");
    authOptions = EF.IsDesignTime ? new AuthOptions("design", "design", new string('x', 32), 300, 5, 7) : AuthOptions.Load(builder.Configuration);
}
catch (Exception exception) when (exception is InvalidOperationException or IOException or UnauthorizedAccessException)
{
    // Only our own validation messages are safe to print; never echo file contents/provider errors.
    Console.Error.WriteLine(exception is InvalidOperationException
        ? exception.Message : "Configuration error: local env file cannot be read.");
    Environment.ExitCode = 1;
    return;
}

// Provider failure logs can contain SQL, credentials or arbitrary exception messages.
// Application diagnostics use only public reason codes, types and SQLSTATE.
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.None);
builder.Logging.AddFilter("Npgsql", LogLevel.None);
builder.Logging.AddFilter("HotChocolate", LogLevel.None);
builder.Logging.AddFilter("Microsoft.AspNetCore.Diagnostics", LogLevel.None);
builder.Logging.AddFilter("Microsoft.Extensions.Hosting.Internal.Host", LogLevel.None);

builder.Services.AddPooledDbContextFactory<SlagModeContext>(opt =>
    // Creating/migrating a durable Docker volume may exceed the normal query timeout.
    opt.UseNpgsql(slagConnection, npgsql =>
    {
        if (EF.IsDesignTime) npgsql.CommandTimeout(180);
    })
        .EnableSensitiveDataLogging(false).EnableDetailedErrors(false), poolSize: 128);
builder.Services.AddPooledDbContextFactory<AuthContext>(opt =>
    opt.UseNpgsql(authConnection, npgsql =>
    {
        if (EF.IsDesignTime) npgsql.CommandTimeout(180);
    })
        .EnableSensitiveDataLogging(false).EnableDetailedErrors(false), poolSize: 128);

builder.Services.AddScoped<AglomCalculationService>();
builder.Services.AddScoped<AglomCalculatorService>();
builder.Services.AddScoped<SlagModeQueryService>();
builder.Services.AddScoped<SlagModeCalculationService>();
builder.Services.AddSingleton(authOptions);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.Configure<PasswordHasherOptions>(o =>
{
    o.CompatibilityMode = PasswordHasherCompatibilityMode.IdentityV3;
    o.IterationCount = CredentialService.IterationCount;
});
builder.Services.AddSingleton<LoginThrottle>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<CredentialService>();
builder.Services.AddScoped<PasswordChangeService>();
builder.Services.AddScoped<RefreshSessionService>();
builder.Services.AddScoped<CurrentActor>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
{
    o.MapInboundClaims = false;
    o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true, IssuerSigningKey = authOptions.Key,
        ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
        ValidateIssuer = true, ValidIssuer = authOptions.Issuer,
        ValidateAudience = true, ValidAudience = authOptions.Audience,
        ValidateLifetime = true, RequireExpirationTime = true, RequireSignedTokens = true,
        ClockSkew = TimeSpan.FromSeconds(authOptions.ClockSkewSeconds), NameClaimType = "sub"
    };
});
builder.Services.AddAuthorization();

builder.Services.AddSingleton<PublicErrorFilter>();

builder.Services.AddGraphQLServer()
    .AddHttpRequestInterceptor((context, executor, request, cancellationToken) =>
    {
        request.AllowIntrospection();
        return ValueTask.CompletedTask;
    })
    .AddAuthorization()
    .ModifyRequestOptions(opt => opt.IncludeExceptionDetails = builder.Environment.IsDevelopment())
    .AddErrorFilter<PublicErrorFilter>(services => services.GetRootServiceProvider().GetRequiredService<PublicErrorFilter>())
    .AddQueryType<Query>()
    .AddMutationType<Mutation>();

var app = builder.Build();

if (args.Contains("--export-schema", StringComparer.Ordinal))
{
    var executor = await app.Services.GetRequestExecutorAsync();
    Console.WriteLine(executor.Schema.ToString());
    return;
}

if (args.Any(a => a is "--bootstrap-user" or "--bootstrap-demo" or "--reset-password" or "--disable-user"))
{
    try
    {
        if (args.Contains("--bootstrap-demo", StringComparer.Ordinal)) await DemoUserBootstrap.RunAsync(app.Services, builder.Configuration);
        else if (args.Contains("--reset-password", StringComparer.Ordinal)) await PasswordMaintenance.RunAsync(app.Services, builder.Configuration);
        else if (args.Contains("--disable-user", StringComparer.Ordinal)) await PasswordMaintenance.DisableAsync(app.Services, builder.Configuration);
        else await UserBootstrap.RunAsync(app.Services, builder.Configuration);
    }
    catch (Exception) { app.Logger.LogError("Bootstrap failed; verify configuration, account state and database migrations."); Environment.ExitCode = 1; }
    return;
}

// EF tools build the host to resolve contexts; only the running API checks availability.
// Migrations remain exclusively the responsibility of the migrator/explicit EF CLI.
if (!EF.IsDesignTime && !await DatabaseStartupCheck.CheckAsync(app.Services, app.Logger))
{
    app.Logger.LogCritical("API startup aborted: required database unavailable or misconfigured");
    Environment.ExitCode = 1;
    return;
}

if (!EF.IsDesignTime && builder.Configuration.GetValue<bool>("DEMO_BOOTSTRAP"))
{
    try { await DemoUserBootstrap.RunAsync(app.Services, builder.Configuration); }
    catch (Exception)
    {
        app.Logger.LogError("Demo account setup failed; check DEMO_USER/ADMIN_EMAIL and PASSWORD in Soft/.env.");
        Environment.ExitCode = 1;
        return;
    }
}

app.Use(async (context, next) =>
{
    context.Response.Headers.CacheControl = "no-store";
    await next();
});
app.UseAuthentication();
app.UseAuthorization();
app.MapGet("/", () => Results.Redirect("/graphql"));
app.MapGraphQL().WithOptions(o => o.Tool.Enable = true);
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
