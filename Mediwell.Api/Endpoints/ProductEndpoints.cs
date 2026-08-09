using Mediwell.Api.Models;
using Mediwell.Api.Repasitories;

namespace Mediwell.Api.Endpoints;

public static class ProductEndpoints
{
    public static void MapProductEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/products");

        // GET /products — hamma preparatlar
        group.MapGet("/", async (ProductRepository repo) =>
        {
            var products = await repo.GetAllAsync();
            return Results.Ok(products);
        });

        // POST /products — yangi preparat qo'shish
        group.MapPost("/", async (Product product, ProductRepository repo) =>
        {
            var id = await repo.CreateAsync(product);
            return Results.Created($"/products/{id}", new { id });
        });
    }
}