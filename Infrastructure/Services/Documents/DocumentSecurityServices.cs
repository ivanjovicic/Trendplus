using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Data;
using Application.Documents.Interfaces;
using Application.Documents.Models;
using Domain.Model.Documents;
using Infrastructure.Configuration;
using Infrastructure.DbContexts;
using Infrastructure.Services.Documents.Internal;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using Npgsql;

namespace Infrastructure.Services.Documents;

public sealed class DocumentUserContextAccessor : IDocumentUserContextAccessor
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public DocumentUserContextAccessor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public DocumentExecutionContext GetCurrent()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        var user = httpContext?.User;
        var userId = user?.FindFirst("sub")?.Value
            ?? user?.FindFirst("user_id")?.Value
            ?? httpContext?.Request.Headers["X-User-Id"].FirstOrDefault()
            ?? "anonymous";
        var userName = user?.Identity?.Name
            ?? httpContext?.Request.Headers["X-User-Name"].FirstOrDefault()
            ?? userId;
        var roles = user?.FindAll("role").Select(claim => claim.Value).ToArray()
            ?? httpContext?.Request.Headers["X-User-Roles"].ToString().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            ?? new[] { "AnalyticsExport" };

        return new DocumentExecutionContext
        {
            UserId = userId,
            UserName = userName,
            Roles = roles.Length == 0 ? new[] { "AnalyticsExport" } : roles,
            CorrelationId = httpContext?.TraceIdentifier,
            IpAddress = httpContext?.Connection.RemoteIpAddress?.ToString(),
            UserAgent = httpContext?.Request.Headers.UserAgent.ToString()
        };
    }
}

public sealed class DocumentAccessControlService : IDocumentAccessControlService
{
    private static readonly HashSet<string> ExportRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        "Admin",
        "AnalyticsExport",
        "AnalyticsViewer"
    };

    private static readonly HashSet<string> ElevatedRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        "Admin",
        "AnalyticsAdmin",
        "DocumentAdmin"
    };

    public void EnsureCanGenerate(DocumentExecutionContext executionContext)
    {
        if (executionContext.Roles.Any(role => ExportRoles.Contains(role)))
        {
            return;
        }

        throw new UnauthorizedAccessException("User does not have export permissions.");
    }

    public void EnsureCanAccess(DocumentRecord document, DocumentExecutionContext executionContext)
    {
        if (document.RequestedByUserId == executionContext.UserId || CanBypassOwnership(executionContext))
        {
            return;
        }

        throw new UnauthorizedAccessException("User cannot access this document.");
    }

    public bool CanBypassOwnership(DocumentExecutionContext executionContext)
    {
        return executionContext.Roles.Any(role => ElevatedRoles.Contains(role));
    }
}

public sealed class DocumentDownloadTokenService : IDocumentDownloadTokenService
{
    private readonly byte[] _signingKeyBytes;
    private readonly ILogger<DocumentDownloadTokenService> _logger;

    public DocumentDownloadTokenService(
        Microsoft.Extensions.Options.IOptions<DocumentExportOptions> options,
        IHostEnvironment env,
        ILogger<DocumentDownloadTokenService> logger)
    {
        _logger = logger;
        var signingKey = options.Value.ResolveSigningKey();
        if (string.IsNullOrWhiteSpace(signingKey))
        {
            if (env.IsDevelopment())
            {
                // Generate an ephemeral signing key for local development to avoid startup failure.
                signingKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
                _logger.LogWarning("Document signing key is not configured. Generated ephemeral signing key for development environment. Tokens will not be valid across restarts.");
            }
            else
            {
                throw new InvalidOperationException("Document signing key is not configured.");
            }
        }

        _signingKeyBytes = Encoding.UTF8.GetBytes(signingKey);
    }

    public string Create(Guid documentId, DateTime expiresAtUtc)
    {
        var payload = JsonSerializer.Serialize(new TokenPayload(documentId, expiresAtUtc), DocumentJson.Options);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        using var hmac = new HMACSHA256(_signingKeyBytes);
        var signature = hmac.ComputeHash(payloadBytes);
        return $"{Base64UrlEncode(payloadBytes)}.{Base64UrlEncode(signature)}";
    }

    public bool TryValidate(Guid documentId, string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        var parts = token.Split('.', 2);
        if (parts.Length != 2)
        {
            return false;
        }

        byte[] payloadBytes;
        byte[] signatureBytes;

        try
        {
            payloadBytes = Base64UrlDecode(parts[0]);
            signatureBytes = Base64UrlDecode(parts[1]);
        }
        catch (FormatException)
        {
            return false;
        }

        using var hmac = new HMACSHA256(_signingKeyBytes);
        var expected = hmac.ComputeHash(payloadBytes);
        if (!CryptographicOperations.FixedTimeEquals(expected, signatureBytes))
        {
            return false;
        }

        try
        {
            var payload = JsonSerializer.Deserialize<TokenPayload>(payloadBytes, DocumentJson.Options);
            return payload is not null
                && payload.DocumentId == documentId
                && payload.ExpiresAtUtc > DateTime.UtcNow;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string Base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static byte[] Base64UrlDecode(string value)
    {
        var normalized = value.Replace('-', '+').Replace('_', '/');
        normalized = normalized.PadRight(normalized.Length + (4 - normalized.Length % 4) % 4, '=');
        return Convert.FromBase64String(normalized);
    }

    private sealed record TokenPayload(Guid DocumentId, DateTime ExpiresAtUtc);
}

public sealed class DocumentAuditService : IDocumentAuditService
{
    private readonly TrendplusDbContext _db;

    public DocumentAuditService(TrendplusDbContext db)
    {
        _db = db;
    }

    public async Task WriteAsync(
        Guid documentId,
        string action,
        DocumentExecutionContext executionContext,
        object? details,
        CancellationToken ct = default)
    {
        _db.DocumentAudits.Add(new DocumentAudit
        {
            DocumentId = documentId,
            Action = action,
            UserId = executionContext.UserId,
            UserName = executionContext.UserName,
            Roles = string.Join(",", executionContext.Roles),
            IpAddress = executionContext.IpAddress,
            UserAgent = executionContext.UserAgent,
            DetailsJson = details is null ? null : JsonSerializer.Serialize(details, DocumentJson.Options),
            CreatedAtUtc = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(ct);
    }
}

public sealed class DocumentQueueStore : IDocumentQueueStore
{
    private readonly TrendplusDbContext _db;
    private readonly Microsoft.Extensions.Logging.ILogger<DocumentQueueStore> _logger;

    public DocumentQueueStore(
        TrendplusDbContext db,
        Microsoft.Extensions.Logging.ILogger<DocumentQueueStore> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<IReadOnlyList<Guid>> ClaimNextQueuedAsync(int batchSize, CancellationToken ct = default)
    {
        const int maxAttempts = 3;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                var connection = _db.GetDbConnection();
                if (connection.State != ConnectionState.Open)
                {
                    await connection.OpenAsync(ct);
                }

                await using (var existsCommand = connection.CreateCommand())
                {
                    existsCommand.CommandText = "SELECT to_regclass('public.\"Documents\"') IS NOT NULL;";
                    var exists = (bool?)await existsCommand.ExecuteScalarAsync(ct) ?? false;
                    if (!exists)
                    {
                        _logger.LogWarning("Document queue table is not available yet. Claim attempt skipped.");
                        return Array.Empty<Guid>();
                    }
                }

                await using var transaction = await connection.BeginTransactionAsync(ct);
                try
                {
                    await using var command = connection.CreateCommand();
                    command.Transaction = transaction;
                    command.CommandText = """
                        WITH next_jobs AS (
                            SELECT "Id"
                            FROM "Documents"
                            WHERE "Status" = 'queued'
                              AND ("NextAttemptAtUtc" IS NULL OR "NextAttemptAtUtc" <= NOW())
                            ORDER BY "CreatedAtUtc"
                            LIMIT @batchSize
                            FOR UPDATE SKIP LOCKED
                        )
                        UPDATE "Documents" AS d
                        SET "Status" = 'processing',
                            "StartedAtUtc" = NOW(),
                            "UpdatedAtUtc" = NOW()
                        FROM next_jobs
                        WHERE d."Id" = next_jobs."Id"
                        RETURNING d."Id";
                        """;

                    var parameter = command.CreateParameter();
                    parameter.ParameterName = "batchSize";
                    parameter.Value = batchSize;
                    command.Parameters.Add(parameter);

                    var claimed = new List<Guid>();
                    await using (var reader = await command.ExecuteReaderAsync(ct))
                    {
                        while (await reader.ReadAsync(ct))
                        {
                            claimed.Add(reader.GetGuid(0));
                        }
                    }

                    await transaction.CommitAsync(ct);
                    if (claimed.Count > 0)
                    {
                        _logger.LogInformation("Claimed {ClaimedCount} queued document jobs.", claimed.Count);
                    }

                    return claimed;
                }
                catch (PostgresException ex) when (ex.SqlState == "42P01")
                {
                    await transaction.RollbackAsync(ct);
                    _logger.LogWarning(ex, "Document queue table missing during claim. Returning empty batch.");
                    return Array.Empty<Guid>();
                }
                catch (Exception ex) when (attempt < maxAttempts)
                {
                    await transaction.RollbackAsync(ct);
                    _logger.LogWarning(ex, "Failed to claim queued documents on attempt {Attempt}/{MaxAttempts}. Retrying.", attempt, maxAttempts);
                    await Task.Delay(TimeSpan.FromMilliseconds(250 * attempt), ct);
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync(ct);
                    _logger.LogError(ex, "Failed to claim queued documents after {Attempts} attempts.", attempt);
                    throw new InvalidOperationException("Failed to claim queued document jobs.", ex);
                }
            }
            catch (PostgresException ex) when (ex.SqlState == "42P01")
            {
                _logger.LogWarning(ex, "Document queue claim skipped because table Documents does not exist.");
                return Array.Empty<Guid>();
            }
        }

        return Array.Empty<Guid>();
    }
}
