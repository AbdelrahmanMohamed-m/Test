namespace Domain;

public class Result<T>
{
    public bool IsSuccess { get; }
    public string? Error { get; }
    public T? Value { get; }

    private Result(bool success, T? value, string? error)
    {
        IsSuccess = success;
        Value = value;
        Error = error;
    }

    public static Result<T> Success(T value)
    {
        if (value == null)
            throw new ArgumentNullException(nameof(value), "Success result cannot have null value");
            
        return new Result<T>(true, value, null);
    }

    public static Result<T> Failure(string error)
    {
        if (string.IsNullOrWhiteSpace(error))
            throw new ArgumentNullException(nameof(error), "Failure result cannot have empty error message");
            
        return new(false, default, error);
    }
}

public class Result
{
    public bool IsSuccess { get; }
    public string? Error { get; }

    private Result(bool success, string? error)
    {
        IsSuccess = success;
        Error = error;
    }

    public static Result Success() => new(true, null);
    public static Result Failure(string error)
    {
        if (string.IsNullOrWhiteSpace(error))
            throw new ArgumentNullException(nameof(error), "Failure result cannot have empty error message");
            
        return new Result(false, error);
    }
}
