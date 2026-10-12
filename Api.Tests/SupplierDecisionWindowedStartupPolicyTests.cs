using Infrastructure.Seed;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Api.Tests;

public sealed class SupplierDecisionWindowedStartupPolicyTests
{
    [Fact]
    public void MissingOverride_PreservesExistingMigrationAndTestBehavior()
    {
        var configuration = new ConfigurationBuilder().Build();
        Assert.True(DatabaseInitializer.ShouldBuildSupplierDecisionWindowedViewsAtStartup(configuration));
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    public void ExplicitStartupOverride_IsHonored(string setting, bool expected)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DatabaseInitialization:BuildSupplierDecisionWindowedViewsOnStartup"] = setting
            })
            .Build();

        Assert.Equal(expected, DatabaseInitializer.ShouldBuildSupplierDecisionWindowedViewsAtStartup(configuration));
    }
}
