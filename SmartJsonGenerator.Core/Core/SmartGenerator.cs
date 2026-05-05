using SmartJsonGenerator.Core.Abstractions;
using SmartJsonGenerator.Core.Configuration;
using System.Buffers;
using System.Collections;
using System.Text.Json;

namespace SmartJsonGenerator.Core.Core;

public class SmartGenerator : ISmartJsonGenerator
{
    private readonly IMetaDataCache _cache;
    private readonly IEnumerable<IValueGenerator> _valueGenerators;
    private readonly SmartJsonOptions _options;

    public SmartGenerator(IMetaDataCache cahce, IEnumerable<IValueGenerator>? valueGenerators = null,
        SmartJsonOptions? options = null)
    {
        _cache = cahce;
        _valueGenerators = valueGenerators ?? new List<IValueGenerator>();
        _options = options ?? new SmartJsonOptions();
    }

    public string GenerateJson<T>()
    {
        var instance = Generate<T>();
        return JsonSerializer.Serialize(instance);
    }

    public string GenerateJson<T>(int count = 1)
    {
        var options = new JsonWriterOptions { Indented = true };
        var output = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(output, options);

        if (count > 1) writer.WriteStartArray();

        for (int i = 0; i < count; i++)
        {
            // Nesneyi üret ve doğrudan writer'a serialize et
            var instance = Generate<T>();
            JsonSerializer.Serialize(writer, instance);
        }

        if (count > 1) writer.WriteEndArray();

        writer.Flush();
        return System.Text.Encoding.UTF8.GetString(output.WrittenSpan);
    }

    public T Generate<T>()
    {
        // Instance değil, Type bazlı takip (Backtracking için)
        var typeStack = new HashSet<Type>();
        return (T)GenerateInternal(typeof(T), 0, typeStack);
    }

    public IEnumerable<T> GenerateMany<T>(int count)
    {
        for (int i = 0; i < count; i++)
        {
            yield return Generate<T>();
        }
    }

    public async IAsyncEnumerable<T> GenerateManyAsync<T>(int count,
        [System.Runtime.CompilerServices.EnumeratorCancellation]
        CancellationToken cancellationToken = default)
    {
        for (int i = 0; i < count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Asenkron akışta CPU bound işlemi blocklamamak için Task.Run kullanılabilir 
            // ancak şimdilik memory-friendly IAsyncEnumerable patternini kuruyoruz.
            yield return Generate<T>();
        }
    }

    private bool TryGenerateCollection(Type type, int depth, HashSet<Type> typeStack, out object? result)
    {
        result = null;

        // 1. String bir IEnumerable'dır ama biz onu koleksiyon olarak işlemeyiz.
        if (type == typeof(string)) return false;

        // 2. Array Kontrolü (T[])
        if (type.IsArray)
        {
            var elementType = type.GetElementType()!;
            var count = _options.DefaultCollectionSize; // Config'den gelmeli (örn: 3)
            var array = Array.CreateInstance(elementType, count);

            for (int i = 0; i < count; i++)
            {
                var item = GenerateInternal(elementType, depth + 1, new HashSet<Type>(typeStack));
                array.SetValue(item, i);
            }

            result = array;
            return true;
        }

        // 3. Dictionary Kontrolü (IDictionary<K, V>)
        if (type.IsGenericType && typeof(IDictionary).IsAssignableFrom(type))
        {
            var keyType = type.GetGenericArguments()[0];
            var valueType = type.GetGenericArguments()[1];
            var dict = (IDictionary)Activator.CreateInstance(type)!;

            for (int i = 0; i < _options.DefaultCollectionSize; i++)
            {
                var k = GenerateInternal(keyType, depth + 1, new HashSet<Type>(typeStack));
                var v = GenerateInternal(valueType, depth + 1, new HashSet<Type>(typeStack));
                if (k != null) dict.Add(k, v);
            }

            result = dict;
            return true;
        }

        // 4. List / IEnumerable Kontrolü (List<T>, ICollection<T>)
        if (type.IsGenericType && typeof(IEnumerable).IsAssignableFrom(type))
        {
            var elementType = type.GetGenericArguments()[0];
            // Somut bir tip oluştur (List<T>)
            var listType = typeof(List<>).MakeGenericType(elementType);
            var list = (IList)Activator.CreateInstance(listType)!;

            for (int i = 0; i < _options.DefaultCollectionSize; i++)
            {
                var item = GenerateInternal(elementType, depth + 1, new HashSet<Type>(typeStack));
                list.Add(item);
            }

            result = list;
            return true;
        }

        return false;
    }

    // Özyinelemeli (Recursive) ana fonksiyon
    private object? GenerateInternal(Type type, int depth, HashSet<Type> typeStack, Type? containerType = null,
        string? propertyName = null)
    {
        if (depth > _options.MaxDepth) return null;

        if (containerType != null && propertyName != null &&
            _options.Rules.TryGetValue(containerType, out var typeConfig))
        {
            if (typeConfig.PropertyRules.TryGetValue(propertyName, out var customFactory))
            {
                return customFactory();
            }
        }

        // 1. Önce primitive/basit değer üreticilerini kontrol et (String, Int vs.)
        var generator = _valueGenerators.FirstOrDefault(g => g.CanHandle(type, string.Empty));
        if (generator != null)
        {
            return generator.GenerateValue(type, string.Empty);
        }

        // collection types
        if (TryGenerateCollection(type, depth, typeStack, out var collectionResult))
        {
            return collectionResult;
        }

        // 2. Döngüsel Referans Kontrolü (Type-based)
        if (!type.IsValueType && type != typeof(string))
        {
            if (typeStack.Contains(type)) return null; // Aynı dalda aynı tip üretilemez
            typeStack.Add(type);
        }

        var metadata = _cache.GetOrAdd(type);
        object? instance;

        // 3. Nesne Yaratma
        try
        {
            if (metadata.BestConstructor != null && metadata.ConstructorParameters.Count > 0)
            {
                var args = metadata.ConstructorParameters
                    .Select(p => GenerateInternal(p.ParameterType, depth + 1, new HashSet<Type>(typeStack),
                        type, p.LinkedPropertyName))
                    .ToArray();
                instance = metadata.BestConstructor.Invoke(args);
            }
            else
            {
                instance = Activator.CreateInstance(type);
            }
        }
        catch
        {
            return null;
        } // Instantiation fails for abstract/interfaces

        // 4. Property Doldurma
        foreach (var prop in metadata.WritableProperties)
        {
            // Constructor'da zaten atanmış olma ihtimalini kontrol et (LinkedPropertyName)
            if (metadata.ConstructorParameters.Any(p => p.LinkedPropertyName == prop.Name))
                continue;

            var value = GenerateInternal(prop.PropertyType, depth + 1, new HashSet<Type>(typeStack), type, prop.Name);
            prop.Setter.Invoke(instance!, value);
        }

        // Backtracking: Bu dal bittiğinde tipi stack'ten çıkarabiliriz (Opsiyonel)
        // typeStack.Remove(type); 

        return instance;
    }
}

