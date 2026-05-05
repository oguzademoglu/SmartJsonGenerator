using System.Reflection;

namespace SmartJsonGenerator.Core.Abstractions;

public interface IMetaDataCache
{
    TypeMetadata GetOrAdd(Type type);
}

public record TypeMetadata(
    Type Type,
    bool IsRecordOrImmutable,
    ConstructorInfo BestConstructor,
    IReadOnlyList<ParameterMetadata> ConstructorParameters,
    IReadOnlyList<IPropertyMetadata> WritableProperties
);

public record ParameterMetadata(string Name, Type ParameterType, string LinkedPropertyName);

public interface IPropertyMetadata
{
    string Name { get; }
    Type PropertyType { get; }
    Action<object, object?> Setter { get; }
}

