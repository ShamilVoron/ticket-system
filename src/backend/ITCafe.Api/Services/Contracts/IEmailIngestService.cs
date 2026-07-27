namespace ITCafe.Api.Services.Contracts;

public interface IEmailIngestService
{
    /// <summary>Poll IMAP INBOX for UNSEEN messages and create/update tickets. Fail-soft.</summary>
    Task PollAsync(CancellationToken cancellationToken = default);
}
