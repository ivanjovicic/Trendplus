using Api.Services;
using Xunit;

namespace Api.Tests;

public sealed class AccessImportServiceTests
{
    [Fact]
    public void BuildAccessOdbcConnectionString_UsesDbq_ForLinuxRegisteredDriver()
    {
        var result = AccessImportService.BuildAccessOdbcConnectionString(
            "/tmp/trendplus-access-import/test.accdb",
            isWindows: false);

        Assert.Contains("Driver={MDBTools};", result);
        Assert.Contains("DBQ=/tmp/trendplus-access-import/test.accdb;", result);
        Assert.DoesNotContain("Database=", result);
    }

    [Fact]
    public void BuildAccessOdbcConnectionString_UsesDbq_ForLinuxDriverPath()
    {
        var result = AccessImportService.BuildAccessOdbcConnectionString(
            "/tmp/trendplus-access-import/test.accdb",
            isWindows: false,
            driverPath: "/usr/lib/x86_64-linux-gnu/odbc/libmdbodbc.so");

        Assert.Contains("Driver=/usr/lib/x86_64-linux-gnu/odbc/libmdbodbc.so;", result);
        Assert.Contains("DBQ=/tmp/trendplus-access-import/test.accdb;", result);
        Assert.DoesNotContain("Database=", result);
    }

    [Fact]
    public void BuildAccessOdbcConnectionString_UsesWindowsDriver_WhenRequested()
    {
        var result = AccessImportService.BuildAccessOdbcConnectionString(
            @"C:\temp\trendplus.accdb",
            isWindows: true);

        Assert.Contains("Driver={Microsoft Access Driver (*.mdb, *.accdb)};", result);
        Assert.Contains(@"Dbq=C:\temp\trendplus.accdb;", result);
        Assert.Contains("ReadOnly=1;", result);
    }
}
