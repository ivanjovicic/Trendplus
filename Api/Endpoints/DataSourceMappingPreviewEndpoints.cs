using Api.Models;
using Api.Services.DataSources;

namespace Trendplus2.Endpoints;

public static class DataSourceMappingPreviewEndpoints
{
    public static void MapDataSourceMappingPreviewEndpoints(this WebApplication app)
    {
        app.MapPost("/api/data-sources/{profileName}/mapping-preview", async (
            string profileName,
            DataSourceMappingPreviewRequest request,
            IDataSourceMappingPreviewService service,
            HttpContext httpContext,
            IConfiguration configuration,
            CancellationToken ct = default) =>
        {
            var denial = AuthorizeAdmin(httpContext, configuration);
            if (denial is not null)
                return denial;

            try
            {
                var result = await service.PreviewAsync(profileName, request, ct);
                return Results.Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { category = "invalid_request", error = ex.Message });
            }
            catch (TimeoutException ex)
            {
                return Results.Problem(
                    title: "Source mapping preview timed out",
                    detail: ex.Message,
                    statusCode: StatusCodes.Status504GatewayTimeout,
                    extensions: new Dictionary<string, object?>
                    {
                        ["category"] = "timeout"
                    });
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return Results.StatusCode(499);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { category = "invalid_configuration", error = ex.Message });
            }
            catch (Exception ex)
            {
                return Results.Problem(
                    title: "Source mapping preview failed",
                    detail: ex.Message,
                    statusCode: StatusCodes.Status502BadGateway,
                    extensions: new Dictionary<string, object?>
                    {
                        ["category"] = "connection_failed"
                    });
            }
        })
        .RequireRateLimiting("db-heavy")
        .WithTags("Data Sources")
        .WithName("PreviewDataSourceMapping");
    }

    private static IResult? AuthorizeAdmin(HttpContext httpContext, IConfiguration configuration)
    {
        var access = AdminAccessControl.GetDecision(httpContext, configuration);
        if (access is AdminAccessDecision.MissingCredential)
            return Results.Unauthorized();
        if (access is AdminAccessDecision.Forbidden)
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        return null;
    }
}
