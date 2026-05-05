using SmartJsonGenerator.Core.Abstractions;
using SmartJsonGenerator.Core.Configuration;
using System.Buffers;
using System.Collections;
using System.Collections.Concurrent;
using System.Text.Json;

namespace SmartJsonGenerator.Core.Core;

/// <summary>
/// Core implementation of <see cref="ISmartJsonGenerator"/>.
/// Generates populated object graphs and serializes them to JSON using
/// Expression-tree-compiled delegates to avoid raw reflection in hot paths.
/// </summary>
public class SmartGenerator : ISmartJsonGenerator
{
    private readonly IMetaDataCache _cache;
    private readonly IValueGenerator[] _valueGenerators;
    private readonly SmartJsonOptions _options;

    // Caches the resolved generator per Type (null = no generator handles this type).
    // TryGetValue returns true even when the stored value is null, enabling O(1) negative caching.
    private readonly ConcurrentDictionary<Type, IValueGenerator?> _generatorCache = new();

    /// <summary>
    /// Initializes a new <see cref="SmartGenerator"/> instance.
    /// </summary>
    /// <param name="cache">Metadata cache that stores pre-compiled type information.</param>
    /// <param name="valueGenerators">Optional set of leaf-value generators (string, numeric, DateTime…).</param>
    /// <param name="options">Generation options such as max depth and collection size.</param>
    public SmartGenerator(IMetaDataCache cache, IEnumerable<IValueGenerator>? valueGenerators = null,
        SmartJsonOptions? options = null)
    {
        _cache = cache;
        _valueGenerators = (valueGenerators ?? []).ToArray();
        _options = options ?? new SmartJsonOptions();
    }

    /// <inheritdoc />
    public string GenerateJson<T>()
    {
        var instance = Generate<T>();
        return JsonSerializer.Serialize(instance);
    }

    /// <inheritdoc />
    public string GenerateJson<T>(int count)
    {
        var writerOptions = new JsonWriterOptions { Indented = true };
        var output = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(output, writerOptions);

        if (count > 1) writer.WriteStartArray();

        for (int i = 0; i < count; i++)
        {
            var instance = Generate<T>();
            JsonSerializer.Serialize(writer, instance);
        }

        if (count > 1) writer.WriteEndArray();

        writer.Flush();
        return System.Text.Encoding.UTF8.GetString(output.WrittenSpan);
    }

    /// <inheritdoc />
    public T Generate<T>()
    {
        var typeStack = new HashSet<Type>();
        return (T)GenerateInternal(typeof(T), 0, typeStack)!;
    }

    /// <inheritdoc />
    public IEnumerable<T> GenerateMany<T>(int count)
    {
        for (int i = 0; i < count; i++)
            yield return Generate<T>();
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<T> GenerateManyAsync<T>(int count,
        [System.Runtime.CompilerServices.EnumeratorCancellation]
        CancellationToken cancellationToken = default)
    {
        for (int i = 0; i < count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return Generate<T>();
        }
    }

    // -------------------------------------------------------------------------
    // Hot-path generator lookup — result cached per Type in ConcurrentDictionary.
    // Linear scan of _valueGenerators runs only on the first encounter of each type.
    // -------------------------------------------------------------------------

    private IValueGenerator? FindGenerator(Type type)
    {
        if (_generatorCache.TryGetValue(type, out var cached))
            return cached;

        IValueGenerator? found = null;
        for (int i = 0; i < _valueGenerators.Length; i++)
        {
            if (_valueGenerators[i].CanHandle(type, string.Empty))
            {
                found = _valueGenerators[i];
                break;
            }
        }

        // TryAdd is safe under concurrency; a duplicate add from another thread is harmless.
        _generatorCache.TryAdd(type, found);
        return found;
    }

    // -------------------------------------------------------------------------
    // Collection handler — passes the SAME typeStack reference (no copies).
    // Each element's recursive call manages its own backtracking internally.
    // -------------------------------------------------------------------------

    private bool TryGenerateCollection(Type type, int depth, HashSet<Type> typeStack, out object? result)
    {
        result = null;

        // string implements IEnumerable but is not a collection.
        if (type == typeof(string)) return false;

        if (type.IsArray)
        {
            var elementType = type.GetElementType()!;
            var count = _options.DefaultCollectionSize;
            var array = Array.CreateInstance(elementType, count);

            for (int i = 0; i < count; i++)
                array.SetValue(GenerateInternal(elementType, depth + 1, typeStack), i);

            result = array;
            return true;
        }

        if (type.IsGenericType && typeof(IDictionary).IsAssignableFrom(type))
        {
            var genericArgs = type.GetGenericArguments();
            var dict = (IDictionary)Activator.CreateInstance(type)!;

            for (int i = 0; i < _options.DefaultCollectionSize; i++)
            {
                var k = GenerateInternal(genericArgs[0], depth + 1, typeStack);
                var v = GenerateInternal(genericArgs[1], depth + 1, typeStack);
                if (k != null) dict.Add(k, v);
            }

            result = dict;
            return true;
        }

        if (type.IsGenericType && typeof(IEnumerable).IsAssignableFrom(type))
        {
            var elementType = type.GetGenericArguments()[0];
            var listType = typeof(List<>).MakeGenericType(elementType);
            var list = (IList)Activator.CreateInstance(listType)!;

            for (int i = 0; i < _options.DefaultCollectionSize; i++)
                list.Add(GenerateInternal(elementType, depth + 1, typeStack));

            result = list;
            return true;
        }

        return false;
    }

    // -------------------------------------------------------------------------
    // Recursive core — single HashSet<Type> reference flows through the entire
    // call tree. Backtracking (try-finally-remove) keeps the stack clean even
    // when exceptions occur, preventing false circular-reference detections on
    // subsequent calls within the same Generate<T>() session.
    // -------------------------------------------------------------------------

    private object? GenerateInternal(Type type, int depth, HashSet<Type> typeStack,
        Type? containerType = null, string? propertyName = null)
    {
        if (depth > _options.MaxDepth) return null;

        // Per-type property override (highest priority — fluent RuleFor API)
        if (containerType != null && propertyName != null &&
            _options.Rules.TryGetValue(containerType, out var typeConfig) &&
            typeConfig.PropertyRules.TryGetValue(propertyName, out var customFactory))
        {
            return customFactory();
        }

        // Global naming rule override (project-wide conventions, case-insensitive key match)
        if (!string.IsNullOrEmpty(propertyName) &&
            _options.GlobalNamingRules.TryGetValue(propertyName, out var namingFactory))
        {
            return namingFactory();
        }

        // Primitive / leaf types: resolved via O(1) cached lookup
        var generator = FindGenerator(type);
        if (generator != null)
            return generator.GenerateValue(type, propertyName ?? string.Empty);

        // Collection types: array, dictionary, list
        if (TryGenerateCollection(type, depth, typeStack, out var collectionResult))
            return collectionResult;

        // Circular reference guard — value types (int, Guid, struct…) are never
        // added to the stack: they cannot form circular references and the overhead
        // would cause false positives.
        bool addedToStack = false;
        if (!type.IsValueType && type != typeof(string))
        {
            if (typeStack.Contains(type)) return null;
            typeStack.Add(type);
            addedToStack = true;
        }

        var metadata = _cache.GetOrAdd(type);
        object? instance;

        try
        {
            // Instantiation via Expression-tree-compiled delegates (no raw reflection).
            // ArrayPool reduces heap pressure for the temporary args array; safe because
            // CompiledConstructor reads all elements before we return the array to the pool.
            if (metadata.CompiledConstructor != null)
            {
                var count = metadata.ConstructorParameters.Count;
                var args = ArrayPool<object?>.Shared.Rent(count);
                try
                {
                    for (int i = 0; i < count; i++)
                    {
                        var p = metadata.ConstructorParameters[i];
                        args[i] = GenerateInternal(p.ParameterType, depth + 1, typeStack, type, p.LinkedPropertyName);
                    }
                    instance = metadata.CompiledConstructor(args);
                }
                finally
                {
                    ArrayPool<object?>.Shared.Return(args, clearArray: true);
                }
            }
            else if (metadata.CompiledParameterlessConstructor != null)
            {
                instance = metadata.CompiledParameterlessConstructor();
            }
            else
            {
                // Abstract type or interface — nothing to instantiate.
                return null;
            }

            // Property population — O(1) HashSet lookup replaces LINQ Any() in the loop
            foreach (var prop in metadata.WritableProperties)
            {
                if (metadata.ConstructorLinkedProperties.Contains(prop.Name)) continue;

                var value = GenerateInternal(prop.PropertyType, depth + 1, typeStack, type, prop.Name);
                prop.Setter(instance, value);
            }

            return instance;
        }
        catch
        {
            // Abstract types, interfaces, or exotic constructors — return null gracefully.
            return null;
        }
        finally
        {
            // Backtracking: always remove the type we added, even if an exception occurred.
            // This guarantees the shared typeStack is never left in a dirty state.
            if (addedToStack)
                typeStack.Remove(type);
        }
    }
}
