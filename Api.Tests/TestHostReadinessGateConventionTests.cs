using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace Api.Tests;

/// <summary>
/// RQ640: every Api.Tests host using <see cref="Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory{Program}"/>
/// must declare <c>StartupReadiness:GateApiTraffic</c> explicitly so Testing env does not return gated 503 by accident.
/// </summary>
public sealed class TestHostReadinessGateConventionTests
{
    private const string GateSettingKey = "StartupReadiness:GateApiTraffic";
    private const string HostTypePattern = "WebApplicationFactory<global::Program>";
    private const string DefaultFixturePattern = "IClassFixture<WebApplicationFactory<global::Program>>";

    private static readonly IReadOnlyDictionary<string, string> AllowListedRelativePaths =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    [Fact]
    public void ApiTests_WebApplicationFactoryHosts_DeclareStartupReadinessGateExplicitly()
    {
        var apiTestsRoot = ResolveApiTestsRoot();
        var violations = new List<string>();

        foreach (var filePath in Directory.EnumerateFiles(apiTestsRoot, "*.cs", SearchOption.AllDirectories))
        {
            if (filePath.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                || filePath.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            {
                continue;
            }

            var relativePath = Path.GetRelativePath(apiTestsRoot, filePath).Replace('\\', '/');
            if (string.Equals(relativePath, "TestHostReadinessGateConventionTests.cs", StringComparison.Ordinal))
            {
                continue;
            }

            var source = File.ReadAllText(filePath);
            if (!source.Contains(HostTypePattern, StringComparison.Ordinal))
            {
                continue;
            }

            if (AllowListedRelativePaths.TryGetValue(relativePath, out var allowReason))
            {
                continue;
            }

            if (!source.Contains(GateSettingKey, StringComparison.Ordinal))
            {
                violations.Add(
                    $"{relativePath}: mentions {HostTypePattern} but not '{GateSettingKey}'. " +
                    $"Add builder.UseSetting(\"{GateSettingKey}\", \"false\") (or \"true\" when testing readiness) in the host factory.");
                continue;
            }

            if (source.Contains(DefaultFixturePattern, StringComparison.Ordinal)
                && !ConfiguresInjectedDefaultFactory(source))
            {
                violations.Add(
                    $"{relativePath}: uses {DefaultFixturePattern} but does not configure the injected factory " +
                    $"with WithWebHostBuilder + '{GateSettingKey}'. Mirror AnalyticsDbInfrastructureTests constructor wiring.");
            }
        }

        Assert.True(
            violations.Count == 0,
            "Api.Tests WebApplicationFactory hosts must declare StartupReadiness gate explicitly:\n"
            + string.Join(Environment.NewLine, violations));
    }

    private static bool ConfiguresInjectedDefaultFactory(string source)
    {
        return Regex.IsMatch(
            source,
            @"factory\.WithWebHostBuilder[\s\S]*?" + Regex.Escape(GateSettingKey),
            RegexOptions.CultureInvariant);
    }

    private static string ResolveApiTestsRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "Api.Tests");
            if (Directory.Exists(candidate)
                && File.Exists(Path.Combine(candidate, "Api.Tests.csproj")))
            {
                return candidate;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Could not locate Api.Tests project root from test output directory.");
    }
}
