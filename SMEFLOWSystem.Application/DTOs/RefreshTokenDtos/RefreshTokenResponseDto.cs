namespace SMEFLOWSystem.Application.DTOs.RefreshTokenDtos;

public class RefreshTokenResponseDto
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public bool IsExpired { get; set; }
}
