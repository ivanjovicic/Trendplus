using System;
using System.IO;
using System.Text.Json;
using Xunit;

namespace Trendplus2.Tests;

internal static class GoldenSnapshotAssert
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = false
    };

    public static void Matches(string snapshotFileName, object actualProjection)
    {
        var snapshotPath = ResolveSnapshotPath(snapshotFileName);
        var expectedJson = File.ReadAllText(snapshotPath);
        var expected = NormalizeJson(expectedJson);

        var actualJson = JsonSerializer.Serialize(actualProjection, SerializerOptions);
        var actual = NormalizeJson(actualJson);

        Assert.Equal(expected, actual);
    }

    private static string ResolveSnapshotPath(string snapshotFileName)
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Golden"));
        var path = Path.Combine(root, snapshotFileName);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Golden snapshot not found: {path}", path);
        }

        return path;
    }

    private static string NormalizeJson(string json)
    {
        var document = JsonDocument.Parse(json);
        return JsonSerializer.Serialize(document.RootElement, SerializerOptions);
    }
}