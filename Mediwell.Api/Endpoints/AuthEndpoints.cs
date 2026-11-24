using System.Security.Claims;
using Mediwell.Api.Data;
using Mediwell.Api.Models;
using Mediwell.Api.Repasitories;
using Mediwell.Api.Services;

namespace Mediwell.Api.Endpoints;

public static class AuthEndpoints
{
    public const string LoginRateLimit = "login";

    public static void MapAuthEndpoints(this WebApplication app)
    {
        var auth = app.MapGroup("/auth");

        // POST /auth/login — token olish
        auth.MapPost("/login", async (LoginRequest req, AuthService svc) =>
        {
            var user = await svc.CheckCredentialsAsync(req.Login, req.Password);
            if (user is null)
                return Results.Json(new { error = "Login yoki parol noto'g'ri" }, statusCode: StatusCodes.Status401Unauthorized);

            var (token, expiresAt) = svc.CreateToken(user);
            return Results.Ok(new { token, expiresAt, user = UserDto.From(user) });
        })
        .AllowAnonymous()
        .RequireRateLimiting(LoginRateLimit);

        // GET /auth/me — joriy foydalanuvchi
        auth.MapGet("/me", async (ClaimsPrincipal principal, UserRepository users) =>
        {
            var id = principal.UserId();
            var user = id is null ? null : await users.GetByIdAsync(id.Value);
            return user is null || !user.Active ? Results.Unauthorized() : Results.Ok(UserDto.From(user));
        });

        // POST /auth/change-password — o'z parolini almashtirish
        auth.MapPost("/change-password", async (ChangePasswordRequest req, ClaimsPrincipal principal,
            UserRepository users, AuthService svc) =>
        {
            var errors = req.Validate();
            if (errors.Count > 0) return Results.BadRequest(new { errors });

            var id = principal.UserId();
            var user = id is null ? null : await users.GetByIdAsync(id.Value);
            if (user is null || !user.Active) return Results.Unauthorized();
            if (!svc.VerifyPassword(user, req.CurrentPassword))
                return Results.BadRequest(new { errors = new[] { "Joriy parol noto'g'ri." } });

            await users.SetPasswordAsync(user.Id, svc.HashPassword(req.NewPassword));
            return Results.Ok(new { changed = true });
        });

        // ---------- Foydalanuvchilar (faqat admin) ----------
        var users = app.MapGroup("/users").RequireAuthorization(Roles.Admin);

        users.MapGet("/", async (UserRepository repo) =>
            Results.Ok((await repo.GetAllAsync()).Select(UserDto.From)));

        users.MapPost("/", async (SaveUserRequest req, UserRepository repo, AuthService svc) =>
        {
            var errors = req.Validate(isCreate: true);
            if (errors.Count > 0) return Results.BadRequest(new { errors });
            try
            {
                var id = await repo.CreateAsync(req.Login, req.FullName, req.Role, req.Active, svc.HashPassword(req.Password!));
                return Results.Created($"/users/{id}", new { id });
            }
            catch (DuplicateKeyException ex)
            {
                return Results.Conflict(new { error = ex.Message });
            }
        });

        users.MapPut("/{id:long}", async (long id, SaveUserRequest req, ClaimsPrincipal principal,
            UserRepository repo, AuthService svc) =>
        {
            var errors = req.Validate(isCreate: false).ToList();
            // Admin o'zini qulflab qo'ymasin
            if (principal.UserId() == id && (!req.Active || req.Role != Roles.Admin))
                errors.Add("O'zingizni faolsizlantirib yoki admin rolidan chiqarib bo'lmaydi.");
            if (errors.Count > 0) return Results.BadRequest(new { errors });

            try
            {
                var hash = string.IsNullOrEmpty(req.Password) ? null : svc.HashPassword(req.Password);
                return await repo.UpdateAsync(id, req.Login, req.FullName, req.Role, req.Active, hash)
                    ? Results.Ok(new { id })
                    : Results.NotFound(new { error = $"Foydalanuvchi topilmadi: id={id}" });
            }
            catch (DuplicateKeyException ex)
            {
                return Results.Conflict(new { error = ex.Message });
            }
        });
    }
}
