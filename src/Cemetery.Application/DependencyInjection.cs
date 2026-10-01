using Cemetery.Application.Abstractions;
using Cemetery.Application.Auth;
using Cemetery.Application.Dashboard;
using Cemetery.Application.Organizations;
using Cemetery.Application.Validation;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Cemetery.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssemblyContaining<RegisterUserValidator>();
        services.AddSingleton<TimeProvider>(TimeProvider.System);
        Register<RegisterUser, UserProfile, RegisterUserHandler>(services);
        Register<LoginUser, UserProfile, LoginUserHandler>(services);
        Register<CreateOrganization, OrganizationCreated, CreateOrganizationHandler>(services);
        Register<InviteMember, InvitationIssued, InviteMemberHandler>(services);
        Register<AcceptInvitation, OrganizationSummary, AcceptInvitationHandler>(services);
        Register<SwitchOrganization, OrganizationSummary, SwitchOrganizationHandler>(services);
        services.AddScoped<IQueryHandler<ListMyOrganizations, IReadOnlyList<OrganizationSummary>>, ListMyOrganizationsHandler>();
        services.AddScoped<IQueryHandler<GetDashboard, DashboardView>, GetDashboardHandler>();
        services.AddScoped<IQueryHandler<GetSession, SessionView>, GetSessionHandler>();
        return services;
    }

    private static void Register<TCommand, TResult, THandler>(IServiceCollection services)
        where THandler : class, ICommandHandler<TCommand, TResult>
    {
        services.AddScoped<THandler>();
        services.AddScoped<ICommandHandler<TCommand, TResult>>(provider =>
            new ValidatingCommandHandler<TCommand, TResult>(
                provider.GetRequiredService<THandler>(),
                provider.GetRequiredService<IValidator<TCommand>>()));
    }
}
