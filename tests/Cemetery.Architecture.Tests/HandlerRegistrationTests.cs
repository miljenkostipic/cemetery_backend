using Cemetery.Application.Abstractions;
using Cemetery.Application.Auth;
using Cemetery.Application.Validation;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Cemetery.Architecture.Tests;

public sealed class HandlerRegistrationTests : IClassFixture<CemeteryApiFactory>
{
    private readonly CemeteryApiFactory _factory;

    public HandlerRegistrationTests(CemeteryApiFactory factory) => _factory = factory;

    [Fact]
    public void Every_handler_resolves_and_every_command_has_a_validator()
    {
        var handlers = HandlerTypes().ToArray();
        Assert.NotEmpty(handlers);
        using var scope = _factory.Services.CreateScope();
        var commands = 0;
        foreach (var handler in handlers)
        {
            AssertRegistered(scope.ServiceProvider, handler);
            if (CommandInterface(handler) is not null)
                commands++;
        }

        Assert.True(commands > 0);
    }

    private static void AssertRegistered(IServiceProvider provider, Type handler)
    {
        Assert.NotNull(provider.GetRequiredService(handler));
        var command = CommandInterface(handler);
        if (command is null)
            return;

        var arguments = command.GetGenericArguments();
        Assert.NotNull(provider.GetRequiredService(typeof(IValidator<>).MakeGenericType(arguments[0])));
        var decorated = typeof(ValidatingCommandHandler<,>).MakeGenericType(arguments);
        Assert.IsType(decorated, provider.GetRequiredService(command));
    }

    private static IEnumerable<Type> HandlerTypes() =>
        typeof(RegisterUserHandler).Assembly.GetTypes().Where(IsConcreteHandler);

    private static bool IsConcreteHandler(Type type) =>
        type is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false }
        && type.GetInterfaces().Any(IsHandlerInterface);

    private static Type? CommandInterface(Type handler) =>
        handler.GetInterfaces().SingleOrDefault(type =>
            type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ICommandHandler<,>));

    private static bool IsHandlerInterface(Type type) =>
        type.IsGenericType && type.GetGenericTypeDefinition() is var open
        && (open == typeof(ICommandHandler<,>) || open == typeof(IQueryHandler<,>));
}
