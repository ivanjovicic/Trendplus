namespace Application.Common.Tenancy;

public interface ITenantContext
{
    bool IsResolved { get; }

    TenantId TenantId { get; }
}
