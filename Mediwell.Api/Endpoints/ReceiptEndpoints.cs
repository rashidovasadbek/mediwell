using Mediwell.Api.Data;
using Mediwell.Api.Models;
using Mediwell.Api.Repasitories;

namespace Mediwell.Api.Endpoints;

public static class ReceiptEndpoints
{
    public static void MapReceiptEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/receipts");

        // GET /receipts — hamma kirimlar
        group.MapGet("/", async (ReceiptRepository repo) =>
        {
            var receipts = await repo.GetAllAsync();
            return Results.Ok(receipts);
        });

        // GET /receipts/{id} — bitta kirim
        group.MapGet("/{id:long}", async (long id, ReceiptRepository repo) =>
        {
            var receipt = await repo.GetByIdAsync(id);
            return receipt is null
                ? Results.NotFound(new { error = $"Kirim topilmadi: id={id}" })
                : Results.Ok(receipt);
        });

        // POST /receipts — kirim + agregat kodlar
        group.MapPost("/", async (CreateReceiptRequest req, ReceiptRepository repo) =>
        {
            var errors = req.Validate();
            if (errors.Count > 0)
                return Results.BadRequest(new { errors });

            try
            {
                var id = await repo.CreateAsync(req);
                return Results.Created($"/receipts/{id}", new { receiptId = id, codesAdded = req.Codes.Count });
            }
            catch (DuplicateKeyException ex)
            {
                return Results.Conflict(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });
    }
}
