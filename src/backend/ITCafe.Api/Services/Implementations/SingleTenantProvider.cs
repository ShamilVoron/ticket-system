using ITCafe.Api.Services.Contracts;

namespace ITCafe.Api.Services.Implementations;

public class SingleTenantProvider : ITenantProvider
{
    public int? CurrentOrganizationId => 1;
}
