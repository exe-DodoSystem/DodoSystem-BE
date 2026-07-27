namespace SMEFLOWSystem.Application.Exceptions;

public class DownstreamServiceException : Exception
{
    public DownstreamServiceException(
        string message,
        string errorCode = "DOWNSTREAM_SERVICE_FAILURE",
        bool serviceUnavailable = false,
        Exception? innerException = null)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
        ServiceUnavailable = serviceUnavailable;
    }

    public string ErrorCode { get; }
    public bool ServiceUnavailable { get; }
}
