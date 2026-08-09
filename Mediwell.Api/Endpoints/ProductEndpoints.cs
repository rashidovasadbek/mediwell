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

        // GET /products/{id} — bitta preparat
        group.MapGet("/{id:long}", async (long id, ProductRepository repo) =>
        {
            var product = await repo.GetByIdAsync(id);
            return product is null
                ? Results.NotFound(new { error = $"Preparat topilmadi: id={id}" })
                : Results.Ok(product);
        });

        // POST /products — yangi preparat qo'shish
        group.MapPost("/", async (CreateProductRequest req, ProductRepository repo) =>
        {
            var errors = req.Validate();
            if (errors.Count > 0)
                return Results.BadRequest(new { errors });

            var id = await repo.CreateAsync(req);
            return Results.Created($"/products/{id}", new { id });
        });
    }
}
