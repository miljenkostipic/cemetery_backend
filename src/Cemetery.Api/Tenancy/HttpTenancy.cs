using System.Security.Claims;
using Cemetery.Application.Abstractions;

namespace Cemetery.Api.Tenancy;

public sealed class HttpCurrentUser(IHttpContextAccessor http) : ICurrentUser
{
    public Guid? UserId
    {
        get
        {
            var value = http.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(value, out var id) ? id : null;
        }
    }

    public string? Email =>
        http.HttpContext?.User.FindFirst(ClaimTypes.Email)?.Value
        ?? http.HttpContext?.User.FindFirst(ClaimTypes.Name)?.Value;
}

public sealed class CookieOrganizationStore(IHttpContextAccessor http) : IActiveOrganizationStore
{
    public const string Name = "cemetery.organization";
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(30);

    public void Remember(Guid organizationId)
    {
        var context = http.HttpContext ?? throw new InvalidOperationException("No HTTP context.");
        context.Response.Cookies.Append(Name, organizationId.ToString(), new CookieOptions
        {
            HttpOnly = true,
            IsEssential = true,
            SameSite = SameSiteMode.Lax,
            Secure = context.Request.IsHttps,
            MaxAge = Lifetime,
        });
    }
}
