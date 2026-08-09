using DbUp;
using System.Reflection;
using Mediwell.Api.Data;
using Mediwell.Api.Endpoints;
using Mediwell.Api.Repasitories;
using Mediwell.Api.Endpoints;

Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;
var builder = WebApplication.CreateBuilder(args);


builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSingleton<DbConnectionFactory>();
builder.Services.AddScoped<ProductRepository>();
builder.Services.AddSwaggerGen();


var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
var connectionString = builder.Configuration.GetConnectionString("Default");

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
    return;
}

Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine("Migration muvaffaqiyatli — baza yangilandi.");
Console.ResetColor();
app.MapProductEndpoints();
app.UseHttpsRedirection();

Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;
app.Run();

