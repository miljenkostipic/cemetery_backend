using Cemetery.Application.Abstractions;
using Cemetery.Application.Auth;
using Cemetery.Application.Dashboard;
using Cemetery.Application.Layout;
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
        Register<OpenCemetery, CemeteryView, OpenCemeteryHandler>(services);
        Register<SetCemeteryPlan, CemeteryView, SetCemeteryPlanHandler>(services);
        Register<AddSection, SectionView, AddSectionHandler>(services);
        Register<RemoveSection, bool, RemoveSectionHandler>(services);
        Register<ReplaceSectionOutline, SectionView, ReplaceSectionOutlineHandler>(services);
        Register<AddGraveRow, GraveRowView, AddGraveRowHandler>(services);
        Register<GenerateGraveSites, IReadOnlyList<GraveSiteView>, GenerateGraveSitesHandler>(services);
        Register<ReplaceGraveSiteOutline, GraveSiteView, ReplaceGraveSiteOutlineHandler>(services);
        Register<SplitGraveSite, IReadOnlyList<GraveSiteView>, SplitGraveSiteHandler>(services);
        Register<MergeGraveSites, GraveSiteView, MergeGraveSitesHandler>(services);
        Register<CloseGraveSite, GraveSiteView, CloseGraveSiteHandler>(services);
        Register<ReopenGraveSite, GraveSiteView, ReopenGraveSiteHandler>(services);
        Register<UndoGraveSiteSplit, GraveSiteView, UndoGraveSiteSplitHandler>(services);
        services.AddScoped<IQueryHandler<ListCemeteries, IReadOnlyList<CemeteryView>>, ListCemeteriesHandler>();
        services.AddScoped<IQueryHandler<GetCemetery, CemeteryView>, GetCemeteryHandler>();
        services.AddScoped<IQueryHandler<ListSections, IReadOnlyList<SectionView>>, ListSectionsHandler>();
        services.AddScoped<IQueryHandler<ListGraveRows, IReadOnlyList<GraveRowView>>, ListGraveRowsHandler>();
        services.AddScoped<IQueryHandler<ListGraveSites, IReadOnlyList<GraveSiteView>>, ListGraveSitesHandler>();
        services.AddScoped<IQueryHandler<GetGraveSite, GraveSiteView>, GetGraveSiteHandler>();
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
