namespace JoakimAnder.Toolbox.Results;

/// <summary>
/// Carrier for an inferred-type success value. Construct via <see cref="Result.Success{T}(T)"/>.
/// Implicitly converts to <see cref="Result{T, TError}"/> for any compatible <c>TError</c>.
/// </summary>
public readonly struct Success<T> : IEquatable<Success<T>>
{
    internal readonly T Value;
    internal Success(T value) { Value = value; }

    /// <inheritdoc/>
    public bool Equals(Success<T> other) => EqualityComparer<T>.Default.Equals(Value, other.Value);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Success<T> other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => EqualityComparer<T>.Default.GetHashCode(Value!);

    public static bool operator ==(Success<T> left, Success<T> right) => left.Equals(right);
    public static bool operator !=(Success<T> left, Success<T> right) => !left.Equals(right);
}
