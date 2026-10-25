using HotChocolate.Execution;
using Npgsql;

namespace AglomSlagServer.Errors;

public sealed class PublicErrorFilter(ILogger<PublicErrorFilter> logger, IHostEnvironment environment) : IErrorFilter
{
    public IError OnError(IError error)
    {
        if (error.Exception is not { } exception) return error;
        var expected = SafeErrorDiagnostics.Find<ApiException>(exception);
        var postgres = SafeErrorDiagnostics.Find<PostgresException>(exception);
        var (code, message) = expected is not null
            ? (expected.Code, expected.Message)
            : postgres?.SqlState is PostgresErrorCodes.ForeignKeyViolation or PostgresErrorCodes.UniqueViolation
                ? (PublicErrorCodes.ReferenceConflict, "Операция конфликтует со связанными данными.")
                : (PublicErrorCodes.InternalError, "Внутренняя ошибка сервера.");

        logger.LogWarning("GraphQL request failed: {Code}; exception type {ExceptionType}; SQLSTATE {SqlState}",
            code, exception.GetType().Name, SafeErrorDiagnostics.SqlState(exception));

        // Build afresh: Development diagnostics must not copy raw messages or stack traces.
        var result = ErrorBuilder.New().SetMessage(message).SetCode(code);
        if (error.Path is not null) result.SetPath(error.Path);
        if (error.Locations is not null)
            foreach (var location in error.Locations) result.AddLocation(location);
        if (environment.IsDevelopment())
        {
            result.SetExtension("exceptionType", exception.GetType().Name);
            result.SetExtension("sqlState", SafeErrorDiagnostics.SqlState(exception));
        }
        return result.Build();
    }
}

