namespace ITCafe.Api.Services.Contracts;

public interface ITenantProvider
{
    int? CurrentOrganizationId { get; }
}
