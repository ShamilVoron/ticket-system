using ITCafe.Api.Services.Contracts;

namespace ITCafe.Api.Services;

/// <summary>Background poll of IMAP inbox every 60 seconds when email_ingest_enabled=true.</summary>
public class EmailIngestHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EmailIngestHostedService> _logger;

    public EmailIngestHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<EmailIngestHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("EmailIngestHostedService started");
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var ingest = scope.ServiceProvider.GetRequiredService<IEmailIngestService>();
                await ingest.PollAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Email ingest poll cycle failed");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
