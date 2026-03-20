using System.Text.Json;

namespace Infrastructure.Services.Documents.Internal;

internal static class DocumentJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false,
        PropertyNameCaseInsensitive = true
    };
}
