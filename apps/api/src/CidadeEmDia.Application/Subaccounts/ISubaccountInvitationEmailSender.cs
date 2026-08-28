namespace CidadeEmDia.Application.Subaccounts;

public interface ISubaccountInvitationEmailSender
{
    Task SendInvitationAsync(
        string recipientEmail,
        string masterDisplayName,
        string rawToken,
        CancellationToken cancellationToken = default);
}
