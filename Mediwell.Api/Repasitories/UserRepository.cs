using Dapper;
using Mediwell.Api.Data;
using Mediwell.Api.Models;
using Npgsql;

namespace Mediwell.Api.Repasitories;

public class UserRepository
{
    private readonly DbConnectionFactory _db;

    public UserRepository(DbConnectionFactory db)
    {
        _db = db;
    }

    public async Task<AppUser?> GetByLoginAsync(string login)
    {
        using var conn = _db.Create();
        return await conn.QuerySingleOrDefaultAsync<AppUser>(
            "SELECT * FROM app_user WHERE lower(login) = lower(@login)", new { login = login.Trim() });
    }

    public async Task<AppUser?> GetByIdAsync(long id)
    {
        using var conn = _db.Create();
        return await conn.QuerySingleOrDefaultAsync<AppUser>("SELECT * FROM app_user WHERE id = @id", new { id });
    }

    public async Task<IEnumerable<AppUser>> GetAllAsync()
    {
        using var conn = _db.Create();
        return await conn.QueryAsync<AppUser>("SELECT * FROM app_user ORDER BY id");
    }

    public async Task<bool> AnyActiveAdminWithLoginAsync()
    {
        using var conn = _db.Create();
        return await conn.ExecuteScalarAsync<bool>(
            "SELECT EXISTS (SELECT 1 FROM app_user WHERE role = 'admin' AND active AND login IS NOT NULL)");
    }

    public async Task<long> CreateAsync(string login, string fullName, string role, bool active, string passwordHash)
    {
        using var conn = _db.Create();
        try
        {
            return await conn.ExecuteScalarAsync<long>(@"
                INSERT INTO app_user (login, full_name, role, active, password_hash)
                VALUES (@login, @fullName, @role, @active, @passwordHash)
                RETURNING id",
                new { login = login.Trim(), fullName = fullName.Trim(), role, active, passwordHash });
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            throw new DuplicateKeyException($"Bu login band: {login.Trim()}", ex);
        }
    }

    // passwordHash null bo'lsa — parol o'zgarmaydi
    public async Task<bool> UpdateAsync(long id, string login, string fullName, string role, bool active, string? passwordHash)
    {
        using var conn = _db.Create();
        try
        {
            var n = await conn.ExecuteAsync(@"
                UPDATE app_user
                   SET login = @login, full_name = @fullName, role = @role, active = @active,
                       password_hash = COALESCE(@passwordHash, password_hash)
                 WHERE id = @id",
                new { id, login = login.Trim(), fullName = fullName.Trim(), role, active, passwordHash });
            return n > 0;
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            throw new DuplicateKeyException($"Bu login band: {login.Trim()}", ex);
        }
    }

    public async Task SetPasswordAsync(long id, string passwordHash)
    {
        using var conn = _db.Create();
        await conn.ExecuteAsync("UPDATE app_user SET password_hash = @passwordHash WHERE id = @id",
            new { id, passwordHash });
    }

    public async Task TouchLoginAsync(long id)
    {
        using var conn = _db.Create();
        await conn.ExecuteAsync("UPDATE app_user SET last_login_at = now() WHERE id = @id", new { id });
    }
}
