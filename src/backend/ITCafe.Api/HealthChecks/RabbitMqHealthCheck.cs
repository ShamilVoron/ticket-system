using Microsoft.Extensions.Diagnostics.HealthChecks;
using RabbitMQ.Client;

namespace ITCafe.Api.HealthChecks;

public class RabbitMqHealthCheck : IHealthCheck
{
    private readonly IConfiguration _configuration;

    public RabbitMqHealthCheck(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var host = _configuration.GetValue<string>("RabbitMQ:Host") ?? "localhost";
            var port = _configuration.GetValue<int?>("RabbitMQ:Port") ?? 5672;
            var username = _configuration.GetValue<string>("RabbitMQ:Username") ?? "guest";
            var password = _configuration.GetValue<string>("RabbitMQ:Password") ?? "guest";

            var factory = new ConnectionFactory
            {
                HostName = host,
                Port = port,
                UserName = username,
                Password = password,
                RequestedConnectionTimeout = TimeSpan.FromSeconds(3)
            };

            using var connection = factory.CreateConnection();
            return Task.FromResult(HealthCheckResult.Healthy("RabbitMQ is reachable."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("RabbitMQ connection failed.", ex));
        }
    }
}
