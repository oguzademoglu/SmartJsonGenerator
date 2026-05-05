using SmartJsonGenerator.Core.Abstractions;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;


namespace SmartJsonGenerator.Core.Caching;

public class ConcurrentMetadataCache : IMetaDataCache
{
    private readonly ConcurrentDictionary<Type, TypeMetadata> _cache = new();

    public TypeMetadata GetOrAdd(Type type)
    {
        return _cache.GetOrAdd(type, t =>
        {
            // 1. En uygun constructor'ı bul (En çok parametresi olan)
            var ctor = t.GetConstructors()
                .OrderByDescending(c => c.GetParameters().Length)
                .FirstOrDefault() ?? throw new Exception($"{t.Name} has no constructor!");

            var ctorParams = ctor.GetParameters().Select(p => new ParameterMetadata(
                p.Name!,
                p.ParameterType,
                GetMatchingPropertyName(t, p.Name!)
            )).ToList();

            // 2. Yazılabilir property'leri ayıkla (Init-only dahil)
            var props = t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(CreatePropertyMetadata)
                .ToList();

            bool isImmutable = t.GetCustomAttributes().Any(a => a.GetType().Name == "IsReadOnlyAttribute")
                               || ctorParams.Count > 0;

            return new TypeMetadata(t, isImmutable, ctor, ctorParams, props);
        });
    }

    private string GetMatchingPropertyName(Type t, string paramName)
    {
        // Parameter: "firstName" -> Property: "FirstName" eşleştirmesi
        return t.GetProperties().FirstOrDefault(p =>
            p.Name.Equals(paramName, StringComparison.OrdinalIgnoreCase))?.Name ?? paramName;
    }

    private TypeMetadata CreateMetadata(Type type)
    {
        // 1. En uygun (en çok parametreli) constructor'ı seç
        var bestCtor = type.GetConstructors()
            .OrderByDescending(c => c.GetParameters().Length)
            .FirstOrDefault();

        if (bestCtor == null && !type.IsValueType)
            throw new InvalidOperationException($"{type.Name} tipinin uygun bir constructor'ı bulunamadı.");

        // 2. Constructor parametrelerini haritala
        var ctorParams = bestCtor?.GetParameters().Select(p => new ParameterMetadata(
            p.Name!,
            p.ParameterType,
            GetMatchingPropertyName(type, p.Name!) // Parametre adını property ile eşleştir
        )).ToList() ?? new List<ParameterMetadata>();

        // 3. Yazılabilir property'leri bul
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(CreatePropertyMetadata)
            .ToList();

        // 4. Immutable/Record tespiti (Basit kontrol: Parametreli constructor varsa immutable dostu kabul et)
        bool isImmutable = ctorParams.Count > 0 ||
                           type.GetCustomAttributes().Any(a => a.GetType().Name == "IsReadOnlyAttribute");

        return new TypeMetadata(
            Type: type,
            IsRecordOrImmutable: isImmutable,
            BestConstructor: bestCtor!,
            ConstructorParameters: ctorParams,
            WritableProperties: properties
        );
    }

    private static Func<object> CompileConstructor(Type type)
    {
        // Value Type (struct) veya parametresiz constructor'ı olmayan sınıflar için fallback
        var ctor = type.GetConstructor(Type.EmptyTypes);
        if (ctor == null)
        {
            return type.IsValueType
                ? () => Activator.CreateInstance(type)! // Struct'lar için zorunlu activator
                : throw new InvalidOperationException($"'{type.Name}' has no parameterless constructor.");
        }

        var newExp = Expression.New(ctor);
        return Expression.Lambda<Func<object>>(newExp).Compile();
    }

    private IPropertyMetadata CreatePropertyMetadata(PropertyInfo propertyInfo)
    {
        return new PropertyMetadata
        {
            Name = propertyInfo.Name,
            PropertyType = propertyInfo.PropertyType,
            Setter = CompileSetter(propertyInfo)
        };
    }

    private static Action<object, object?> CompileSetter(PropertyInfo propertyInfo)
    {
        var targetExp = Expression.Parameter(typeof(object), "target");
        var valueExp = Expression.Parameter(typeof(object), "value");

        // ((TTarget)target)
        var castTargetExp = Expression.Convert(targetExp, propertyInfo.DeclaringType!);

        // ((TProperty)value) - unboxing / cast
        var castValueExp = Expression.Convert(valueExp, propertyInfo.PropertyType);

        // target.Property = value
        var propertyAccessExp = Expression.Property(castTargetExp, propertyInfo);
        var assignExp = Expression.Assign(propertyAccessExp, castValueExp);

        return Expression.Lambda<Action<object, object?>>(assignExp, targetExp, valueExp).Compile();
    }

    // İç kullanım için immutable record
    private record PropertyMetadata : IPropertyMetadata
    {
        public required string Name { get; init; }
        public required Type PropertyType { get; init; }
        public required Action<object, object?> Setter { get; init; }
    }
}

