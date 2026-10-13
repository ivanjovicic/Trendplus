using System.Text.RegularExpressions;
using Xunit;

namespace Api.Tests;

/// <summary>
/// A PostgreSQL test that returns early when the fixture is unavailable reports a false
/// Passed locally (no Docker). Such tests must use PostgresRequiredFact/Theory so they
/// report Skipped locally and fail closed in CI.
/// </summary>
public sealed class PostgresRequiredAttributeConventionTests
{
    private static readonly Regex TestAttribute = new(@"^[ \t]*\[(Fact|Theory)\b", RegexOptions.Multiline);
    private static readonly Regex MemberDeclaration = new(@"\n[ \t]+(public|private|internal|protected)\s");

    [Fact]
    public void PostgresContainerFixtureTests_UsePostgresRequiredAttributes()
    {
        var root = FindRepositoryRoot();
        var violations = new List<string>();

        foreach (var path in Directory.GetFiles(Path.Combine(root, "Api.Tests"), "*.cs"))
        {
            var text = File.ReadAllText(path);
            if (Path.GetFileName(path) == nameof(PostgresRequiredAttributeConventionTests) + ".cs"
                || !text.Contains("IClassFixture<PostgresContainerFixture>", StringComparison.Ordinal))
            {
                continue;
            }

            var matches = TestAttribute.Matches(text);
            for (var i = 0; i < matches.Count; i++)
            {
                var start = matches[i].Index;
                var end = i + 1 < matches.Count ? matches[i + 1].Index : text.Length;
                var chunk = text[start..end];
                var declaration = MemberDeclaration.Match(chunk);
                if (!declaration.Success)
                {
                    continue;
                }

                var rest = chunk[(declaration.Index + 1)..];
                var next = MemberDeclaration.Match(rest);
                var body = next.Success ? rest[..next.Index] : rest;
                if (body.Contains("_fixture.IsAvailable", StringComparison.Ordinal)
                    || body.Contains("TryCreateDatabaseConnectionStringAsync", StringComparison.Ordinal))
                {
                    var line = text[..start].Count(c => c == '\n') + 1;
                    violations.Add($"{Path.GetFileName(path)}:{line}");
                }
            }
        }

        Assert.True(
            violations.Count == 0,
            "Use [PostgresRequiredFact]/[PostgresRequiredTheory] (not [Fact]/[Theory]) for tests that need PostgresContainerFixture: " + string.Join(", ", violations));
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Trendplus2.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Could not find repository root.");
    }
}
