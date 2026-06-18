using Api.Services;
using Infrastructure.Services;
using Trendplus2.Endpoints;

namespace Api.Endpoints;

public static class WorkerConfigurationEndpoints
{
    public static void MapWorkerConfigurationEndpoints(this WebApplication app)
    {
        app.MapGet("/api/workers/configuration", async (
            WorkerRegistryService registryService,
            CancellationToken ct = default) =>
        {
            var payload = await registryService.GetConfigurationAsync(ct);
            return Results.Ok(payload);
        })
        .WithName("GetWorkersConfiguration")
        .WithTags("Workers");

        app.MapPost("/api/workers/{workerName}/start", async (
            string workerName,
            HttpContext httpContext,
            IConfiguration configuration,
            WorkerConfigurationService workerConfigurationService,
            WorkerRuntimePolicyService runtimePolicyService,
            CancellationToken ct = default) =>
        {
            var access = AdminAccessControl.GetDecision(httpContext, configuration);
            if (access is AdminAccessDecision.MissingCredential)
                return Results.Unauthorized();
            if (access is AdminAccessDecision.Forbidden)
                return Results.StatusCode(StatusCodes.Status403Forbidden);

            var definition = WorkerRegistryCatalog.Find(workerName);
            if (definition is null)
                return Results.NotFound(new { error = $"Worker '{workerName}' nije pronađen." });

            if (!definition.IsRuntimeControllable)
            {
                return Results.BadRequest(new
                {
                    error = definition.RuntimeControlDisabledReason ?? "Ovaj worker ne podržava ručno pokretanje."
                });
            }

            var updatedBy = httpContext.User.Identity?.Name ?? "system";
            await workerConfigurationService.ResumeWorkerAsync(definition.WorkerName, updatedBy, ct);
            await runtimePolicyService.RequestManualRunAsync(definition.WorkerName, updatedBy, ct);
            return Results.Ok(new WorkerActionResponse
            {
                Success = true,
                Message = $"Worker {definition.WorkerName} je pokrenut."
            });
        })
        .WithName("StartWorker")
        .WithTags("Workers");

        app.MapPost("/api/workers/{workerName}/stop", async (
            string workerName,
            HttpContext httpContext,
            IConfiguration configuration,
            WorkerConfigurationService workerConfigurationService,
            CancellationToken ct = default) =>
        {
            var access = AdminAccessControl.GetDecision(httpContext, configuration);
            if (access is AdminAccessDecision.MissingCredential)
                return Results.Unauthorized();
            if (access is AdminAccessDecision.Forbidden)
                return Results.StatusCode(StatusCodes.Status403Forbidden);

            var definition = WorkerRegistryCatalog.Find(workerName);
            if (definition is null)
                return Results.NotFound(new { error = $"Worker '{workerName}' nije pronađen." });

            if (!definition.IsRuntimeControllable)
            {
                return Results.BadRequest(new
                {
                    error = definition.RuntimeControlDisabledReason ?? "Ovaj worker ne podržava ručno zaustavljanje."
                });
            }

            var updatedBy = httpContext.User.Identity?.Name ?? "system";
            await workerConfigurationService.StopWorkerAsync(definition.WorkerName, updatedBy, ct);
            return Results.Ok(new WorkerActionResponse
            {
                Success = true,
                Message = $"Worker {definition.WorkerName} je zaustavljen."
            });
        })
        .WithName("StopWorkerByName")
        .WithTags("Workers");

        app.MapPost("/api/workers/{workerName}/restart", async (
            string workerName,
            HttpContext httpContext,
            IConfiguration configuration,
            WorkerConfigurationService workerConfigurationService,
            WorkerRuntimePolicyService runtimePolicyService,
            CancellationToken ct = default) =>
        {
            var access = AdminAccessControl.GetDecision(httpContext, configuration);
            if (access is AdminAccessDecision.MissingCredential)
                return Results.Unauthorized();
            if (access is AdminAccessDecision.Forbidden)
                return Results.StatusCode(StatusCodes.Status403Forbidden);

            var definition = WorkerRegistryCatalog.Find(workerName);
            if (definition is null)
                return Results.NotFound(new { error = $"Worker '{workerName}' nije pronađen." });

            if (!definition.IsRuntimeControllable)
            {
                return Results.BadRequest(new
                {
                    error = definition.RuntimeControlDisabledReason ?? "Ovaj worker ne podržava restart."
                });
            }

            var updatedBy = httpContext.User.Identity?.Name ?? "system";
            await workerConfigurationService.StopWorkerAsync(definition.WorkerName, updatedBy, ct);
            await workerConfigurationService.ResumeWorkerAsync(definition.WorkerName, updatedBy, ct);
            await runtimePolicyService.RequestManualRunAsync(definition.WorkerName, updatedBy, ct);

            return Results.Ok(new WorkerActionResponse
            {
                Success = true,
                Message = $"Worker {definition.WorkerName} je restartovan."
            });
        })
        .WithName("RestartWorker")
        .WithTags("Workers");

        app.MapPost("/api/workers/{workerName}/schedule/enable", async (
            string workerName,
            HttpContext httpContext,
            IConfiguration configuration,
            WorkerConfigurationService workerConfigurationService,
            CancellationToken ct = default) =>
        {
            var access = AdminAccessControl.GetDecision(httpContext, configuration);
            if (access is AdminAccessDecision.MissingCredential)
                return Results.Unauthorized();
            if (access is AdminAccessDecision.Forbidden)
                return Results.StatusCode(StatusCodes.Status403Forbidden);

            var definition = WorkerRegistryCatalog.Find(workerName);
            if (definition is null)
                return Results.NotFound(new { error = $"Worker '{workerName}' nije pronađen." });

            if (!definition.IsScheduleControllable)
            {
                return Results.BadRequest(new
                {
                    error = definition.ScheduleControlDisabledReason ?? "Ovaj worker ne podržava raspored."
                });
            }

            var updatedBy = httpContext.User.Identity?.Name ?? "system";
            await workerConfigurationService.EnableScheduleAsync(definition.WorkerName, updatedBy, ct);
            return Results.Ok(new WorkerActionResponse
            {
                Success = true,
                Message = $"Raspored je omogućen za worker {definition.WorkerName}."
            });
        })
        .WithName("EnableWorkerScheduleByName")
        .WithTags("Workers");

        app.MapPost("/api/workers/{workerName}/schedule/disable", async (
            string workerName,
            HttpContext httpContext,
            IConfiguration configuration,
            WorkerConfigurationService workerConfigurationService,
            CancellationToken ct = default) =>
        {
            var access = AdminAccessControl.GetDecision(httpContext, configuration);
            if (access is AdminAccessDecision.MissingCredential)
                return Results.Unauthorized();
            if (access is AdminAccessDecision.Forbidden)
                return Results.StatusCode(StatusCodes.Status403Forbidden);

            var definition = WorkerRegistryCatalog.Find(workerName);
            if (definition is null)
                return Results.NotFound(new { error = $"Worker '{workerName}' nije pronađen." });

            if (!definition.IsScheduleControllable)
            {
                return Results.BadRequest(new
                {
                    error = definition.ScheduleControlDisabledReason ?? "Ovaj worker ne podržava raspored."
                });
            }

            var updatedBy = httpContext.User.Identity?.Name ?? "system";
            await workerConfigurationService.DisableScheduleAsync(definition.WorkerName, updatedBy, ct);
            return Results.Ok(new WorkerActionResponse
            {
                Success = true,
                Message = $"Raspored je onemogućen za worker {definition.WorkerName}."
            });
        })
        .WithName("DisableWorkerScheduleByName")
        .WithTags("Workers");
    }

}
