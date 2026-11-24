using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Mediwell.Api.Models;
using Mediwell.Api.Repasitories;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Mediwell.Api.Services;

public class AuthService
{
    private readonly AuthOptions _opt;
    private readonly UserRepository _users;
    private readonly PasswordHasher<AppUser> _hasher = new();

    public AuthService(IOptions<AuthOptions> opt, UserRepository users)
    {
        _opt = opt.Value;
        _users = users;
    }

    public string HashPassword(string password) => _hasher.HashPassword(null!, password);

    public bool VerifyPassword(AppUser user, string password)
    {
        if (string.IsNullOrEmpty(user.PasswordHash)) return false;
        var r = _hasher.VerifyHashedPassword(user, user.PasswordHash, password);
        return r != PasswordVerificationResult.Failed;
    }

    // null = login yoki parol noto'g'ri / foydalanuvchi faol emas
    public async Task<AppUser?> CheckCredentialsAsync(string login, string password)
    {
        if (string.IsNullOrWhiteSpace(login) || string.IsNullOrEmpty(password)) return null;
        var user = await _users.GetByLoginAsync(login);
        if (user is null || !user.Active || !VerifyPassword(user, password)) return null;
        await _users.TouchLoginAsync(user.Id);
        return user;
    }

    public (string Token, DateTime ExpiresAt) CreateToken(AppUser user)
    {
        var expires = DateTime.UtcNow.AddHours(_opt.TokenHours);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Login ?? string.Empty),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim("full_name", user.FullName),
        };
        var creds = new SigningCredentials(SigningKey(_opt), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(_opt.Issuer, _opt.Issuer, claims, expires: expires, signingCredentials: creds);
        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }

    public static SymmetricSecurityKey SigningKey(AuthOptions opt) => new(Encoding.UTF8.GetBytes(opt.JwtKey));

    // Login qilgan admin yo'q bo'lsa — konfiguratsiyadagi BootstrapAdmin yaratiladi (faqat bir marta).
    public async Task EnsureBootstrapAdminAsync(ILogger logger)
    {
        if (await _users.AnyActiveAdminWithLoginAsync()) return;

        var b = _opt.BootstrapAdmin;
        if (b is null || string.IsNullOrEmpty(b.Password))
        {
            logger.LogWarning("Tizimda login qila oladigan admin yo'q. Auth:BootstrapAdmin sozlamasini kiriting.");
            return;
        }
        if (b.Password.Length < AuthOptions.MinPasswordLength)
            throw new InvalidOperationException(
                $"Auth:BootstrapAdmin:Password kamida {AuthOptions.MinPasswordLength} belgi bo'lishi kerak.");

        var existing = await _users.GetByLoginAsync(b.Login);
        if (existing is not null)
        {
            // Login band, lekin u faol admin emas — tegmaymiz, qo'lda hal qilinsin.
            logger.LogWarning("Auth:BootstrapAdmin login '{Login}' allaqachon band (rol: {Role}, faol: {Active}).",
                b.Login, existing.Role, existing.Active);
            return;
        }

        await _users.CreateAsync(b.Login, b.FullName, Roles.Admin, true, HashPassword(b.Password));
        logger.LogWarning("Boshlang'ich admin yaratildi: '{Login}'. Kirgandan keyin parolni almashtiring " +
                          "va Auth:BootstrapAdmin:Password ni konfiguratsiyadan olib tashlang.", b.Login);
    }
}

public static class ClaimsPrincipalExtensions
{
    public static long? UserId(this ClaimsPrincipal user)
    {
        var sub = user.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
        return long.TryParse(sub, out var id) ? id : null;
    }
}
