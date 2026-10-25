using System.Text.RegularExpressions;
using Path = System.IO.Path;

namespace AglomSlagServer.Startup;

// Deliberately small, literal dotenv dialect shared with the single-quoted Compose templates.
internal static partial class LocalEnvironment
{
    internal static void Load(WebApplicationBuilder builder, string[] args)
    {
        if (!builder.Environment.IsDevelopment()) return;
        var explicitPath = Environment.GetEnvironmentVariable("AGLOMSLAG_ENV_FILE");
        var path = explicitPath ?? Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath,
            "..", ".env"));
        if (!File.Exists(path))
        {
            if (explicitPath is not null)
                throw new InvalidOperationException("Configuration error: AGLOMSLAG_ENV_FILE does not exist.");
            return;
        }

        var values = new Dictionary<string, string>();
        var lineNumber = 0;
        foreach (var line in File.ReadLines(path))
        {
            lineNumber++;
            var text = line.Trim();
            if (text.Length == 0 || text.StartsWith('#')) continue;
            var separator = text.IndexOf('=');
            if (separator < 1) throw InvalidLine(lineNumber);
            var key = text[..separator].Trim();
            var value = text[(separator + 1)..].Trim();
            if (!EnvironmentKey().IsMatch(key) || !values.TryAdd(key, value)) throw InvalidLine(lineNumber);
            if (value.StartsWith('\'') || value.StartsWith('"'))
            {
                if (value.Length < 2 || value[^1] != value[0]) throw InvalidLine(lineNumber);
                values[key] = value[1..^1];
            }
        }
        // Parse the entire file before changing the environment. Existing process values win.
        foreach (var (key, value) in values)
            if (Environment.GetEnvironmentVariable(key) is null)
                Environment.SetEnvironmentVariable(key, value);
        builder.Configuration.AddEnvironmentVariables().AddCommandLine(args);
    }

    private static InvalidOperationException InvalidLine(int line) =>
        new($"Configuration error: invalid local env file at line {line}; use KEY='literal value', one per line.");

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]*$")]
    private static partial Regex EnvironmentKey();
}
