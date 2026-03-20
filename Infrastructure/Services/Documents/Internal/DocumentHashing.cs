using System.Security.Cryptography;

namespace Infrastructure.Services.Documents.Internal;

internal static class DocumentHashing
{
    public static async Task<string> ComputeSha256Async(string path, CancellationToken ct)
    {
        await using var stream = File.OpenRead(path);
        using var sha = SHA256.Create();
        var hash = await sha.ComputeHashAsync(stream, ct);
        return Convert.ToHexString(hash);
    }
}
