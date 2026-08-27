namespace CidadeEmDia.Application.Authentication;

public interface IPasswordResetEmailSender
{
    Task SendPasswordResetAsync(
        string recipientEmail,
        string displayName,
        string rawToken,
        CancellationToken cancellationToken = default);
}
