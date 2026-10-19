using Cemetery.Application.Abstractions;
using Cemetery.Application.Exceptions;
using Cemetery.Application.Register;
using Cemetery.Domain.Identity;
using Cemetery.Domain.Launch;
using FluentValidation;

namespace Cemetery.Application.Launch;

public sealed record WebhookView(Guid Id, string Url, string Event);

public sealed record WebhookCreated(Guid Id, string Url, string Event, string Secret);

public sealed record WebhookBody(string Url, string Event);

public sealed record ListWebhooks;

public sealed class ListWebhooksHandler(
    ICurrentUser current,
    ITenantContext tenant,
    IMembershipRepository memberships,
    IWebhookRepository webhooks) : IQueryHandler<ListWebhooks, IReadOnlyList<WebhookView>>
{
    public async Task<IReadOnlyList<WebhookView>> Handle(ListWebhooks query, CancellationToken cancellationToken)
    {
        await RegisterAccess.RequireClerkAsync(current, tenant, memberships, cancellationToken).ConfigureAwait(false);
        var rows = await webhooks.ListAsync(cancellationToken).ConfigureAwait(false);
        return rows.Select(static item => new WebhookView(item.Id, item.Url, item.Event)).ToArray();
    }
}

public sealed record AddWebhook(string Url, string Event);

public sealed class AddWebhookValidator : AbstractValidator<AddWebhook>
{
    public AddWebhookValidator()
    {
        RuleFor(command => command.Url).Must(static url => Accepts(url)).WithErrorCode("webhook.url_invalid");
        RuleFor(command => command.Event).Must(static name => WebhookEvents.IsKnown((name ?? "").Trim())).WithErrorCode("webhook.event_invalid");
    }

    private static bool Accepts(string? url)
    {
        try
        {
            _ = WebhookAddress.Require(url);
            return true;
        }
        catch (DomainRuleException)
        {
            return false;
        }
    }
}

public sealed class AddWebhookHandler(
    ICurrentUser current,
    ITenantContext tenant,
    IMembershipRepository memberships,
    IWebhookRepository webhooks,
    IUnitOfWork unitOfWork) : ICommandHandler<AddWebhook, WebhookCreated>
{
    public async Task<WebhookCreated> Handle(AddWebhook command, CancellationToken cancellationToken)
    {
        var organizationId = await RegisterAccess.RequireClerkAsync(current, tenant, memberships, cancellationToken).ConfigureAwait(false);
        var created = WebhookSubscription.Register(organizationId, command.Url, command.Event, WebhookSecrets.Create());
        await webhooks.AddAsync(created, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new WebhookCreated(created.Id, created.Url, created.Event, created.Secret);
    }
}

public sealed record RemoveWebhook(Guid WebhookId);

public sealed class RemoveWebhookValidator : AbstractValidator<RemoveWebhook>
{
    public RemoveWebhookValidator() =>
        RuleFor(command => command.WebhookId).NotEmpty().WithErrorCode("webhook.not_found");
}

public sealed class RemoveWebhookHandler(
    ICurrentUser current,
    ITenantContext tenant,
    IMembershipRepository memberships,
    IWebhookRepository webhooks,
    IUnitOfWork unitOfWork) : ICommandHandler<RemoveWebhook, bool>
{
    public async Task<bool> Handle(RemoveWebhook command, CancellationToken cancellationToken)
    {
        await RegisterAccess.RequireClerkAsync(current, tenant, memberships, cancellationToken).ConfigureAwait(false);
        var subscription = await webhooks.FindAsync(command.WebhookId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("webhook.not_found");
        webhooks.Remove(subscription);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }
}
