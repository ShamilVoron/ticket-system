using ITCafe.Api;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace ITCafe.Tests.Integration;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    public string DatabaseName { get; } = Guid.NewGuid().ToString();

    static CustomWebApplicationFactory()
    {
        Environment.SetEnvironmentVariable("JWT_SECRET", "test-jwt-secret-32-chars-long-min!!!");
        Environment.SetEnvironmentVariable("DB_PASSWORD", "testpass");
        Environment.SetEnvironmentVariable("RABBITMQ_USERNAME", "guest");
        Environment.SetEnvironmentVariable("RABBITMQ_PASSWORD", "guest");
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.UseEnvironment("Test");
        builder.ConfigureAppConfiguration(cfg =>
        {
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TestDbName"] = DatabaseName
            });
        });
        return base.CreateHost(builder);
    }
}
