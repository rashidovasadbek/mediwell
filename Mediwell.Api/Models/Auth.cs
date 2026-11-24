namespace Mediwell.Api.Models;

public static class Roles
{
    // 01_schema.sql dagi app_user.role CHECK bilan bir xil
    public const string Admin = "admin";
    public const string Warehouse = "warehouse"; // skladchi

    public static readonly IReadOnlyList<string> All =
        new[] { "admin", "accountant", "warehouse", "manager", "mp" };
}

public class AppUser
{
    public long Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string? Login { get; set; }
    public string? PasswordHash { get; set; }
    public bool Active { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
}

// Javobda parol xeshi qaytmaydi
public record UserDto(long Id, string? Login, string FullName, string Role, bool Active, DateTime? LastLoginAt)
{
    public static UserDto From(AppUser u) => new(u.Id, u.Login, u.FullName, u.Role, u.Active, u.LastLoginAt);
}

public class LoginRequest
{
    public string Login { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class ChangePasswordRequest
{
    public string CurrentPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        if (string.IsNullOrEmpty(CurrentPassword)) errors.Add("Joriy parol kiritilmagan.");
        if (NewPassword.Length < AuthOptions.MinPasswordLength)
            errors.Add($"Yangi parol kamida {AuthOptions.MinPasswordLength} belgi bo'lishi kerak.");
        return errors;
    }
}

// POST /users (yaratish) va PUT /users/{id} (Password bo'sh bo'lsa — o'zgarmaydi)
public class SaveUserRequest
{
    public string Login { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Role { get; set; } = Roles.Warehouse;
    public bool Active { get; set; } = true;
    public string? Password { get; set; }

    public IReadOnlyList<string> Validate(bool isCreate)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(Login)) errors.Add("Login bo'sh bo'lmasligi kerak.");
        else if (Login.Trim().Any(char.IsWhiteSpace)) errors.Add("Loginda bo'sh joy bo'lmasligi kerak.");
        if (string.IsNullOrWhiteSpace(FullName)) errors.Add("To'liq ism bo'sh bo'lmasligi kerak.");
        if (!Roles.All.Contains(Role))
            errors.Add($"Rol noto'g'ri: '{Role}'. Ruxsat etilgan: {string.Join(", ", Roles.All)}.");
        if (isCreate && string.IsNullOrEmpty(Password))
            errors.Add("Parol kiritilmagan.");
        if (!string.IsNullOrEmpty(Password) && Password.Length < AuthOptions.MinPasswordLength)
            errors.Add($"Parol kamida {AuthOptions.MinPasswordLength} belgi bo'lishi kerak.");
        return errors;
    }
}

// appsettings: "Auth": { "JwtKey", "Issuer", "TokenHours", "BootstrapAdmin": { Login, Password, FullName } }
public class AuthOptions
{
    public const int MinPasswordLength = 8;

    public string JwtKey { get; set; } = string.Empty;
    public string Issuer { get; set; } = "mediwell";
    public int TokenHours { get; set; } = 12;
    public BootstrapAdminOptions? BootstrapAdmin { get; set; }
}

public class BootstrapAdminOptions
{
    public string Login { get; set; } = "admin";
    public string Password { get; set; } = string.Empty;
    public string FullName { get; set; } = "Administrator";
}
