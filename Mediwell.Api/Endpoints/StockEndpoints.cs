using Mediwell.Api.Repasitories;

namespace Mediwell.Api.Endpoints;

 static class StockEndpoints
{
    public static void MapStockEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/stock");

        // GET /stock — real-time qoldiq
        group.MapGet("/", async (StockRepository repo) =>
        {
            var stock = await repo.GetStockAsync();
            return Results.Ok(stock);
        });
    }
}