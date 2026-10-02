using Cemetery.Application.Exceptions;
using Cemetery.Domain.Identity;
using Microsoft.AspNetCore.Diagnostics;

namespace Cemetery.Api.Errors;

public sealed class CemeteryExceptionHandler(ILogger<CemeteryExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var mapped = Map(exception);
        if (mapped is null)
            return false;

        var errorCode = mapped.Code;
        if (logger.IsEnabled(LogLevel.Information))
            logger.LogInformation("Request failed with {ErrorCode}", errorCode);
        await WriteAsync(httpContext, mapped).ConfigureAwait(false);
        return true;
    }

    private static Mapped? Map(Exception exception) => exception switch
    {
        ValidationFailedException validation => new Mapped(Status.BadRequest, ValidationFailedException.Code, validation.ErrorCodes),
        UnauthorizedException unauthorized => new Mapped(Status.Unauthorized, unauthorized.Code, null),
        ForbiddenException forbidden => new Mapped(Status.Forbidden, forbidden.Code, null),
        NotFoundException notFound => new Mapped(Status.NotFound, notFound.Code, null),
        ConflictException conflict => new Mapped(Status.Conflict, conflict.Code, null),
        DomainRuleException domain => new Mapped(Status.Unprocessable, domain.Code, null),
        _ => null,
    };

    private static Task WriteAsync(HttpContext httpContext, Mapped mapped)
    {
        var extensions = new Dictionary<string, object?> { ["errorCode"] = mapped.Code };
        if (mapped.Errors is not null)
            extensions["errors"] = mapped.Errors;

        return Results.Problem(statusCode: mapped.Status, title: mapped.Code, extensions: extensions)
            .ExecuteAsync(httpContext);
    }

    private sealed record Mapped(int Status, string Code, IReadOnlyList<string>? Errors);

    private static class Status
    {
        public const int BadRequest = 400;
        public const int Unauthorized = 401;
        public const int Forbidden = 403;
        public const int NotFound = 404;
        public const int Conflict = 409;
        public const int Unprocessable = 422;
    }
}
