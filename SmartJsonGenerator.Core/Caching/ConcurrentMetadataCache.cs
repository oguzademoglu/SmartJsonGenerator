using SmartJsonGenerator.Core.Abstractions;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;

namespace SmartJsonGenerator.Core.Caching;

/// <summary>
/// Thread-safe, process-lifetime cache of pre-compiled type metadata.
/// Each <see cref="TypeMetadata"/> entry is built once via Expression Trees and reused for all subsequent calls.
/// </summary>
public class ConcurrentMetadataCache : IMetaDataCache
{
    private readonly ConcurrentDictionary<Type, TypeMetadata> _cache = new();

    /// <inheritdoc />
    public TypeMetadata GetOrAdd(Type type) => _cache.GetOrAdd(type, BuildMetadata);

    // -------------------------------------------------------------------------
    // Core builder — called once per type, result is cached forever.
    // -------------------------------------------------------------------------

    private static TypeMetadata BuildMetadata(Type type)
    {
        // 1. Best constructor: prefer the one with the most parameters (record / immutable friendly)
        var ctor = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .OrderByDescending(c => c.GetParameters().Length)
            .FirstOrDefault();

        // 2. Map constructor parameters to their matching public property names
        var ctorParams = ctor?.GetParameters()
            .Select(p => new ParameterMetadata(
                p.Name!,
                p.ParameterType,
                ResolveLinkedPropertyName(type, p.Name!)))
            .ToList() ?? [];

        // 3. O(1) lookup set — avoids per-property LINQ Any() in the hot path
        var linkedProps = new HashSet<string>(
            ctorParams.Select(p => p.LinkedPropertyName),
            StringComparer.Ordinal);

        // 4. Writable properties (CanWrite covers both regular and init-only setters)
        var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanWrite)
            .Select(CreatePropertyMetadata)
            .ToList();

        // 5. Compile constructor delegates — executed once, then cached
        Func<object?[], object>? compiledCtor = null;
        Func<object>? compiledParamlessCtor = null;

        if (ctor != null && ctorParams.Count > 0)
            compiledCtor = CompileParameterizedConstructor(ctor);
        else if (ctor != null)
            compiledParamlessCtor = CompileParameterlessConstructor(ctor);
        else if (type.IsValueType)
            compiledParamlessCtor = CompileValueTypeFactory(type);

        bool isImmutable = ctorParams.Count > 0 ||
                           type.GetCustomAttributes().Any(a => a.GetType().Name == "IsReadOnlyAttribute");

        return new TypeMetadata(
            Type: type,
            IsRecordOrImmutable: isImmutable,
            BestConstructor: ctor,
            ConstructorParameters: ctorParams,
            WritableProperties: props,
            ConstructorLinkedProperties: linkedProps,
            CompiledConstructor: compiledCtor,
            CompiledParameterlessConstructor: compiledParamlessCtor
        );
    }

    // -------------------------------------------------------------------------
    // Expression Tree compilers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Compiles: <c>(object?[] args) => (object)new T((T0)args[0], (T1)args[1], ...)</c>
    /// </summary>
    private static Func<object?[], object> CompileParameterizedConstructor(ConstructorInfo ctor)
    {
        var argsParam = Expression.Parameter(typeof(object?[]), "args");

        var ctorArgExpressions = ctor.GetParameters()
            .Select((p, i) => (Expression)Expression.Convert(
                Expression.ArrayIndex(argsParam, Expression.Constant(i)),
                p.ParameterType))
            .ToArray();

        var newExpr = Expression.New(ctor, ctorArgExpressions);
        var body = Expression.Convert(newExpr, typeof(object));
        return Expression.Lambda<Func<object?[], object>>(body, argsParam).Compile();
    }

    /// <summary>
    /// Compiles: <c>() => (object)new T()</c>
    /// </summary>
    private static Func<object> CompileParameterlessConstructor(ConstructorInfo ctor)
    {
        var newExpr = Expression.New(ctor);
        var body = Expression.Convert(newExpr, typeof(object));
        return Expression.Lambda<Func<object>>(body).Compile();
    }

    /// <summary>
    /// Compiles: <c>() => (object)default(T)</c> — for value types (structs) with no explicit constructor.
    /// </summary>
    private static Func<object> CompileValueTypeFactory(Type type)
    {
        var defaultExpr = Expression.Default(type);
        var body = Expression.Convert(defaultExpr, typeof(object));
        return Expression.Lambda<Func<object>>(body).Compile();
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Maps a constructor parameter name to its corresponding public property name (case-insensitive).
    /// e.g. "firstName" → "FirstName"
    /// </summary>
    private static string ResolveLinkedPropertyName(Type type, string paramName)
    {
        return type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .FirstOrDefault(p => p.Name.Equals(paramName, StringComparison.OrdinalIgnoreCase))
            ?.Name ?? paramName;
    }

    private static IPropertyMetadata CreatePropertyMetadata(PropertyInfo prop)
    {
        return new PropertyMetadata
        {
            Name = prop.Name,
            PropertyType = prop.PropertyType,
            Setter = CompileSetter(prop)
        };
    }

    /// <summary>
    /// Compiles: <c>(object target, object? value) => ((TOwner)target).Property = (TProperty)value</c>
    /// Expression Trees bypass the C# compiler's init-only restriction at the IL level.
    /// </summary>
    private static Action<object, object?> CompileSetter(PropertyInfo prop)
    {
        var targetParam = Expression.Parameter(typeof(object), "target");
        var valueParam = Expression.Parameter(typeof(object), "value");

        var castTarget = Expression.Convert(targetParam, prop.DeclaringType!);
        var castValue = Expression.Convert(valueParam, prop.PropertyType);
        var propertyAccess = Expression.Property(castTarget, prop);
        var assign = Expression.Assign(propertyAccess, castValue);

        return Expression.Lambda<Action<object, object?>>(assign, targetParam, valueParam).Compile();
    }

    private sealed record PropertyMetadata : IPropertyMetadata
    {
        public required string Name { get; init; }
        public required Type PropertyType { get; init; }
        public required Action<object, object?> Setter { get; init; }
    }
}
