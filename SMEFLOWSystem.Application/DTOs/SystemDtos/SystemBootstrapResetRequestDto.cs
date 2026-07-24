namespace SMEFLOWSystem.Application.DTOs.SystemDtos;

public sealed class SystemBootstrapResetRequestDto
{
    public string Confirmation { get; set; } = string.Empty;
    public string CurrentPassword { get; set; } = string.Empty;
}

public sealed class SystemBootstrapResetResponseDto
{
    public string Message { get; set; } = "System bootstrap account was reset successfully.";
    public bool BootstrapAvailable { get; set; } = true;
}

public sealed class SystemBootstrapResetResult
{
    public bool Succeeded { get; set; }
    public string ErrorCode { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;

    public static SystemBootstrapResetResult Success()
        => new() { Succeeded = true };

    public static SystemBootstrapResetResult Failure(string code, string message)
        => new() { ErrorCode = code, Message = message };
}
