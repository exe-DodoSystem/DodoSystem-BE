namespace SMEFLOWSystem.Application.Exceptions;

public class ConflictException : Exception
{
    public ConflictException(
        string message,
        string errorCode = "RESOURCE_CONFLICT")
        : base(message)
    {
        ErrorCode = errorCode;
    }

    public string ErrorCode { get; }
}
