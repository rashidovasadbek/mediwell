using System.Data;
using Npgsql;

namespace Mediwell.Api.Data;

public class DbConnectionFactory
{
    private readonly string _connectionString;
    public DbConnectionFactory(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("Default")
                            ?? throw new InvalidOperationException("Connection string 'Default' topilmadi.");
    }

    public IDbConnection Create()
    {
        return new NpgsqlConnection(_connectionString);
    }
}