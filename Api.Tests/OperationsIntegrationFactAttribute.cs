using System;
using Xunit;

namespace Trendplus2.Tests;

/// <summary>
/// Keeps live Operations proof visible as skipped when the external integration
/// dependency is not enabled, instead of allowing an early-return pass.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
internal sealed class OperationsIntegrationFactAttribute : FactAttribute
{
    public OperationsIntegrationFactAttribute()
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable("TRENDPLUS_RUN_INTEGRATION_TESTS"),
                "true",
                StringComparison.OrdinalIgnoreCase))
        {
            Skip = "TRENDPLUS_RUN_INTEGRATION_TESTS=true is required for live Operations proof.";
        }
    }
}
