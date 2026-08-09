using System.Reflection;
using Dapper;
using DbUp;
using Mediwell.Api.Data;
using Mediwell.Api.Endpoints;
using Mediwell.Api.Repasitories;

DefaultTypeMap.MatchNamesWithUnderscores = true;
SqlMapper.AddTypeHandler(new DateOnlyTypeHandler());

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton<DbConnectionFactory>();
builder.Services.AddScoped<ProductRepository>();
builder.Services.AddScoped<ReceiptRepository>();
builder.Services.AddScoped<StockRepository>();

var connectionString = builder.Configuration.GetConnectionString("Default")
                       ?? throw new InvalidOperationException("Connection string 'Default' topilmadi.");

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

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapProductEndpoints();
app.MapReceiptEndpoints();
app.MapStockEndpoints();

app.Run();
return 0;
