using System.Reflection;
using Application.Common.Tenancy;
using Xunit;

namespace Api.Tests;

public sealed class TenantContextContractTests
{
    [Fact]
    public void TenantId_EmptyGuid_IsRejected()
    {
        var ex = Assert.Throws<ArgumentException>(() => new TenantId(Guid.Empty));

        Assert.Equal("value", ex.ParamName);
    }

    [Fact]
    public void TenantId_SameGuid_HasValueEquality()
    {
        var tenantGuid = Guid.Parse("11111111-2222-3333-4444-555555555555");
        var left = new TenantId(tenantGuid);
        var right = new TenantId(tenantGuid);

        Assert.Equal(left, right);
        Assert.True(left.Equals(right));
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
    }

    [Fact]
    public void TenantId_ToString_IsCanonicalAndStable()
    {
        var tenantGuid = Guid.Parse("11111111-2222-3333-4444-555555555555");
        var tenantId = new TenantId(tenantGuid);

        Assert.Equal(tenantGuid.ToString("D"), tenantId.ToString());
        Assert.Equal(tenantId.ToString(), tenantId.ToString());
    }

    [Fact]
    public void TenantContext_Unresolved_DoesNotExposeDefaultTenant()
    {
        ITenantContext context = new UnresolvedTenantContext();

        Assert.False(context.IsResolved);
        var ex = Assert.Throws<InvalidOperationException>(() => _ = context.TenantId);
        Assert.Contains("not resolved", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TenantContext_Resolved_ExposesExactTenant()
    {
        var tenantId = new TenantId(Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"));
        var context = new ResolvedTenantContext(tenantId);

        Assert.True(context.IsResolved);
        Assert.Equal(tenantId, context.TenantId);
    }

    [Fact]
    public void TenantId_IsNotConstructedFromStoreIdByContract()
    {
        var constructors = typeof(TenantId).GetConstructors(BindingFlags.Public | BindingFlags.Instance);

        Assert.Single(constructors);

        var constructorParameters = constructors[0].GetParameters();
        var onlyParameter = Assert.Single(constructorParameters);

        Assert.Equal(typeof(Guid), onlyParameter.ParameterType);
        Assert.Equal("value", onlyParameter.Name);

        var publicMembersWithStoreLikeParameters = typeof(TenantId)
            .GetMembers(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .OfType<MethodBase>()
            .SelectMany(member => member.GetParameters())
            .Any(parameter => parameter.ParameterType == typeof(int) || parameter.ParameterType == typeof(string));

        Assert.False(publicMembersWithStoreLikeParameters);
    }

    private sealed class ResolvedTenantContext : ITenantContext
    {
        public ResolvedTenantContext(TenantId tenantId)
        {
            TenantId = tenantId;
        }

        public bool IsResolved => true;

        public TenantId TenantId { get; }
    }

    private sealed class UnresolvedTenantContext : ITenantContext
    {
        public bool IsResolved => false;

        public TenantId TenantId => throw new InvalidOperationException("Tenant context is not resolved.");
    }
}
