namespace SmartJsonGenerator.Core.Abstractions;

public interface ISmartJsonGenerator
{
    string GenerateJson<T>();
    string GenerateJson<T>(int count);

    T Generate<T>();
    IEnumerable<T> GenerateMany<T>(int count);

    IAsyncEnumerable<T> GenerateManyAsync<T>(int count, CancellationToken cancellationToken);
}

