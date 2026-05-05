namespace SmartJsonGenerator.Core.Abstractions.ValueGenerators;

/// <summary>Generates random string values for <see langword="string"/> properties.</summary>
public class StringValueGenerator : IValueGenerator
{
    /// <inheritdoc />
    public bool CanHandle(Type propertyType, string propertyName) => propertyType == typeof(string);

    /// <inheritdoc />
    public object? GenerateValue(Type propertyType, string propertyName)
    {
        // İleride buraya Property ismine göre (örn: "Email", "FirstName") akıllı mantıklar (Faker) eklenebilir.
        return $"{propertyName}_{Guid.NewGuid().ToString("N")[..8]}";
    }
}

