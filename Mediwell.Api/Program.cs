using System.Net;
using System.Reflection;
using System.Threading.RateLimiting;
using Dapper;
using DbUp;
using Mediwell.Api.Data;
using Mediwell.Api.Endpoints;
using Mediwell.Api.Models;
using Mediwell.Api.Repasitories;
using Mediwell.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

DefaultTypeMap.MatchNamesWithUnderscores = true;
SqlMapper.AddTypeHandler(new DateOnlyTypeHandler());

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    // Swagger'da "Authorize" tugmasi — token bilan sinash uchun
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT", In = ParameterLocation.Header
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }]
            = Array.Empty<string>()
    });
});
builder.Services.AddSingleton<DbConnectionFactory>();
builder.Services.AddScoped<ProductRepository>();
builder.Services.AddScoped<ReceiptRepository>();
builder.Services.AddScoped<StockRepository>();
builder.Services.AddScoped<AggregateRepository>();
builder.Services.AddScoped<StockProductRepository>();
builder.Services.AddScoped<StockLedgerRepository>();
builder.Services.AddScoped<UserRepository>();
builder.Services.AddScoped<AuthService>();

// ---------- Auth (JWT) ----------
var authSection = builder.Configuration.GetSection("Auth");
builder.Services.Configure<AuthOptions>(authSection);
var authOptions = authSection.Get<AuthOptions>() ?? new AuthOptions();
if (System.Text.Encoding.UTF8.GetByteCount(authOptions.JwtKey) < 32)
    throw new InvalidOperationException("Auth:JwtKey sozlanmagan yoki 32 baytdan qisqa (appsettings.*.json).");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = authOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = authOptions.Issuer,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = AuthService.SigningKey(authOptions),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
        };
    });

builder.Services.AddAuthorization(o =>
{
    // Hamma endpoint default holatda login talab qiladi; ochiqlari .AllowAnonymous() bilan belgilanadi.
    o.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
    o.AddPolicy(Roles.Admin, p => p.RequireRole(Roles.Admin));
});

// Login'ni parol terib ko'rishdan himoya: IP bo'yicha daqiqasiga 10 urinish
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy(AuthEndpoints.LoginRateLimit, ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));
});

var connectionString = builder.Configuration.GetConnectionString("Default")
                       ?? throw new InvalidOperationException("Connection string 'Default' topilmadi.");
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.SetIsOriginAllowed(_ => true)  // faqat Development'da ishlatiladi (UseCors pastda)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// Migratsiya host qurilishidan oldin — baza tayyor bo'lmasa umuman ishga tushmaymiz.
var upgrader = DeployChanges.To
    .PostgresqlDatabase(connectionString)
    .WithScriptsEmbeddedInAssembly(Assembly.GetExecutingAssembly())
    .LogToConsole()
    .Build();

var result = upgrader.PerformUpgrade();

if (!result.Successful)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine("MIGRATION XATO: " + result.Error);
    Console.ResetColor();
    return 1;
}

Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine("Migration muvaffaqiyatli — baza yangilandi.");
Console.ResetColor();

var app = builder.Build();

// Nginx orqasida: haqiqiy mijoz IP'si (rate limit uchun) va sxema (https) — faqat localhost proxy'ga ishonamiz
var forwarded = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
};
forwarded.KnownNetworks.Clear();
forwarded.KnownProxies.Clear();
forwarded.KnownProxies.Add(IPAddress.Loopback);
forwarded.KnownProxies.Add(IPAddress.IPv6Loopback);
app.UseForwardedHeaders(forwarded);

// Login qila oladigan admin bo'lmasa — Auth:BootstrapAdmin dan yaratiladi
using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<AuthService>()
        .EnsureBootstrapAdminAsync(app.Logger);
}

// Development: Swagger, CORS (Vite boshqa portda), eski wwwroot sinov sahifasi.
// Production: SSL'ni Nginx qiladi (https redirect yo'q), panel va API bitta domenda (CORS kerak emas),
// wwwroot berilmaydi.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseCors();
    app.UseDefaultFiles();
    app.UseStaticFiles();
}

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

// GET /health — servis va baza holati (deploy.sh tekshiradi). Login talab qilmaydi.
app.MapGet("/health", async (DbConnectionFactory db) =>
{
    var dbOk = false;
    try
    {
        using var conn = db.Create();
        dbOk = await conn.ExecuteScalarAsync<int>("SELECT 1") == 1;
    }
    catch
    {
        // baza ulanmadi — dbOk = false
    }
    return Results.Json(new { status = dbOk ? "ok" : "error", db = dbOk },
        statusCode: dbOk ? StatusCodes.Status200OK : StatusCodes.Status503ServiceUnavailable);
}).AllowAnonymous();

app.MapAuthEndpoints();

app.MapProductEndpoints();
app.MapReceiptEndpoints();
app.MapStockEndpoints();
app.MapAggregateEndpoints();
app.MapStockLedgerEndpoints();

app.Run();
return 0;
