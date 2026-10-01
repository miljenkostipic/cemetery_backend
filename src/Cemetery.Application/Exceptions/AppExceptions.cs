namespace Cemetery.Application.Exceptions;

public sealed class ValidationFailedException(IReadOnlyList<string> errorCodes) : Exception(Code)
{
    public const string Code = "validation.failed";

    public IReadOnlyList<string> ErrorCodes { get; } = errorCodes;
}

public sealed class UnauthorizedException(string code) : Exception(code)
{
    public string Code { get; } = code;
}

public sealed class ForbiddenException(string code) : Exception(code)
{
    public string Code { get; } = code;
}

public sealed class NotFoundException(string code) : Exception(code)
{
    public string Code { get; } = code;
}

public sealed class ConflictException(string code) : Exception(code)
{
    public string Code { get; } = code;
}
