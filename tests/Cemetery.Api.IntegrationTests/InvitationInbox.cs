using Cemetery.Application.Abstractions;
using Cemetery.Domain.Identity;

namespace Cemetery.Api.IntegrationTests;

internal sealed class InvitationInbox : IEmailSender
{
    private readonly Dictionary<string, string> invitations = new(StringComparer.OrdinalIgnoreCase);

    public Task SendInvitationAsync(EmailAddress recipient, string acceptUrl, CancellationToken cancellationToken)
    {
        invitations[recipient.Value] = Token(acceptUrl);
        return Task.CompletedTask;
    }

    public Task SendPasswordResetAsync(EmailAddress recipient, string resetUrl, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public string TakeInvitation(string email)
    {
        if (!invitations.Remove(email, out var token))
            throw new InvalidOperationException("Invitation was not sent.");

        return token;
    }

    public void Clear() => invitations.Clear();

    private static string Token(string acceptUrl)
    {
        var query = new Uri(acceptUrl).Query.TrimStart('?');
        foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            if (parts.Length == 2 && parts[0] == "token")
                return Uri.UnescapeDataString(parts[1]);
        }

        throw new InvalidOperationException("Invitation URL has no token.");
    }
}
