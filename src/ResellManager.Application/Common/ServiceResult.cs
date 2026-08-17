namespace ResellManager.Application.Common;

public class ServiceResult
{
    protected ServiceResult(bool isSuccess, string? errorMessage)
    {
        IsSuccess = isSuccess;
        ErrorMessage = errorMessage;
    }

    public bool IsSuccess { get; }
    public bool Success => IsSuccess;
    public string? ErrorMessage { get; }

    public static ServiceResult Ok() => new(true, null);
    public static ServiceResult Failure(string errorMessage) => new(false, errorMessage);
}

public sealed class ServiceResult<T> : ServiceResult
{
    private ServiceResult(bool isSuccess, T? value, string? errorMessage)
        : base(isSuccess, errorMessage) => Value = value;

    public T? Value { get; }

    public static ServiceResult<T> Ok(T value) => new(true, value, null);
    public new static ServiceResult<T> Failure(string errorMessage) => new(false, default, errorMessage);
}
