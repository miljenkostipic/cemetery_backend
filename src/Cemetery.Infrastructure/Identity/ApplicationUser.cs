using Cemetery.Domain.Identity;
using Microsoft.AspNetCore.Identity;

namespace Cemetery.Infrastructure.Identity;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public string DisplayName { get; set; } = "";
}
