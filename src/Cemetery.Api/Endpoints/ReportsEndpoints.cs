using Cemetery.Application.Abstractions;
using Cemetery.Application.Launch;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Cemetery.Api.Endpoints;

public static class ReportsEndpoints
{
    public static RouteGroupBuilder MapReportsEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/cemeteries/{cemeteryId}/reports", GetReport).RequireAuthorization("tenant");
        return api;
    }

    private static async Task<Ok<CemeteryReportView>> GetReport(
        Guid cemeteryId,
        IQueryHandler<GetCemeteryReport, CemeteryReportView> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.Handle(new GetCemeteryReport(cemeteryId), cancellationToken).ConfigureAwait(false));
}
