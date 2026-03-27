using Api.Dtos;
using Api.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace Api.Endpoints
{
    public static class TransferEndpoints
    {
        public static void MapTransferEndpoints(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("/api/transfers").WithTags("Transfers");

            group.MapPost("/", CreateDraftAsync);
            group.MapPut("/{id:long}", UpdateDraftAsync);
            group.MapPost("/{id:long}/confirm", ConfirmAsync);
            group.MapPost("/{id:long}/complete", CompleteAsync);
            group.MapPost("/{id:long}/cancel", CancelAsync);
            group.MapGet("/", ListAsync);
            group.MapGet("/{id:long}", GetAsync);

            // Backward-compatible aliases used by older frontend builds.
            app.MapPost("/transfers", CreateDraftAsync);
            app.MapPut("/transfers/{id:long}", UpdateDraftAsync);
            app.MapPost("/transfers/{id:long}/confirm", ConfirmAsync);
            app.MapPost("/transfers/{id:long}/complete", CompleteAsync);
            app.MapPost("/transfers/{id:long}/cancel", CancelAsync);
            app.MapGet("/transfers", ListAsync);
            app.MapGet("/transfers/{id:long}", GetAsync);
        }

        private static async Task<IResult> CreateDraftAsync(
            TransferCreateRequest req,
            ITransferService svc,
            HttpContext ctx,
            ILoggerFactory loggerFactory,
            CancellationToken ct)
        {
            var logger = loggerFactory.CreateLogger("TransferEndpoints");
            try
            {
                var user = ctx.User?.Identity?.Name ?? "system";
                var result = await svc.CreateDraftAsync(req, user, ct);
                return Results.Created($"/api/transfers/{result.Id}", result);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Transfer draft creation failed.");
                return Results.Problem("Neuspesno kreiranje transfera.");
            }
        }

        private static async Task<IResult> UpdateDraftAsync(
            long id,
            TransferUpdateRequest req,
            ITransferService svc,
            HttpContext ctx,
            ILoggerFactory loggerFactory,
            CancellationToken ct)
        {
            var logger = loggerFactory.CreateLogger("TransferEndpoints");
            try
            {
                var user = ctx.User?.Identity?.Name ?? "system";
                var result = await svc.UpdateDraftAsync(id, req, user, ct);
                return Results.Ok(result);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Transfer draft update failed. TransferId={TransferId}", id);
                return Results.Problem("Neuspesan update transfera.");
            }
        }

        private static Task<IResult> ConfirmAsync(
            long id,
            ITransferService svc,
            HttpContext ctx,
            ILoggerFactory loggerFactory,
            CancellationToken ct)
            => ExecuteStateActionAsync(id, svc, ctx, loggerFactory, ct, (service, transferId, user, token) => service.ConfirmAsync(transferId, user, token));

        private static Task<IResult> CompleteAsync(
            long id,
            ITransferService svc,
            HttpContext ctx,
            ILoggerFactory loggerFactory,
            CancellationToken ct)
            => ExecuteStateActionAsync(id, svc, ctx, loggerFactory, ct, (service, transferId, user, token) => service.CompleteAsync(transferId, user, token));

        private static Task<IResult> CancelAsync(
            long id,
            ITransferService svc,
            HttpContext ctx,
            ILoggerFactory loggerFactory,
            CancellationToken ct)
            => ExecuteStateActionAsync(id, svc, ctx, loggerFactory, ct, (service, transferId, user, token) => service.CancelAsync(transferId, user, token));

        private static async Task<IResult> ExecuteStateActionAsync(
            long id,
            ITransferService svc,
            HttpContext ctx,
            ILoggerFactory loggerFactory,
            CancellationToken ct,
            Func<ITransferService, long, string, CancellationToken, Task<TransferResponse>> action)
        {
            var logger = loggerFactory.CreateLogger("TransferEndpoints");
            try
            {
                var user = ctx.User?.Identity?.Name ?? "system";
                var result = await action(svc, id, user, ct);
                return Results.Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Transfer action failed. TransferId={TransferId}", id);
                return Results.Problem("Neuspesna promena statusa transfera.");
            }
        }

        private static async Task<IResult> GetAsync(long id, ITransferService svc, CancellationToken ct)
        {
            var result = await svc.GetAsync(id, ct);
            return result is null ? Results.NotFound(new { error = $"Transfer {id} nije pronadjen." }) : Results.Ok(result);
        }

        private static async Task<IResult> ListAsync(
            ITransferService svc,
            int pageNumber = 1,
            int pageSize = 20,
            string? status = null,
            string? actor = null,
            string? createdBy = null,
            string? updatedBy = null,
            CancellationToken ct = default)
        {
            var result = await svc.ListAsync(pageNumber, pageSize, status, actor, createdBy, updatedBy, ct);
            return Results.Ok(result);
        }
    }
}
