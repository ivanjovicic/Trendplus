using Xunit;

namespace Api.Tests;

public sealed class AccessImportDnevnikTriggerTests
{
    [Fact]
    public void ShouldSkipLinkedTablesByDnevnikTrigger_WhenIncrementalAndNoDelta_ReturnsTrue()
    {
        var shouldSkip = AccessImportService.ShouldSkipLinkedTablesByDnevnikTrigger(
            incrementalWriteMode: true,
            dnevnikTablePresent: true,
            dnevnikImportedDelta: 0);

        Assert.True(shouldSkip);
    }

    [Fact]
    public void ShouldSkipLinkedTablesByDnevnikTrigger_WhenIncrementalAndPositiveDelta_ReturnsFalse()
    {
        var shouldSkip = AccessImportService.ShouldSkipLinkedTablesByDnevnikTrigger(
            incrementalWriteMode: true,
            dnevnikTablePresent: true,
            dnevnikImportedDelta: 5);

        Assert.False(shouldSkip);
    }

    [Theory]
    [InlineData(false, true, 0)]
    [InlineData(true, false, 0)]
    [InlineData(false, false, 0)]
    public void ShouldSkipLinkedTablesByDnevnikTrigger_WhenPrerequisitesMissing_ReturnsFalse(
        bool incrementalWriteMode,
        bool dnevnikTablePresent,
        int dnevnikImportedDelta)
    {
        var shouldSkip = AccessImportService.ShouldSkipLinkedTablesByDnevnikTrigger(
            incrementalWriteMode,
            dnevnikTablePresent,
            dnevnikImportedDelta);

        Assert.False(shouldSkip);
    }
}
