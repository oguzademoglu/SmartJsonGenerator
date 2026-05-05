namespace SmartJsonGenerator.Core.Abstractions.ValueGenerators;

/// <summary>Generates random numeric values for all built-in numeric types.</summary>
public class NumericValueGenerator : IValueGenerator
{
    private readonly Random _random = Random.Shared;

    /// <inheritdoc />
    public bool CanHandle(Type propertyType, string propertyName)
        => propertyType == typeof(int)
        || propertyType == typeof(long)
        || propertyType == typeof(short)
        || propertyType == typeof(byte)
        || propertyType == typeof(float)
        || propertyType == typeof(double)
        || propertyType == typeof(decimal);

    /// <inheritdoc />
    public object? GenerateValue(Type propertyType, string propertyName)
    {
        if (propertyType == typeof(int))     return _random.Next(1, 1000);
        if (propertyType == typeof(long))    return (long)_random.Next(1, 1_000_000);
        if (propertyType == typeof(short))   return (short)_random.Next(1, 1000);
        if (propertyType == typeof(byte))    return (byte)_random.Next(0, 256);
        if (propertyType == typeof(float))   return (float)(_random.NextDouble() * 1000);
        if (propertyType == typeof(double))  return _random.NextDouble() * 1000;
        if (propertyType == typeof(decimal)) return (decimal)(_random.NextDouble() * 1000);

        return 0;
    }
}

