using Microsoft.Extensions.Configuration;
using Xunit;

namespace Api.Tests;

public sealed class DailySalesConfigurationTests
{
    [Fact]
    public void ProductionConfiguration_BindsBelgradeTimezoneAndPreservesFileSink()
    {
        var repositoryRoot = FindRepositoryRoot();
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Path.Combine(repositoryRoot, "Api"))
            .AddJsonFile("appsettings.Production.json", optional: false)
            .Build();

        Assert.Equal("Europe/Belgrade", configuration["DailySales:TimeZoneId"]);

        var fileSink = configuration.GetSection("Serilog:WriteTo")
            .GetChildren()
            .Single(section => section["Name"] == "File");

        Assert.Equal("Logs/log-.txt", fileSink["Args:path"]);
        Assert.Equal("Day", fileSink["Args:rollingInterval"]);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Trendplus2.sln"))
                && File.Exists(Path.Combine(directory.FullName, "Api", "appsettings.Production.json")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the repository root for production configuration test.");
    }
}
