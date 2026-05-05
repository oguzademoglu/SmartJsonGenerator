namespace SmartJsonGenerator.Core.Abstractions.ValueGenerators;

public class StringValueGenerator : IValueGenerator
{
    public bool CanHandle(Type propertyType, string propertyName) => propertyType == typeof(string);

    public object? GenerateValue(Type propertyType, string propertyName)
    {
        // İleride buraya Property ismine göre (örn: "Email", "FirstName") akıllı mantıklar (Faker) eklenebilir.
        return $"{propertyName}_{Guid.NewGuid().ToString("N")[..8]}";
    }
}

