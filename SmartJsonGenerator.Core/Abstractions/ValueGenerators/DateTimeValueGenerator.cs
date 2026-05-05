namespace SmartJsonGenerator.Core.Abstractions.ValueGenerators;

public class DateTimeValueGenerator : IValueGenerator
{
    private readonly Random _random = Random.Shared;

    public bool CanHandle(Type propertyType, string propertyName)
        => propertyType == typeof(DateTime)
           || propertyType == typeof(DateTimeOffset)
           || propertyType == typeof(DateOnly)
           || propertyType == typeof(TimeOnly)
           || propertyType == typeof(TimeSpan);

    public object? GenerateValue(Type propertyType, string propertyName)
    {
        var daysOffset = _random.Next(-3650, 3650);
        var baseDate = DateTime.UtcNow.AddDays(daysOffset);

        if (propertyType == typeof(DateTime)) return baseDate;
        if (propertyType == typeof(DateTimeOffset)) return new DateTimeOffset(baseDate);
        if (propertyType == typeof(DateOnly)) return DateOnly.FromDateTime(baseDate);
        if (propertyType == typeof(TimeOnly))
            return new TimeOnly(_random.Next(0, 24), _random.Next(0, 60), _random.Next(0, 60));
        if (propertyType == typeof(TimeSpan)) return TimeSpan.FromSeconds(_random.Next(0, 86400));

        return DateTime.UtcNow;
    }
}

