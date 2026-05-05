namespace SmartJsonGenerator.Core.Abstractions;

/// <summary>Generates populated object instances and optionally serializes them to JSON.</summary>
public interface ISmartJsonGenerator
{
    /// <summary>Generates a single <typeparamref name="T"/> instance and returns it as an indented JSON string.</summary>
    string GenerateJson<T>();

    /// <summary>Generates <paramref name="count"/> instances and returns them as an indented JSON array (or single object when count is 1).</summary>
    string GenerateJson<T>(int count);

    /// <summary>Generates and returns a single populated <typeparamref name="T"/> instance.</summary>
    T Generate<T>();

    /// <summary>Lazily generates <paramref name="count"/> populated <typeparamref name="T"/> instances.</summary>
    IEnumerable<T> GenerateMany<T>(int count);

    /// <summary>Asynchronously streams <paramref name="count"/> populated <typeparamref name="T"/> instances.</summary>
    IAsyncEnumerable<T> GenerateManyAsync<T>(int count, CancellationToken cancellationToken);
}

