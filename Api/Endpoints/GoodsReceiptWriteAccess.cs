using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace Trendplus2.Endpoints;

/// <summary>
/// A receipt write is never authorized by the shared admin key alone.
/// Keys are provisioned per operator in server-side secret configuration, never in source control.
/// </summary>
internal static class GoodsReceiptWriteAccess
{
    public static IResult? Reject(HttpContext context, IConfiguration configuration, out string operatorId)
    {
        operatorId = string.Empty;
        // Fail closed until the business owner explicitly enables and provisions write access.
        if (configuration.GetValue<bool?>("GoodsReceipts:WritesEnabled") != true)
            return Results.StatusCode(StatusCodes.Status404NotFound);

        var id = context.Request.Headers["X-Receipt-Operator-Id"].FirstOrDefault();
        var key = context.Request.Headers["X-Receipt-Operator-Key"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(key))
            return Results.Unauthorized();

        // Only explicitly configured, stable opaque operator IDs may appear in audit records.
        if (id.Length > 80 || !id.All(ch => ch is >= 'a' and <= 'z' or >= '0' and <= '9' or '_' or '-'))
            return Results.StatusCode(StatusCodes.Status403Forbidden);

        var section = configuration.GetSection("GoodsReceipts:Operators").GetSection(id);
        var expected = section["Key"];
        var sharedAdminKey = configuration["Admin:ApiKey"];
        if (string.IsNullOrWhiteSpace(sharedAdminKey))
            sharedAdminKey = Environment.GetEnvironmentVariable("ADMIN_API_KEY");
        if (section.GetValue<bool?>("Enabled") != true ||
            string.IsNullOrEmpty(expected) || expected.Length < 32 ||
            string.IsNullOrEmpty(key) || key.Length < 32 ||
            string.Equals(expected, sharedAdminKey, StringComparison.Ordinal))
            return Results.StatusCode(StatusCodes.Status403Forbidden);

        if (!CryptographicOperations.FixedTimeEquals(
            SHA256.HashData(Encoding.UTF8.GetBytes(expected)),
            SHA256.HashData(Encoding.UTF8.GetBytes(key))))
            return Results.StatusCode(StatusCodes.Status403Forbidden);

        operatorId = id;
        return null;
    }
}
