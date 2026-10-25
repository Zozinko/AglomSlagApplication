using Npgsql;

namespace AglomSlagServer.Startup;

internal static class DatabaseConfiguration
{
    internal static string Connection(IConfiguration configuration, string context, string alias, string database)
    {
        // Keep both historical API and migrator names, with the API name taking precedence.
        var supplied = configuration.GetConnectionString(context) ?? configuration[alias];
        NpgsqlConnectionStringBuilder connection;
        if (supplied is not null)
        {
            try { connection = new NpgsqlConnectionStringBuilder(supplied); }
            catch (Exception e) when (e is ArgumentException or FormatException)
            {
                throw new InvalidOperationException($"Configuration error: invalid connection string for {context}.");
            }
            if (string.IsNullOrWhiteSpace(connection.Host) || string.IsNullOrWhiteSpace(connection.Database)
                || string.IsNullOrWhiteSpace(connection.Username) || string.IsNullOrEmpty(connection.Password))
                throw new InvalidOperationException($"Configuration error: {context} requires Host, Database, Username and Password.");
        }
        else
        {
            var host = Required(configuration, "DB_HOST");
            var user = Required(configuration, "DB_USER");
            var password = Required(configuration, "DB_PASSWORD");
            if (!int.TryParse(Required(configuration, "DB_PORT"), out var port) || port is < 1 or > 65535)
                throw new InvalidOperationException("Configuration error: DB_PORT must be between 1 and 65535.");
            connection = new NpgsqlConnectionStringBuilder
            {
                Host = host, Port = port, Database = database, Username = user, Password = password
            };
        }
        return connection.ConnectionString;
    }

    private static string Required(IConfiguration configuration, string key)
    {
        var value = configuration[key];
        if (string.IsNullOrEmpty(value))
            throw new InvalidOperationException($"Configuration error: required variable {key} is missing or empty.");
        return value;
    }
}
