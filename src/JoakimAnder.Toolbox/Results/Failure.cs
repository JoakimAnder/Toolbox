namespace JoakimAnder.Toolbox.Results;

/// <summary>
/// Carrier for an inferred-type failure value. Construct via <see cref="Result.Failure{TError}(TError)"/>.
/// Implicitly converts to <see cref="Result{T, TError}"/> and <see cref="Result{TError}"/>.
/// </summary>
public readonly struct Failure<TError> : IEquatable<Failure<TError>> where TError : notnull
{
    internal readonly TError Error;
    internal Failure(TError error) { Error = error; }

    /// <inheritdoc/>
    public bool Equals(Failure<TError> other) => EqualityComparer<TError>.Default.Equals(Error, other.Error);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Failure<TError> other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => EqualityComparer<TError>.Default.GetHashCode(Error);

    public static bool operator ==(Failure<TError> left, Failure<TError> right) => left.Equals(right);
    public static bool operator !=(Failure<TError> left, Failure<TError> right) => !left.Equals(right);
}
