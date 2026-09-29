using Mediwell.Api.Repasitories;

namespace Mediwell.Api.Endpoints;

public static class AggregateEndpoints
{
    public static void MapAggregateEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/aggregates");

        // GET /aggregates/by-receipt/{receiptId} — kirimning agregatlari
        group.MapGet("/by-receipt/{receiptId:long}", async (long receiptId, AggregateRepository repo) =>
        {
            var list = await repo.GetByReceiptAsync(receiptId);
            return Results.Ok(list);
        });

        // GET /aggregates/by-code/{code} — kod bo'yicha bitta agregat
        group.MapGet("/by-code/{code}", async (string code, AggregateRepository repo) =>
        {
            var agg = await repo.GetByCodeAsync(code);
            return agg is null ? Results.NotFound() : Results.Ok(agg);
        });
        
        group.MapPost("/{id:long}/arrive", async (long id, ArriveRequest req, AggregateRepository repo) =>
        {
            var ok = await repo.MarkArrivedAsync(id, req.ArrivalFirm, req.UserId);
            return ok ? Results.Ok(new { message = "Prixod qilindi" }) : Results.NotFound();
        });
        
        group.MapPost("/{id:long}/correct-count", async (long id, CorrectCountRequest req, AggregateRepository repo) =>
        {
            var ok = await repo.CorrectCountAsync(id, req.NewCount, req.UserId);
            return ok ? Results.Ok(new { message = "Son tuzatildi" }) : Results.NotFound();
        });
        
        // POST /aggregates/mediwell-arrive — skanerlangan kod naqddan rasmiyga (Mediwellga prixod)
        group.MapPost("/mediwell-arrive", async (MediwellArriveRequest req, AggregateRepository repo) =>
        {
            var (ok, message) = await repo.MediwellArriveAsync(req.Code, req.UserId);
            return ok ? Results.Ok(new { message }) : Results.BadRequest(new { error = message });
        });
        
        // GET /aggregates/codes?status=cash yoki official — markirovka kodlarni holat bo'yicha ko'rish
        group.MapGet("/codes", async (string? status, AggregateRepository repo) =>
        {
            var codes = await repo.GetCodesByStatusAsync(status);
            return Results.Ok(codes);
        });
        
        
    }
    
    public class ArriveRequest
    {
        public string ArrivalFirm { get; set; } = string.Empty;
        public long? UserId { get; set; }
    }
    
    public class CorrectCountRequest
    {
        public int NewCount { get; set; }
        public long? UserId { get; set; }
    }
    
    public class MediwellArriveRequest
    {
        public string Code { get; set; } = string.Empty;
        public long? UserId { get; set; }
    }
}