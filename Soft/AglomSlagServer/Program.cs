using AglomCalculator.Services;
using AglomGraphQL.Api.Services;
using AglomSlagServer.Mutations;
using AglomSlagServer.Queries;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddScoped<AglomCalculationService>();
builder.Services.AddScoped<AglomCalculatorService>();

builder.Services
    .AddGraphQLServer()
    .AddQueryType<Query>()
    .AddMutationType<Mutation>();

var app = builder.Build();

app.MapGraphQL();

app.Run();