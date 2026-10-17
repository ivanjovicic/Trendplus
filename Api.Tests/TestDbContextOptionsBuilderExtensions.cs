using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Api.Tests;

internal static class TestDbContextOptionsBuilderExtensions
{
    internal static DbContextOptionsBuilder IgnoreManyServiceProvidersCreatedWarning(
        this DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.ConfigureWarnings(warnings => warnings.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning));
        return optionsBuilder;
    }
}
