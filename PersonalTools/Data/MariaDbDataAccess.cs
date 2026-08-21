using System.Data;
using MySqlConnector;

namespace PersonalTools.Data;

public interface IMariaDbDataAccess
{
    Task<T?> GetDataSP<T>(string procedureName, Func<MySqlDataReader, T> map, IEnumerable<MySqlParameter>? parameters = null, CancellationToken cancellationToken = default);
    Task<List<T>> GetBulkDataSP<T>(string procedureName, Func<MySqlDataReader, T> map, IEnumerable<MySqlParameter>? parameters = null, CancellationToken cancellationToken = default);
    Task<int> ExecuteSP(string procedureName, IEnumerable<MySqlParameter>? parameters = null, CancellationToken cancellationToken = default);
    Task<T> GetScalarSP<T>(string procedureName, IEnumerable<MySqlParameter>? parameters = null, CancellationToken cancellationToken = default);
}

public sealed class MariaDbDataAccess : IMariaDbDataAccess
{
    private readonly string _connectionString;
    private readonly ILogger<MariaDbDataAccess> _logger;

    public MariaDbDataAccess(IConfiguration configuration, ILogger<MariaDbDataAccess> logger)
    {
        _connectionString = configuration.GetConnectionString("PersonalTools") ?? string.Empty;
        _logger = logger;
    }

    public async Task<T?> GetDataSP<T>(string procedureName, Func<MySqlDataReader, T> map, IEnumerable<MySqlParameter>? parameters = null, CancellationToken cancellationToken = default)
    {
        await using MySqlConnection connection = OpenConnection();
        await connection.OpenAsync(cancellationToken);
        await using MySqlCommand command = CreateCommand(connection, procedureName, parameters);
        await using MySqlDataReader reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? map(reader) : default;
    }

    public async Task<List<T>> GetBulkDataSP<T>(string procedureName, Func<MySqlDataReader, T> map, IEnumerable<MySqlParameter>? parameters = null, CancellationToken cancellationToken = default)
    {
        List<T> results = new();
        await using MySqlConnection connection = OpenConnection();
        await connection.OpenAsync(cancellationToken);
        await using MySqlCommand command = CreateCommand(connection, procedureName, parameters);
        await using MySqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) results.Add(map(reader));
        return results;
    }

    public async Task<int> ExecuteSP(string procedureName, IEnumerable<MySqlParameter>? parameters = null, CancellationToken cancellationToken = default)
    {
        await using MySqlConnection connection = OpenConnection();
        await connection.OpenAsync(cancellationToken);
        await using MySqlCommand command = CreateCommand(connection, procedureName, parameters);
        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<T> GetScalarSP<T>(string procedureName, IEnumerable<MySqlParameter>? parameters = null, CancellationToken cancellationToken = default)
    {
        await using MySqlConnection connection = OpenConnection();
        await connection.OpenAsync(cancellationToken);
        await using MySqlCommand command = CreateCommand(connection, procedureName, parameters);
        object? value = await command.ExecuteScalarAsync(cancellationToken);
        if (value is null || value == DBNull.Value) throw new InvalidOperationException($"Stored procedure '{procedureName}' returned no value.");
        return (T)Convert.ChangeType(value, typeof(T));
    }

    private MySqlConnection OpenConnection()
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            _logger.LogError("The PersonalTools MariaDB connection string has not been configured.");
            throw new InvalidOperationException("MariaDB is not configured. Set ConnectionStrings__PersonalTools in the server environment or use .NET User Secrets locally.");
        }

        return new MySqlConnection(_connectionString);
    }

    private static MySqlCommand CreateCommand(MySqlConnection connection, string procedureName, IEnumerable<MySqlParameter>? parameters)
    {
        if (string.IsNullOrWhiteSpace(procedureName)) throw new ArgumentException("A stored procedure name is required.", nameof(procedureName));
        MySqlCommand command = new(procedureName, connection) { CommandType = CommandType.StoredProcedure, CommandTimeout = 30 };
        if (parameters is not null) command.Parameters.AddRange(parameters.ToArray());
        return command;
    }
}
