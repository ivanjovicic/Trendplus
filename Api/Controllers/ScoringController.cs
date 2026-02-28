using Api.Models;
using Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Api.Controllers
{
    [ApiController]
    [Route("api/v1/scoring")]
    [EnableRateLimiting("api-v1")]
    [Produces("application/json")]
    public class ScoringController : ControllerBase
    {
        private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".webp"
        };

        private readonly IRuntimeScoringEngine _runtimeScoringEngine;
        private readonly ILogger<ScoringController> _logger;

        public ScoringController(
            IRuntimeScoringEngine runtimeScoringEngine,
            ILogger<ScoringController> logger)
        {
            _runtimeScoringEngine = runtimeScoringEngine;
            _logger = logger;
        }

        [HttpPost("evaluate")]
        [Consumes("multipart/form-data")]
        [ProducesResponseType(typeof(RuntimeScoringEvaluateResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> Evaluate(
            [FromForm] RuntimeScoringEvaluateRequest request,
            CancellationToken ct = default)
        {
            if (request.Image is null || request.Image.Length == 0)
            {
                return BadRequest(new { error = "Image is required." });
            }

            var extension = Path.GetExtension(request.Image.FileName);
            if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension))
            {
                return BadRequest(new { error = "Invalid image type. Allowed: jpg, jpeg, png, webp." });
            }

            if (request.Image.Length > 12 * 1024 * 1024)
            {
                return BadRequest(new { error = "Image is too large. Max size is 12MB." });
            }

            var tempDir = Path.Combine(Path.GetTempPath(), "trendplus-runtime-scoring");
            Directory.CreateDirectory(tempDir);

            var tempPath = Path.Combine(tempDir, $"{Guid.NewGuid():N}{extension}");
            try
            {
                await using (var fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await request.Image.CopyToAsync(fs, ct);
                }

                var input = new RuntimeScoringEngineInput(
                    ImagePath: tempPath,
                    Cost: request.Cost,
                    TargetPrice: request.TargetPrice,
                    Brand: request.Brand,
                    Category: request.Category,
                    Market: request.Market,
                    ArtikalId: request.ArtikalId,
                    DobavljacId: request.DobavljacId,
                    TipObuceId: request.TipObuceId,
                    SezonaId: request.SezonaId,
                    Velicina: request.Velicina,
                    Boja: request.Boja,
                    Materijal: request.Materijal);

                var response = await _runtimeScoringEngine.EvaluateAsync(input, ct);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Runtime scoring evaluate failed.");
                return Problem(
                    title: "Runtime scoring failed",
                    detail: ex.Message,
                    statusCode: StatusCodes.Status500InternalServerError);
            }
            finally
            {
                try
                {
                    if (System.IO.File.Exists(tempPath))
                    {
                        System.IO.File.Delete(tempPath);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to cleanup temp runtime scoring image.");
                }
            }
        }
    }
}
