namespace SmartJsonGenerator.Core.Abstractions;

public interface IValueGenerator
{
    // Bu generator, ilgili property tipi ve ismi için veri üretebilir mi?
    // Örn: Property adı "Email" ve tipi "string" ise StringValueGenerator bunu üstlenir.
    bool CanHandle(Type propertyType, string propertyName);

    // Rastgele değeri üret
    object? GenerateValue(Type propertyType, string propertyName);
}
