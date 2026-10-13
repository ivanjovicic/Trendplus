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
    private static readonly Regex TestAttribute = new(
        @"^[ \t]*\[\s*(?:(?:global::)?Xunit\.)?(?:Fact|Theory)(?:Attribute)?\b",
        RegexOptions.Multiline);
    private static readonly Regex MemberDeclaration = new(
        @"\n[ \t]+(?:public|private|internal|protected)\s+(?:(?:static|async|virtual|override|sealed|new|partial)\s+)*(?:[^;\r\n{]+?\s+)(?<name>@?\w+)\s*\(",
        RegexOptions.Multiline);
    private static readonly Regex PostgresCollectionDefinition = new(
        @"\[CollectionDefinition\(\s*""(?<name>[^""]+)""",
        RegexOptions.Multiline);
    private static readonly Regex CollectionUsage = new(
        @"\[Collection\(\s*""(?<name>[^""]+)""",
        RegexOptions.Multiline);

    [Fact]
    public void PostgresContainerFixtureTests_UsePostgresRequiredAttributes()
    {
        var root = FindRepositoryRoot();
        var sourceFiles = Directory.GetFiles(Path.Combine(root, "Api.Tests"), "*.cs");
        var postgresCollections = sourceFiles
            .Select(File.ReadAllText)
            .Where(text => text.Contains("ICollectionFixture<PostgresContainerFixture>", StringComparison.Ordinal))
            .SelectMany(text => PostgresCollectionDefinition.Matches(text).Select(match => match.Groups["name"].Value))
            .ToHashSet(StringComparer.Ordinal);
        var violations = new List<string>();

        foreach (var path in sourceFiles)
        {
            var text = File.ReadAllText(path);
            if (Path.GetFileName(path) == nameof(PostgresRequiredAttributeConventionTests) + ".cs"
                || !UsesPostgresContainerFixture(text, postgresCollections))
            {
                continue;
            }

            violations.AddRange(FindViolations(Path.GetFileName(path), text));
        }

        Assert.True(
            violations.Count == 0,
            "Use [PostgresRequiredFact]/[PostgresRequiredTheory] (not [Fact]/[Theory]) for tests that need PostgresContainerFixture: " + string.Join(", ", violations));
    }

    [Fact]
    public void Convention_finds_qualified_test_attributes_that_reach_a_fixture_guard_through_a_helper()
    {
        const string source = """
            public sealed class Sample : IClassFixture<PostgresContainerFixture>
            {
                private readonly PostgresContainerFixture _fixture;

                [Xunit.Fact]
                public async Task UsesPostgres()
                {
                    await CreateDatabaseAsync();
                }

                private Task CreateDatabaseAsync()
                {
                    if (!_fixture.IsAvailable) return Task.CompletedTask;
                    return Task.CompletedTask;
                }
            }
            """;

        var violations = FindViolations("Sample.cs", source);

        var violation = Assert.Single(violations);
        Assert.Equal("Sample.cs:5", violation);
    }

    [Fact]
    public void Convention_does_not_flag_a_required_attribute_or_a_fixture_free_test()
    {
        const string source = """
            public sealed class Sample : IClassFixture<PostgresContainerFixture>
            {
                private readonly PostgresContainerFixture _fixture;

                [PostgresRequiredTheory]
                public async Task UsesPostgres()
                {
                    await CreateDatabaseAsync();
                }

                private Task<string?> CreateDatabaseAsync()
                {
                    return _fixture.TryCreateDatabaseConnectionStringAsync("sample");
                }
            }

            public sealed class UnitOnly
            {
                [Fact]
                public void DoesNotUsePostgres() { }
            }
            """;

        Assert.Empty(FindViolations("Sample.cs", source));
    }

    [Fact]
    public void Convention_recognizes_a_postgres_collection_fixture()
    {
        const string source = """
            [Collection("postgres")]
            public sealed class Sample
            {
                [Fact]
                public async Task UsesPostgres() => await CreateDatabaseAsync();

                private Task<string?> CreateDatabaseAsync()
                {
                    return _fixture.TryCreateDatabaseConnectionStringAsync("sample");
                }
            }
            """;

        Assert.True(UsesPostgresContainerFixture(source, new HashSet<string>(StringComparer.Ordinal) { "postgres" }));
        Assert.Single(FindViolations("Sample.cs", source));
    }

    private static IReadOnlyList<string> FindViolations(string fileName, string text)
    {
        var members = MemberDeclaration.Matches(text)
            .Cast<Match>()
            .Select(match => new SourceMember(
                match.Groups["name"].Value.TrimStart('@'),
                match.Index,
                match.Index + match.Length))
            .ToArray();
        var memberBodies = members
            .Select((member, index) => new KeyValuePair<string, string>(
                member.Name,
                text[member.BodyStart..(index + 1 < members.Length ? members[index + 1].Start : text.Length)]))
            .GroupBy(pair => pair.Key, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Last().Value, StringComparer.Ordinal);
        var violations = new List<string>();

        foreach (Match attribute in TestAttribute.Matches(text))
        {
            var declaration = members.FirstOrDefault(member => member.Start > attribute.Index);
            if (declaration is null || !memberBodies.TryGetValue(declaration.Name, out var body))
            {
                continue;
            }

            if (ReachesPostgresAvailabilityGuard(body, memberBodies, new HashSet<string>(StringComparer.Ordinal)))
            {
                var line = text[..attribute.Index].Count(character => character == '\n') + 1;
                violations.Add($"{fileName}:{line}");
            }
        }

        return violations;
    }

    private static bool UsesPostgresContainerFixture(string text, IReadOnlySet<string> postgresCollections)
        => text.Contains("IClassFixture<PostgresContainerFixture>", StringComparison.Ordinal)
           || Regex.IsMatch(text, @"\b[A-Za-z_]\w*\s*\(\s*PostgresContainerFixture\b")
           || CollectionUsage.Matches(text).Any(match => postgresCollections.Contains(match.Groups["name"].Value));

    private static bool ReachesPostgresAvailabilityGuard(
        string body,
        IReadOnlyDictionary<string, string> memberBodies,
        ISet<string> visitedMembers)
    {
        if (body.Contains(".IsAvailable", StringComparison.Ordinal)
            || body.Contains("TryCreateDatabaseConnectionStringAsync", StringComparison.Ordinal))
        {
            return true;
        }

        foreach (var member in memberBodies)
        {
            if (!visitedMembers.Add(member.Key)
                || !Regex.IsMatch(body, $@"\b{Regex.Escape(member.Key)}\s*\("))
            {
                continue;
            }

            if (ReachesPostgresAvailabilityGuard(member.Value, memberBodies, visitedMembers))
            {
                return true;
            }
        }

        return false;
    }

    private sealed record SourceMember(string Name, int Start, int BodyStart);

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
