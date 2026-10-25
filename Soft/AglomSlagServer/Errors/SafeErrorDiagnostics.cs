using Npgsql;

namespace AglomSlagServer.Errors;

/// <summary>Diagnostics exclude exception messages, SQL, input data and stack traces.</summary>
internal static class SafeErrorDiagnostics
{
    internal static T? Find<T>(Exception? exception) where T : Exception
    {
        while (exception is not null)
        {
            if (exception is T match) return match;
            exception = exception.InnerException;
        }
        return null;
    }

    internal static string SqlState(Exception? exception)
    {
        var state = Find<PostgresException>(exception)?.SqlState;
        return state is { Length: 5 } && state.All(char.IsAsciiLetterOrDigit) ? state : "none";
    }
}
