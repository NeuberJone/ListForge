namespace ListForge.Core;

public class OperationResult
{
    protected OperationResult(
        bool success,
        string userMessage = "",
        string technicalMessage = "",
        Exception? exception = null,
        string errorCode = "")
    {
        Success = success;
        UserMessage = userMessage;
        TechnicalMessage = technicalMessage;
        Exception = exception;
        ErrorCode = errorCode;
    }

    public bool Success { get; }
    public string UserMessage { get; }
    public string TechnicalMessage { get; }
    public Exception? Exception { get; }
    public string ErrorCode { get; }

    public static OperationResult Ok(string userMessage = "", string technicalMessage = "") =>
        new(true, userMessage, technicalMessage);

    public static OperationResult Fail(
        string userMessage,
        string technicalMessage = "",
        Exception? exception = null,
        string errorCode = "") =>
        new(false, userMessage, technicalMessage, exception, errorCode);
}

public sealed class OperationResult<T>
{
    private OperationResult(
        bool success,
        T? value = default,
        string userMessage = "",
        string technicalMessage = "",
        Exception? exception = null,
        string errorCode = "")
    {
        Success = success;
        Value = value;
        UserMessage = userMessage;
        TechnicalMessage = technicalMessage;
        Exception = exception;
        ErrorCode = errorCode;
    }

    public bool Success { get; }
    public T? Value { get; }
    public string UserMessage { get; }
    public string TechnicalMessage { get; }
    public Exception? Exception { get; }
    public string ErrorCode { get; }

    public static OperationResult<T> Ok(T value, string userMessage = "", string technicalMessage = "") =>
        new(true, value, userMessage, technicalMessage);

    public static OperationResult<T> Fail(
        string userMessage,
        string technicalMessage = "",
        Exception? exception = null,
        string errorCode = "") =>
        new(false, default, userMessage, technicalMessage, exception, errorCode);
}
