using System.Security.Claims;
using ClosedXML.Excel;
using Mediwell.Api.Data;
using Mediwell.Api.Exceptions;
using Mediwell.Api.Models;
using Mediwell.Api.Repasitories;
using Mediwell.Api.Services;

namespace Mediwell.Api.Endpoints;

// Sklad hisobi (markirovkasiz): guruhlar, dorilar, qoldiq, harakatlar
public static class StockLedgerEndpoints
{
    private static readonly string[] SuggestionFields = { "counterparty", "region", "agent" };

    public static void MapStockLedgerEndpoints(this WebApplication app)
    {
        MapGroups(app);
        MapProducts(app);
        MapBalance(app);
        MapMovements(app);
    }

    // Repository xatolarini HTTP javobga aylantirish
    private static async Task<IResult> Safe(Func<Task<IResult>> action)
    {
        try
        {
            return await action();
        }
        catch (NegativeStockException ex)
        {
            return Results.Conflict(new { error = ex.Message, errors = ex.Problems });
        }
        catch (DuplicateKeyException ex)
        {
            return Results.Conflict(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { errors = new[] { ex.Message } });
        }
    }

    // ---------------- /stock-groups ----------------
    private static void MapGroups(WebApplication app)
    {
        var g = app.MapGroup("/stock-groups").RequireAuthorization(Roles.Admin);

        g.MapGet("/", async (StockProductRepository repo) => Results.Ok(await repo.GetGroupsAsync()));

        g.MapPost("/", (SaveProductGroupRequest req, StockProductRepository repo) => Safe(async () =>
        {
            var errors = req.Validate();
            if (errors.Count > 0) return Results.BadRequest(new { errors });
            var id = await repo.CreateGroupAsync(req);
            return Results.Created($"/stock-groups/{id}", new { id });
        }));

        g.MapPut("/{id:long}", (long id, SaveProductGroupRequest req, StockProductRepository repo) => Safe(async () =>
        {
            var errors = req.Validate();
            if (errors.Count > 0) return Results.BadRequest(new { errors });
            return await repo.UpdateGroupAsync(id, req)
                ? Results.Ok(new { id })
                : Results.NotFound(new { error = $"Guruh topilmadi: id={id}" });
        }));
    }

    // ---------------- /stock-products ----------------
    private static void MapProducts(WebApplication app)
    {
        var g = app.MapGroup("/stock-products").RequireAuthorization(Roles.Admin);

        g.MapGet("/", async (long? groupId, bool? includeInactive, StockProductRepository repo) =>
            Results.Ok(await repo.GetProductsAsync(groupId, includeInactive ?? false)));

        g.MapPost("/", (SaveStockProductRequest req, StockProductRepository repo) => Safe(async () =>
        {
            var errors = req.Validate(isCreate: true);
            if (errors.Count > 0) return Results.BadRequest(new { errors });
            var id = await repo.CreateProductAsync(req);
            return Results.Created($"/stock-products/{id}", new { id });
        }));

        g.MapPut("/{id:long}", (long id, SaveStockProductRequest req, StockProductRepository repo) => Safe(async () =>
        {
            var errors = req.Validate(isCreate: false);
            if (errors.Count > 0) return Results.BadRequest(new { errors });
            return await repo.UpdateProductAsync(id, req)
                ? Results.Ok(new { id })
                : Results.NotFound(new { error = $"Dori topilmadi: id={id}" });
        }));

        g.MapDelete("/{id:long}", async (long id, StockProductRepository repo) =>
        {
            var result = await repo.DeleteProductAsync(id);
            return result switch
            {
                null => Results.NotFound(new { error = $"Dori topilmadi: id={id}" }),
                "deactivated" => Results.Ok(new
                {
                    deleted = false, deactivated = true,
                    message = "Dorining harakatlari bor — o'chirilmadi, faolsizlantirildi."
                }),
                _ => Results.Ok(new { deleted = true, deactivated = false, message = "Dori o'chirildi." })
            };
        });
    }

    // ---------------- /stock-balance ----------------
    private static void MapBalance(WebApplication app)
    {
        var g = app.MapGroup("/stock-balance").RequireAuthorization(Roles.Admin);

        g.MapGet("/", async (long? groupId, DateOnly? asOf, StockLedgerRepository repo) =>
        {
            if (groupId is null or <= 0)
                return Results.BadRequest(new { errors = new[] { "groupId ko'rsatilishi kerak." } });
            return Results.Ok(await repo.GetBalanceAsync(groupId.Value, asOf));
        });

        // Excel: Препарат | нал | mediwell | Жами + ЖАМИ qatori
        g.MapGet("/export", async (long? groupId, DateOnly? asOf, StockLedgerRepository repo) =>
        {
            if (groupId is null or <= 0)
                return Results.BadRequest(new { errors = new[] { "groupId ko'rsatilishi kerak." } });

            var group = await repo.GetGroupAsync(groupId.Value);
            if (group is null)
                return Results.NotFound(new { error = $"Guruh topilmadi: id={groupId}" });

            var rows = await repo.GetBalanceAsync(groupId.Value, asOf);
            var date = asOf ?? DateOnly.FromDateTime(DateTime.Today);

            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("остатка");

            ws.Cell(1, 1).Value = $"{group.Name} — qoldiq {date:dd.MM.yyyy} holatiga";
            ws.Range(1, 1, 1, 4).Merge().Style.Font.SetBold().Font.SetFontSize(13);

            string[] headers = { "Препарат", "нал", "mediwell", "Жами" };
            for (var c = 0; c < headers.Length; c++) ws.Cell(3, c + 1).Value = headers[c];
            var header = ws.Range(3, 1, 3, 4);
            header.Style.Font.SetBold().Fill.SetBackgroundColor(XLColor.LightGray);

            var r = 4;
            foreach (var row in rows)
            {
                ws.Cell(r, 1).Value = row.ProductName;
                ws.Cell(r, 2).Value = row.Nal;
                ws.Cell(r, 3).Value = row.Mediwell;
                ws.Cell(r, 4).Value = row.Total;
                r++;
            }

            ws.Cell(r, 1).Value = "ЖАМИ";
            ws.Cell(r, 2).Value = rows.Sum(x => x.Nal);
            ws.Cell(r, 3).Value = rows.Sum(x => x.Mediwell);
            ws.Cell(r, 4).Value = rows.Sum(x => x.Total);
            ws.Range(r, 1, r, 4).Style.Font.SetBold();

            var table = ws.Range(3, 1, r, 4);
            table.Style.Border.SetOutsideBorder(XLBorderStyleValues.Thin)
                 .Border.SetInsideBorder(XLBorderStyleValues.Thin);
            ws.Range(4, 2, r, 4).Style.NumberFormat.Format = "#,##0";
            ws.Range(3, 2, r, 4).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            ws.Columns(1, 4).AdjustToContents();

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return Results.File(ms.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"Qoldiq_{group.Code}_{date:yyyy-MM-dd}.xlsx");
        });
    }

    // ---------------- /stock-movements ----------------
    private static void MapMovements(WebApplication app)
    {
        var g = app.MapGroup("/stock-movements").RequireAuthorization(Roles.Admin);

        g.MapGet("/", async (long? groupId, long? productId, string? type, DateOnly? from, DateOnly? to,
            string? search, int? page, int? pageSize, StockLedgerRepository repo) =>
        {
            if (!string.IsNullOrWhiteSpace(type) && !StockRules.Types.Contains(type))
                return Results.BadRequest(new
                {
                    errors = new[] { $"type noto'g'ri: '{type}'. Ruxsat etilgan: {string.Join(", ", StockRules.Types)}." }
                });
            return Results.Ok(await repo.ListAsync(groupId, productId, type, from, to, search,
                page ?? 1, pageSize ?? 50));
        });

        g.MapGet("/suggestions", async (string? field, long? groupId, StockLedgerRepository repo) =>
        {
            if (field is null || !SuggestionFields.Contains(field))
                return Results.BadRequest(new { errors = new[] { "field: counterparty, region yoki agent bo'lishi kerak." } });
            return Results.Ok(await repo.GetSuggestionsAsync(field, groupId));
        });

        g.MapGet("/{id:long}", async (long id, StockLedgerRepository repo) =>
        {
            var m = await repo.GetByIdAsync(id);
            return m is null ? Results.NotFound(new { error = $"Harakat topilmadi: id={id}" }) : Results.Ok(m);
        });

        g.MapPost("/", (CreateStockMovementRequest req, ClaimsPrincipal user, StockLedgerRepository repo) => Safe(async () =>
        {
            var errors = req.Validate();
            if (errors.Count > 0) return Results.BadRequest(new { errors });
            var id = await repo.CreateAsync(req, user.UserId());
            return Results.Created($"/stock-movements/{id}", new { id });
        }));

        g.MapPost("/batch", (StockMovementBatchRequest req, ClaimsPrincipal user, StockLedgerRepository repo) => Safe(async () =>
        {
            var errors = req.Validate();
            if (errors.Count > 0) return Results.BadRequest(new { errors });
            var (docNo, ids) = await repo.CreateBatchAsync(req, user.UserId());
            return Results.Ok(new { docNo, ids, count = ids.Count });
        }));

        g.MapPost("/opening", (OpeningRequest req, ClaimsPrincipal user, StockLedgerRepository repo) => Safe(async () =>
        {
            var errors = req.Validate();
            if (errors.Count > 0) return Results.BadRequest(new { errors });
            var (docNo, inserted, replaced) = await repo.SaveOpeningAsync(req, user.UserId());
            return Results.Ok(new { docNo, inserted, replaced });
        }));

        g.MapPut("/{id:long}", (long id, UpdateStockMovementRequest req, StockLedgerRepository repo) => Safe(async () =>
        {
            var errors = req.Validate();
            if (errors.Count > 0) return Results.BadRequest(new { errors });
            return await repo.UpdateAsync(id, req)
                ? Results.Ok(await repo.GetByIdAsync(id))
                : Results.NotFound(new { error = $"Harakat topilmadi: id={id}" });
        }));

        g.MapDelete("/{id:long}", (long id, StockLedgerRepository repo) => Safe(async () =>
            await repo.DeleteAsync(id)
                ? Results.Ok(new { deleted = true })
                : Results.NotFound(new { error = $"Harakat topilmadi: id={id}" })));
    }
}
