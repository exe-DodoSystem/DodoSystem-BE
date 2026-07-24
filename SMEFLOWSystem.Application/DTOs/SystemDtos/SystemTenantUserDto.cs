namespace SMEFLOWSystem.Application.DTOs.SystemDtos;

public sealed class SystemTenantUserQueryDto
{
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Search { get; set; }
    public string? Role { get; set; }
    public bool? IsActive { get; set; }
}

public sealed class SystemTenantUserDto
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool IsVerified { get; set; }
    public IReadOnlyList<string> Roles { get; set; } = Array.Empty<string>();
    public DateTime CreatedAt { get; set; }
}
