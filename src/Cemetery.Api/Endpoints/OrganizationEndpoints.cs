using Cemetery.Application.Abstractions;
using Cemetery.Application.Dashboard;
using Cemetery.Application.Organizations;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Cemetery.Api.Endpoints;

public static class OrganizationEndpoints
{
    public static RouteGroupBuilder MapOrganizationEndpoints(this RouteGroupBuilder api)
    {
        api.MapPost("/organizations", Create).RequireAuthorization();
        api.MapGet("/organizations", List).RequireAuthorization();
        api.MapPost("/organizations/switch", Switch).RequireAuthorization();
        api.MapPost("/organizations/invitations", Invite).RequireAuthorization("tenant");
        api.MapPost("/invitations/accept", Accept).RequireAuthorization();
        api.MapGet("/dashboard", Dashboard).RequireAuthorization("tenant");
        api.MapGet("/health", Health).AllowAnonymous();
        return api;
    }

    private static async Task<Ok<OrganizationCreated>> Create(
        CreateOrganization command,
        ICommandHandler<CreateOrganization, OrganizationCreated> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.Handle(command, cancellationToken).ConfigureAwait(false));

    private static async Task<Ok<IReadOnlyList<OrganizationSummary>>> List(
        IQueryHandler<ListMyOrganizations, IReadOnlyList<OrganizationSummary>> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.Handle(new ListMyOrganizations(), cancellationToken).ConfigureAwait(false));

    private static async Task<Ok<OrganizationSummary>> Switch(
        SwitchOrganization command,
        ICommandHandler<SwitchOrganization, OrganizationSummary> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.Handle(command, cancellationToken).ConfigureAwait(false));

    private static async Task<Ok<InvitationIssued>> Invite(
        InviteMember command,
        ICommandHandler<InviteMember, InvitationIssued> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.Handle(command, cancellationToken).ConfigureAwait(false));

    private static async Task<Ok<OrganizationSummary>> Accept(
        AcceptInvitation command,
        ICommandHandler<AcceptInvitation, OrganizationSummary> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.Handle(command, cancellationToken).ConfigureAwait(false));

    private static async Task<Ok<DashboardView>> Dashboard(
        IQueryHandler<GetDashboard, DashboardView> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.Handle(new GetDashboard(), cancellationToken).ConfigureAwait(false));

    private static Ok<HealthStatus> Health() => TypedResults.Ok(new HealthStatus("ok"));
}

public sealed record HealthStatus(string Status);
