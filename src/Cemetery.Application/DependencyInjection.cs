using Cemetery.Application.Abstractions;
using Cemetery.Application.Auth;
using Cemetery.Application.Dashboard;
using Cemetery.Application.Layout;
using Cemetery.Application.Organizations;
using Cemetery.Application.Catalog;
using Cemetery.Application.Register;
using Cemetery.Application.Operations;
using Cemetery.Application.Rights;
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
        Register<RequestPasswordReset, PasswordResetDelivery, RequestPasswordResetHandler>(services);
        Register<ConfirmPasswordReset, UserProfile, ConfirmPasswordResetHandler>(services);
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
        Register<RecordDeceased, DeceasedView, RecordDeceasedHandler>(services);
        Register<CorrectDeceased, DeceasedView, CorrectDeceasedHandler>(services);
        Register<RemoveDeceased, bool, RemoveDeceasedHandler>(services);
        Register<SetRestPeriod, int, SetRestPeriodHandler>(services);
        Register<RecordInterment, IntermentView, RecordIntermentHandler>(services);
        Register<ExhumeInterment, IntermentView, ExhumeIntermentHandler>(services);
        Register<TransferInterment, IntermentView, TransferIntermentHandler>(services);
        services.AddScoped<IQueryHandler<ListDeceased, IReadOnlyList<DeceasedView>>, ListDeceasedHandler>();
        services.AddScoped<IQueryHandler<GetDeceased, DeceasedView>, GetDeceasedHandler>();
        services.AddScoped<IQueryHandler<ListFreePositions, IReadOnlyList<FreePositionView>>, ListFreePositionsHandler>();
        services.AddScoped<IQueryHandler<ListSiteInterments, IReadOnlyList<IntermentView>>, ListSiteIntermentsHandler>();
        services.AddScoped<IQueryHandler<SearchPublic, IReadOnlyList<PublicSearchHit>>, SearchPublicHandler>();
        services.AddScoped<IQueryHandler<GetMemorial, PublicMemorial>, GetMemorialHandler>();
        services.AddScoped<IQueryHandler<GetPublicMap, PublicMap>, GetPublicMapHandler>();
        services.AddScoped<IQueryHandler<ListPublicCemeteries, IReadOnlyList<PublicCemeterySummary>>, ListPublicCemeteriesHandler>();
        services.AddScoped<IQueryHandler<GetVisibility, VisibilityView>, GetVisibilityHandler>();
        Register<PublishRegister, VisibilityView, PublishRegisterHandler>(services);
        Register<WithdrawRegister, VisibilityView, WithdrawRegisterHandler>(services);
        Register<HideGraveFromPublic, GraveSiteView, HideGraveFromPublicHandler>(services);
        Register<ShowGraveOnPublicMap, GraveSiteView, ShowGraveOnPublicMapHandler>(services);
        Register<DescribeMemorial, DeceasedView, DescribeMemorialHandler>(services);
        services.AddScoped<IQueryHandler<ListOfficeRequests, IReadOnlyList<OfficeRequestView>>, ListOfficeRequestsHandler>();
        services.AddScoped<IQueryHandler<ListMyOfficeRequests, IReadOnlyList<OfficeRequestView>>, ListMyOfficeRequestsHandler>();
        Register<SendOfficeRequest, OfficeRequestView, SendOfficeRequestHandler>(services);
        Register<ScheduleFuneral, FuneralView, ScheduleFuneralHandler>(services);
        Register<CancelFuneral, FuneralView, CancelFuneralHandler>(services);
        Register<BookChapel, ChapelBookingView, BookChapelHandler>(services);
        Register<OpenWorkOrder, WorkOrderView, OpenWorkOrderHandler>(services);
        Register<IssuePermit, MonumentPermitView, IssuePermitHandler>(services);
        Register<CompleteWorkOrder, CompletionView, CompleteWorkOrderHandler>(services);
        Register<AttachWorkOrderPhoto, PhotoView, AttachWorkOrderPhotoHandler>(services);
        Register<RemoveFuneral, bool, RemoveFuneralHandler>(services);
        Register<RemoveChapelBooking, bool, RemoveChapelBookingHandler>(services);
        Register<RemoveWorkOrder, bool, RemoveWorkOrderHandler>(services);
        Register<RemoveMonumentPermit, bool, RemoveMonumentPermitHandler>(services);
        services.AddScoped<IQueryHandler<ListFunerals, IReadOnlyList<FuneralView>>, ListFuneralsHandler>();
        services.AddScoped<IQueryHandler<ListChapelBookings, IReadOnlyList<ChapelBookingView>>, ListChapelBookingsHandler>();
        services.AddScoped<IQueryHandler<ListWorkOrders, IReadOnlyList<WorkOrderView>>, ListWorkOrdersHandler>();
        services.AddScoped<IQueryHandler<ListPermits, IReadOnlyList<MonumentPermitView>>, ListPermitsHandler>();
        services.AddScoped<IQueryHandler<ListFieldTasks, IReadOnlyList<FieldTaskView>>, ListFieldTasksHandler>();
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
