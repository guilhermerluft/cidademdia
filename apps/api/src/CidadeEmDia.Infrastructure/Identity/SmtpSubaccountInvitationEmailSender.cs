using System.Net;
using System.Net.Mail;
using CidadeEmDia.Application.Subaccounts;

namespace CidadeEmDia.Infrastructure.Identity;

internal sealed class SmtpSubaccountInvitationEmailSender(SubaccountInvitationOptions options) : ISubaccountInvitationEmailSender
{
    public async Task SendInvitationAsync(
        string recipientEmail,
        string masterDisplayName,
        string rawToken,
        CancellationToken cancellationToken = default)
    {
        if (!options.IsEmailConfigured)
            throw new InvalidOperationException("Subaccount invitation e-mail delivery is not configured.");

        var separator = options.InviteUrl.Contains('?', StringComparison.Ordinal) ? "&" : "?";
        var inviteLink = $"{options.InviteUrl}{separator}invite={Uri.EscapeDataString(rawToken)}";
        var safeMaster = WebUtility.HtmlEncode(masterDisplayName);
        var safeLink = WebUtility.HtmlEncode(inviteLink);

        using var message = new MailMessage
        {
            From = new MailAddress(options.FromAddress, options.FromName),
            Subject = "Convite para equipe — CidadeEmDia",
            IsBodyHtml = true,
            Body = $"""
                <p>Olá.</p>
                <p><strong>{safeMaster}</strong> convidou você para participar da equipe no CidadeEmDia.</p>
                <p><a href="{safeLink}">Clique aqui para criar sua conta e aceitar o convite</a>.</p>
                <p>Este convite expira em {Math.Round(options.TokenLifetime.TotalHours)} horas e pode ser utilizado uma única vez.</p>
                <p>Se você não esperava este convite, ignore este e-mail.</p>
                """
        };
        message.To.Add(new MailAddress(recipientEmail));

        using var client = new SmtpClient(options.SmtpHost, options.SmtpPort)
        {
            EnableSsl = options.EnableSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = false
        };

        if (!string.IsNullOrWhiteSpace(options.SmtpUsername))
            client.Credentials = new NetworkCredential(options.SmtpUsername, options.SmtpPassword);

        await client.SendMailAsync(message).WaitAsync(cancellationToken);
    }
}
