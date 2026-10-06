using System.Security.Cryptography;
using System.Text;
using Cemetery.Application.Abstractions;
using Cemetery.Domain.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Cemetery.Infrastructure.Identity;

internal sealed class InvitationTokenFactory : IInvitationTokenFactory
{
    private const int TokenBytes = 32;

    public string Create() => Convert.ToHexString(RandomNumberGenerator.GetBytes(TokenBytes));

    public string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}

internal sealed class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IEmailSender
{
    public Task SendInvitationAsync(EmailAddress recipient, string acceptUrl, CancellationToken cancellationToken)
    {
        var email = recipient.Value;
        if (logger.IsEnabled(LogLevel.Information))
            logger.LogInformation("Invitation for {Email}: {AcceptUrl}", email, acceptUrl);
        return Task.CompletedTask;
    }

    public Task SendPasswordResetAsync(EmailAddress recipient, string resetUrl, CancellationToken cancellationToken)
    {
        var email = recipient.Value;
        if (logger.IsEnabled(LogLevel.Information))
            logger.LogInformation("Password reset for {Email}: {ResetUrl}", email, resetUrl);
        return Task.CompletedTask;
    }
}

internal sealed class ConfiguredAppLinks(IConfiguration configuration) : IAppLinks
{
    public string InvitationUrl(string token) =>
        string.Concat(PublicRoot(), "/invitations/accept?token=", Uri.EscapeDataString(token));

    public string PasswordResetUrl(string email, string token) =>
        string.Concat(PublicRoot(), "/password-reset?email=", Uri.EscapeDataString(email), "&token=", Uri.EscapeDataString(token));

    private string PublicRoot()
    {
        var root = configuration["App:PublicUrl"];
        if (string.IsNullOrWhiteSpace(root))
            throw new InvalidOperationException("App:PublicUrl is required.");

        return root.TrimEnd('/');
    }
}
