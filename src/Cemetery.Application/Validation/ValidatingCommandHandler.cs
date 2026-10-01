using Cemetery.Application.Abstractions;
using Cemetery.Application.Exceptions;
using FluentValidation;

namespace Cemetery.Application.Validation;

public sealed class ValidatingCommandHandler<TCommand, TResult>(
    ICommandHandler<TCommand, TResult> inner,
    IValidator<TCommand> validator) : ICommandHandler<TCommand, TResult>
{
    public async Task<TResult> Handle(TCommand command, CancellationToken cancellationToken)
    {
        var result = await validator.ValidateAsync(command, cancellationToken).ConfigureAwait(false);
        if (!result.IsValid)
            throw new ValidationFailedException(result.Errors.Select(error => error.ErrorCode).ToArray());

        return await inner.Handle(command, cancellationToken).ConfigureAwait(false);
    }
}
