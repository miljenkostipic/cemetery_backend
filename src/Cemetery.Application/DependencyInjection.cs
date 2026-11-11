using Cemetery.Application.Abstractions;
using Cemetery.Application.Auth;
using Cemetery.Application.Integrations;
using Cemetery.Application.Launch;
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
        services.Scan(scan => scan
            .FromAssemblyOf<RegisterUserHandler>()
            .AddClasses(Handlers)
            .AsSelf()
            .AsImplementedInterfaces()
            .WithScopedLifetime());
        services.Decorate(typeof(ICommandHandler<,>), typeof(ValidatingCommandHandler<,>));
        services.AddScoped<OutboxDispatcher>();
        services.AddScoped<IOutboxHandler, WebhookDeliveryHandler>();
        return services;
    }

    private static void Handlers(Scrutor.IImplementationTypeFilter filter) =>
        filter.AssignableToAny(typeof(ICommandHandler<,>), typeof(IQueryHandler<,>))
            .Where(type => !type.IsGenericTypeDefinition);
}
