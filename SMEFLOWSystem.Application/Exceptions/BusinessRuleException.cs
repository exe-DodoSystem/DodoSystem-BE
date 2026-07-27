namespace SMEFLOWSystem.Application.Exceptions;

public class BusinessRuleException : Exception
{
    public BusinessRuleException(
        string message,
        string errorCode = "BUSINESS_RULE_VIOLATION")
        : base(message)
    {
        ErrorCode = errorCode;
    }

    public string ErrorCode { get; }
}
