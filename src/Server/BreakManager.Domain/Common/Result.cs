namespace BreakManager.Domain.Common;

/// <summary>
/// Resultado de una operación que puede fallar por una regla de negocio.
/// Las excepciones quedan reservadas para lo excepcional.
/// </summary>
public class Result
{
    protected Result(bool isSuccess, Error error)
    {
        ArgumentNullException.ThrowIfNull(error);

        if (isSuccess && error != Error.None)
        {
            throw new ArgumentException("Un resultado exitoso no puede tener error.", nameof(error));
        }

        if (!isSuccess && error == Error.None)
        {
            throw new ArgumentException("Un resultado fallido debe tener un error.", nameof(error));
        }

        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public Error Error { get; }

    public static Result Success() => new(true, Error.None);

    public static Result Failure(Error error) => new(false, error);

    public static Result<TValue> Success<TValue>(TValue value) => new(value, true, Error.None);

    public static Result<TValue> Failure<TValue>(Error error) => new(default, false, error);
}

/// <summary>
/// Resultado con valor. Leer <see cref="Value"/> de un resultado fallido es un error de programación.
/// </summary>
#pragma warning disable CA1000 // Fábricas estáticas en tipo genérico: parte de la API de Result<T>.
public sealed class Result<TValue> : Result
{
    private readonly TValue? _value;

    internal Result(TValue? value, bool isSuccess, Error error)
        : base(isSuccess, error)
    {
        _value = value;
    }

    public TValue Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("No se puede leer el valor de un resultado fallido.");

#pragma warning disable CA2225 // Las conversiones implícitas ya tienen equivalentes con nombre: Result.Success / Result.Failure.
    public static implicit operator Result<TValue>(TValue value) => Success(value);

    public static implicit operator Result<TValue>(Error error) => Failure<TValue>(error);
#pragma warning restore CA2225
}
#pragma warning restore CA1000
