using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace Api.Tests;

/// <summary>
/// RQ649: an Api.Tests Program host backed by EF InMemory must not keep the web-process Access import worker, whose
/// queue claim/recovery is relational-only and would otherwise log provider errors every polling interval.
/// builder.UseSetting does not count as a guard: appsettings.json sets AccessImport:RegisterWorkerInWebProcess=true
/// and outranks it, so the registered descriptor has to be removed.
/// </summary>
public sealed class InMemoryTestHostAccessImportWorkerConventionTests
{
    private static readonly string HostTypePattern = "WebApplicationFactory<" + "global::Program>";
    private const string InMemoryPattern = "UseInMemoryDatabase";

    private static readonly string[] AcceptedGuards =
    {
        "AccessImportTestHostIsolation.RemoveAccessImportWorker(",
        "RemoveHostedService<AccessImportBackgroundWorker>(",
        "RemoveAll<IHostedService>()"
    };

    [Fact]
    public void ApiTests_InMemoryProgramHosts_DoNotRegisterAccessImportWorker()
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
            violations.AddRange(FindViolations(relativePath, File.ReadAllText(filePath)));
        }

        Assert.True(
            violations.Count == 0,
            "EF InMemory Program hosts must not register AccessImportBackgroundWorker:\n"
            + string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void FindViolations_FlagsInMemoryProgramHostsWithoutDescriptorRemoval()
    {
        var unguarded = $"class Factory : {HostTypePattern} {{ void C() => o.{InMemoryPattern}(\"x\"); }}";
        var settingOnly = unguarded + "\nbuilder.UseSetting(\"AccessImport:RegisterWorkerInWebProcess\", \"false\");";
        var helperGuard = unguarded + "\nAccessImportTestHostIsolation.RemoveAccessImportWorker(services);";
        var allHostedServicesRemoved = unguarded + "\nservices.RemoveAll<IHostedService>();";
        var relationalHost = $"class Factory : {HostTypePattern} {{ }}";

        Assert.Single(FindViolations("Unguarded.cs", unguarded));
        Assert.Single(FindViolations("SettingOnly.cs", settingOnly));
        Assert.Empty(FindViolations("Helper.cs", helperGuard));
        Assert.Empty(FindViolations("Removed.cs", allHostedServicesRemoved));
        Assert.Empty(FindViolations("Relational.cs", relationalHost));
    }

    private static IEnumerable<string> FindViolations(string relativePath, string source)
    {
        if (!source.Contains(HostTypePattern, StringComparison.Ordinal)
            || !source.Contains(InMemoryPattern, StringComparison.Ordinal)
            || AcceptedGuards.Any(guard => source.Contains(guard, StringComparison.Ordinal)))
        {
            yield break;
        }

        yield return $"{relativePath}: hosts {HostTypePattern} on {InMemoryPattern} without removing the " +
                     "AccessImportBackgroundWorker descriptor (use AccessImportTestHostIsolation.RemoveAccessImportWorker(services)).";
    }

    private static string ResolveApiTestsRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "Api.Tests");
            if (Directory.Exists(candidate) && File.Exists(Path.Combine(candidate, "Api.Tests.csproj")))
            {
                return candidate;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Could not locate Api.Tests project root from test output directory.");
    }
}
