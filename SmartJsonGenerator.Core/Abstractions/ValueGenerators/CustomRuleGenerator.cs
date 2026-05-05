namespace SmartJsonGenerator.Core.Abstractions.ValueGenerators;

/// <summary>
/// Property adına göre anlamlı string değerleri üretir (Email, Phone, Url vb.) ve
/// diğer generatorların kapsamadığı temel tipleri (bool, Guid, char, Enum) üstlenir.
/// </summary>
public class CustomRuleGenerator : IValueGenerator
{
    private readonly Random _random = Random.Shared;

    private static readonly string[] FirstNames = ["Alice", "Bob", "Charlie", "Diana", "Eve", "Frank", "Grace", "Hank"];
    private static readonly string[] LastNames  = ["Smith", "Johnson", "Williams", "Brown", "Jones", "Garcia", "Miller"];
    private static readonly string[] Domains    = ["example.com", "mail.com", "test.org", "demo.net"];
    private static readonly string[] Countries  = ["Turkey", "United States", "Germany", "France", "United Kingdom"];
    private static readonly string[] Cities     = ["Istanbul", "Ankara", "New York", "Berlin", "Paris", "London"];

    public bool CanHandle(Type propertyType, string propertyName)
        => propertyType == typeof(bool)
        || propertyType == typeof(Guid)
        || propertyType == typeof(char)
        || propertyType.IsEnum
        || IsNamedStringProperty(propertyType, propertyName);

    public object? GenerateValue(Type propertyType, string propertyName)
    {
        if (propertyType == typeof(bool)) return _random.Next(2) == 1;
        if (propertyType == typeof(Guid)) return Guid.NewGuid();
        if (propertyType == typeof(char)) return (char)_random.Next('a', 'z' + 1);

        if (propertyType.IsEnum)
        {
            var values = Enum.GetValues(propertyType);
            return values.GetValue(_random.Next(values.Length));
        }

        // Property adına göre anlamlı string üretimi
        return GenerateNamedStringValue(propertyName);
    }

    // -------------------------------------------------------------------

    private static bool IsNamedStringProperty(Type propertyType, string propertyName)
    {
        if (propertyType != typeof(string)) return false;

        var name = propertyName.ToLowerInvariant();
        return name.Contains("email")
            || name.Contains("phone") || name.Contains("tel")
            || name.Contains("url")   || name.Contains("website") || name.Contains("link")
            || name.Contains("name")
            || name.Contains("country")
            || name.Contains("city")
            || name.Contains("address")
            || name.Contains("description") || name.Contains("note") || name.Contains("comment")
            || name.Contains("code")        || name.Contains("zip") || name.Contains("postal")
            || name.Contains("ip");
    }

    private string GenerateNamedStringValue(string propertyName)
    {
        var name = propertyName.ToLowerInvariant();
        var firstName = FirstNames[_random.Next(FirstNames.Length)];
        var lastName  = LastNames[_random.Next(LastNames.Length)];
        var domain    = Domains[_random.Next(Domains.Length)];

        if (name.Contains("email"))
            return $"{firstName.ToLower()}.{lastName.ToLower()}@{domain}";

        if (name.Contains("phone") || name.Contains("tel"))
            return $"+90{_random.Next(500, 560)}{_random.Next(1_000_000, 9_999_999)}";

        if (name.Contains("url") || name.Contains("website") || name.Contains("link"))
            return $"https://www.{domain}/{firstName.ToLower()}";

        if (name.Contains("firstname") || name == "name" || name.Contains("givenname"))
            return firstName;

        if (name.Contains("lastname") || name.Contains("surname") || name.Contains("familyname"))
            return lastName;

        if (name.Contains("name"))
            return $"{firstName} {lastName}";

        if (name.Contains("country"))
            return Countries[_random.Next(Countries.Length)];

        if (name.Contains("city"))
            return Cities[_random.Next(Cities.Length)];

        if (name.Contains("address"))
            return $"{_random.Next(1, 999)} {lastName} St, {Cities[_random.Next(Cities.Length)]}";

        if (name.Contains("description") || name.Contains("note") || name.Contains("comment"))
            return $"Sample {propertyName} text {_random.Next(100, 999)}.";

        if (name.Contains("zip") || name.Contains("postal") || name.Contains("code"))
            return _random.Next(10000, 99999).ToString();

        if (name.Contains("ip"))
            return $"{_random.Next(1, 255)}.{_random.Next(0, 255)}.{_random.Next(0, 255)}.{_random.Next(1, 254)}";

        return $"{propertyName}_{Guid.NewGuid().ToString("N")[..8]}";
    }
}

