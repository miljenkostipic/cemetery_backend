using System.Text.Json;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Cemetery.Infrastructure.Persistence;

public sealed class SnakeCaseEnumConverter<TEnum> : ValueConverter<TEnum, string>
    where TEnum : struct, Enum
{
    public SnakeCaseEnumConverter()
        : base(static value => ToSnake(value), static text => FromSnake(text))
    {
    }

    private static string ToSnake(TEnum value) =>
        JsonNamingPolicy.SnakeCaseLower.ConvertName(value.ToString() ?? "");

    private static TEnum FromSnake(string text)
    {
        foreach (var name in Enum.GetNames<TEnum>())
        {
            if (JsonNamingPolicy.SnakeCaseLower.ConvertName(name) == text)
                return Enum.Parse<TEnum>(name);
        }

        throw new InvalidOperationException($"Unknown {typeof(TEnum).Name} value '{text}'.");
    }
}
