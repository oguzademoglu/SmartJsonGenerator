namespace SmartJsonGenerator.Core.Abstractions;

/// <summary>Contract for leaf-level value generators (string, numeric, date/time, etc.).</summary>
public interface IValueGenerator
{
    /// <summary>
    /// Returns <see langword="true"/> when this generator can produce a value for the given
    /// <paramref name="propertyType"/> and optional <paramref name="propertyName"/> hint.
    /// </summary>
    bool CanHandle(Type propertyType, string propertyName);

    /// <summary>Produces a single random value compatible with <paramref name="propertyType"/>.</summary>
    object? GenerateValue(Type propertyType, string propertyName);
}
