using System.Diagnostics.CodeAnalysis;

namespace SearchLab.Core.Results;

public readonly struct Result<T>
{
    private readonly T _value;
    private readonly Error? _error;

    private Result(T value)
    {
        _value = value;
        _error = null;
    }

    private Result(Error error)
    {
        _value = default!;
        _error = error;
    }

    public T Value => IsSuccess
        ? _value
        : throw new InvalidOperationException("Cannot access Value of a failed Result.");

    public Error Error => _error
        ?? throw new InvalidOperationException("Cannot access Error of a successful Result.");

    [MemberNotNullWhen(false, nameof(_error))]
    public bool IsSuccess => _error is null;

    public static Result<T> Ok(T value) => new(value);

    public static implicit operator Result<T>(T value) => new(value);

    public static implicit operator Result<T>(Error error) => new(error);
}
