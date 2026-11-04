using Cemetery.Application.Abstractions;
using Cemetery.Domain.Identity;

namespace Cemetery.Api.IntegrationTests;

internal sealed class InvitationInbox : IEmailSender
{
    private readonly Dictionary<string, string> invitations = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> resets = new(StringComparer.OrdinalIgnoreCase);

    public Task SendInvitationAsync(EmailAddress recipient, string acceptUrl, CancellationToken cancellationToken)
    {
        invitations[recipient.Value] = Token(acceptUrl, "token");
        return Task.CompletedTask;
    }

    public Task SendPasswordResetAsync(EmailAddress recipient, string resetUrl, CancellationToken cancellationToken)
    {
        resets[recipient.Value] = Token(resetUrl, "token");
        return Task.CompletedTask;
    }

    public string TakeInvitation(string email) => Take(invitations, email, "Invitation was not sent.");

    public string TakeReset(string email) => Take(resets, email, "Password reset was not sent.");

    public void Clear()
    {
        invitations.Clear();
        resets.Clear();
    }

    private static string Take(Dictionary<string, string> messages, string email, string missing)
    {
        if (!messages.Remove(email, out var token))
            throw new InvalidOperationException(missing);

        return token;
    }

    private static string Token(string url, string name)
    {
        var query = new Uri(url).Query.TrimStart('?');
        foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            if (parts.Length == 2 && parts[0] == name)
                return Uri.UnescapeDataString(parts[1]);
        }

        throw new InvalidOperationException("URL has no token.");
    }
}
