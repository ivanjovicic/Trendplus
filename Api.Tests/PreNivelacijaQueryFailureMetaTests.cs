using Trendplus2.Endpoints;
using Xunit;

namespace Api.Tests;

public sealed class PreNivelacijaQueryFailureMetaTests
{
    [Fact]
    public void QueryFailureException_PreservesFailureSourceForUncachedMapping()
    {
        var source = new InvalidOperationException("database unavailable");
        var exception = new PreNivelacijaQueryFailedException(
            salesQueryFailed: true,
            markdownQueryFailed: false,
            source);

        Assert.True(exception.SalesQueryFailed);
        Assert.False(exception.MarkdownQueryFailed);
        Assert.Same(source, exception.InnerException);
    }

    [Fact]
    public void BuildQueryFailureMeta_SalesFailure_IsErrorNotSuccessZero()
    {
        var meta = PreNivelacijaPriorityEndpoints.BuildQueryFailureMeta(salesQueryFailed: true, markdownQueryFailed: false);

        Assert.False(meta.Success);
        Assert.Equal("pre_nivelacija_sales_unavailable", meta.ErrorCode);
        Assert.Contains("nije dostupna", meta.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildQueryFailureMeta_MarkdownFailure_IsErrorNotSuccessZero()
    {
        var meta = PreNivelacijaPriorityEndpoints.BuildQueryFailureMeta(salesQueryFailed: false, markdownQueryFailed: true);

        Assert.False(meta.Success);
        Assert.Equal("pre_nivelacija_markdown_unavailable", meta.ErrorCode);
    }
}
