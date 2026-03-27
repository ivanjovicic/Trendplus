using System.Threading.Tasks;
using Api.Dtos;
using Api.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Api.Endpoints
{
    public static class TransferEndpoints
    {
        public static void MapTransferEndpoints(this IEndpointRouteBuilder app)
        {
            app.MapPost("/transfers", async (TransferCreateRequest req, ITransferService svc, HttpContext ctx) =>
            {
                var user = ctx.User?.Identity?.Name ?? "system";
                var res = await svc.CreateAsync(req, user);
                return Results.Created($"/transfers/{res.Id}", res);
            });

            app.MapGet("/transfers/{id}", async (long id, ITransferService svc) =>
            {
                var res = await svc.GetAsync(id);
                return res is null ? Results.NotFound() : Results.Ok(res);
            });

            app.MapGet("/transfers", () => Results.Ok(new { items = new object[0], total = 0 }));
        }
    }
}
