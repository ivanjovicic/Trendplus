using Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Text.RegularExpressions;
using Xunit;

namespace Api.Tests;

/// <summary>
/// RQ665/RQ685: independent test service providers can create new EF internal providers and increase a
/// process-wide count. Past twenty, ManyServiceProvidersCreatedWarning can throw on whichever test runs
/// next. Test DbContext registrations must therefore opt out of that warning.
/// </summary>
public sealed class TestHostDbContextWarningConventionTests
{
    private static readonly Regex DbContextRegistration = new(
        @"AddDbContext(?:Factory)?\s*<[^>]+>\s*\(",
        RegexOptions.Compiled);

    [Fact]
    public void Test_host_dbcontext_registrations_ignore_many_service_providers_warning()
    {
        var repositoryRoot = FindRepositoryRoot();
        var testRoots = new[] { "Api.Tests", "Infrastructure.Tests" }
            .Select(relativePath => Path.Combine(repositoryRoot, relativePath))
            .ToArray();

        Assert.All(testRoots, root => Assert.True(Directory.Exists(root), $"Expected test source directory was not found: {root}"));

        var violations = testRoots
            .SelectMany(root => Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                && !path.EndsWith(nameof(TestHostDbContextWarningConventionTests) + ".cs", StringComparison.Ordinal))
            .SelectMany(path => FindViolations(Path.GetRelativePath(repositoryRoot, path), File.ReadAllText(path)))
            .ToList();

        Assert.True(violations.Count == 0,
            "Add .ConfigureWarnings(warnings => warnings.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning)) to test DbContext service registrations: "
            + string.Join(", ", violations));
    }

    [Fact]
    public void Convention_flags_a_test_host_registration_without_the_warning_opt_out()
    {
        const string source = """
            class Factory : WebApplicationFactory<Program>
            {
                void Configure(IServiceCollection services)
                {
                    services.AddDbContext<TrendplusDbContext>(options => options.UseNpgsql(_connectionString));
                    services.AddDbContextFactory<TrendplusDbContext>(options => options.UseInMemoryDatabase(_name).ConfigureWarnings(warnings => warnings.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning)));
                }
            }
            """;

        Assert.Equal("Sample.cs:5", Assert.Single(FindViolations("Sample.cs", source)));
    }

    [Fact]
    public void Convention_flags_a_plain_service_collection_registration_without_the_warning_opt_out()
    {
        const string source = """
            class WorkerTests
            {
                void Configure()
                {
                    var services = new ServiceCollection();
                    services.AddDbContext<TrendplusDbContext>(options => options.UseNpgsql(_connectionString));
                }
            }
            """;

        Assert.Equal("Sample.cs:6", Assert.Single(FindViolations("Sample.cs", source)));
    }

    [Fact]
    public void Convention_accepts_a_plain_service_collection_registration_with_the_warning_opt_out()
    {
        const string source = """
            class WorkerTests
            {
                void Configure()
                {
                    var services = new ServiceCollection();
                    services.AddDbContext<TrendplusDbContext>(options => options.UseNpgsql(_connectionString).ConfigureWarnings(warnings => warnings.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning)));
                }
            }
            """;

        Assert.Empty(FindViolations("Sample.cs", source));
    }

    [Fact]
    public void Warning_opt_out_helper_prevents_provider_count_exception_for_plain_service_collections()
    {
        for (var index = 0; index < 25; index++)
        {
            var services = new ServiceCollection();
            services.AddDbContext<TrendplusDbContext>(options => options
                .UseInMemoryDatabase($"rq685-{index}")
                .IgnoreManyServiceProvidersCreatedWarning());

            using var provider = services.BuildServiceProvider();
            using var scope = provider.CreateScope();
            _ = scope.ServiceProvider.GetRequiredService<TrendplusDbContext>();
        }
    }

    [Fact]
    public void Convention_ignores_options_builders_without_service_collection_registration()
    {
        const string source = "var options = new DbContextOptionsBuilder<TrendplusDbContext>().UseNpgsql(cs).Options;";

        Assert.Empty(FindViolations("Plain.cs", source));
    }

    private static IEnumerable<string> FindViolations(string fileName, string source)
    {
        foreach (Match match in DbContextRegistration.Matches(source))
        {
            var openingParenthesis = source.IndexOf('(', match.Index + match.Length - 1);
            var closingParenthesis = FindClosingParenthesis(source, openingParenthesis);
            var argumentEnd = closingParenthesis < 0 ? source.Length : closingParenthesis;
            var registration = source[(openingParenthesis + 1)..argumentEnd];

            if (!registration.Contains("ManyServiceProvidersCreatedWarning", StringComparison.Ordinal))
            {
                var line = source.Take(match.Index).Count(ch => ch == '\n') + 1;
                yield return $"{fileName}:{line}";
            }
        }
    }

    private static int FindClosingParenthesis(string source, int openingParenthesis)
    {
        var depth = 0;
        var inLineComment = false;
        var inBlockComment = false;
        var inString = false;
        var inVerbatimString = false;
        var inCharacter = false;

        for (var index = openingParenthesis; index < source.Length; index++)
        {
            var current = source[index];
            var next = index + 1 < source.Length ? source[index + 1] : '\0';

            if (inLineComment)
            {
                if (current == '\n') inLineComment = false;
                continue;
            }

            if (inBlockComment)
            {
                if (current == '*' && next == '/')
                {
                    inBlockComment = false;
                    index++;
                }

                continue;
            }

            if (inString)
            {
                if (!inVerbatimString && current == '\\')
                {
                    index++;
                    continue;
                }

                if (current == '"')
                {
                    if (inVerbatimString && next == '"')
                    {
                        index++;
                        continue;
                    }

                    inString = false;
                    inVerbatimString = false;
                }

                continue;
            }

            if (inCharacter)
            {
                if (current == '\\')
                {
                    index++;
                    continue;
                }

                if (current == '\'') inCharacter = false;
                continue;
            }

            if (current == '/' && next == '/')
            {
                inLineComment = true;
                index++;
                continue;
            }

            if (current == '/' && next == '*')
            {
                inBlockComment = true;
                index++;
                continue;
            }

            if (current == '"')
            {
                inString = true;
                inVerbatimString = index > 0 && source[index - 1] == '@';
                continue;
            }

            if (current == '\'')
            {
                inCharacter = true;
                continue;
            }

            if (current == '(') depth++;
            if (current == ')' && --depth == 0) return index;
        }

        return -1;
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Api.Tests", "Api.Tests.csproj")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not locate the repository root containing Api.Tests.csproj");
    }
}
