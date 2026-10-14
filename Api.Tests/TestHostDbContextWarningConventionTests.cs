using System.Text.RegularExpressions;
using Xunit;

namespace Api.Tests;

/// <summary>
/// RQ665: every in-process test host builds its own application service provider, and EF Core keys its
/// internal service-provider cache on it, so each WebApplicationFactory adds one more internal provider
/// to a process-wide count. Past twenty, ManyServiceProvidersCreatedWarning throws, and which test hits
/// it depends on test ordering. Hosts that register DbContexts must therefore opt out of that warning.
/// </summary>
public sealed class TestHostDbContextWarningConventionTests
{
    private static readonly Regex DbContextRegistration = new(
        @"AddDbContext(?:Factory)?<\w+>\(options\s*=>\s*options\s*\.(?:UseNpgsql|UseInMemoryDatabase)\([^)]*\)(?<tail>[^;]*);",
        RegexOptions.Compiled);

    [Fact]
    public void Test_host_dbcontext_registrations_ignore_many_service_providers_warning()
    {
        var root = FindTestProjectRoot();
        var violations = Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                && !path.EndsWith(nameof(TestHostDbContextWarningConventionTests) + ".cs", StringComparison.Ordinal))
            .SelectMany(path => FindViolations(Path.GetFileName(path), File.ReadAllText(path)))
            .ToList();

        Assert.True(violations.Count == 0,
            "Add .ConfigureWarnings(warnings => warnings.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning)) to test-host DbContext registrations: "
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
    public void Convention_ignores_files_without_an_in_process_test_host()
    {
        const string source = "var options = services.AddDbContext<TrendplusDbContext>(options => options.UseNpgsql(cs));";

        Assert.Empty(FindViolations("Plain.cs", source));
    }

    private static IEnumerable<string> FindViolations(string fileName, string source)
    {
        if (!source.Contains("WebApplicationFactory<", StringComparison.Ordinal))
        {
            yield break;
        }

        foreach (Match match in DbContextRegistration.Matches(source))
        {
            if (!match.Groups["tail"].Value.Contains("ManyServiceProvidersCreatedWarning", StringComparison.Ordinal))
            {
                var line = source.Take(match.Index).Count(ch => ch == '\n') + 1;
                yield return $"{fileName}:{line}";
            }
        }
    }

    private static string FindTestProjectRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Api.Tests.csproj")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not locate Api.Tests.csproj");
    }
}