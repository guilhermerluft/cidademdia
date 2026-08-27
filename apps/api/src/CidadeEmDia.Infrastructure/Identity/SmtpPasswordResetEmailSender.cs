using System.Net;
using System.Net.Mail;
using CidadeEmDia.Application.Authentication;

namespace CidadeEmDia.Infrastructure.Identity;

internal sealed class SmtpPasswordResetEmailSender(PasswordResetOptions options) : IPasswordResetEmailSender
{
    public async Task SendPasswordResetAsync(
        string recipientEmail,
        string displayName,
        string rawToken,
        CancellationToken cancellationToken = default)
    {
        if (!options.IsEmailConfigured)
            throw new InvalidOperationException("Password reset e-mail delivery is not configured.");

        var separator = options.ResetUrl.Contains('?', StringComparison.Ordinal) ? "&" : "?";
        var resetLink = $"{options.ResetUrl}{separator}token={Uri.EscapeDataString(rawToken)}";
        var safeName = WebUtility.HtmlEncode(displayName);
        var safeLink = WebUtility.HtmlEncode(resetLink);

        using var message = new MailMessage
        {
            From = new MailAddress(options.FromAddress, options.FromName),
            Subject = "Redefinição de senha — CidadeEmDia",
            IsBodyHtml = true,
            Body = $"""
                <p>Olá, {safeName}.</p>
                <p>Recebemos uma solicitação para redefinir a senha da sua conta no CidadeEmDia.</p>
                <p><a href="{safeLink}">Clique aqui para criar uma nova senha</a>.</p>
                <p>Este link expira em {Math.Round(options.TokenLifetime.TotalMinutes)} minutos e pode ser utilizado uma única vez.</p>
                <p>Se você não solicitou essa alteração, ignore este e-mail.</p>
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
