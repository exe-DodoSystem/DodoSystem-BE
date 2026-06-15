using System;

namespace SMEFLOWSystem.Application.DTOs.Leave;

public class LeaveTypeDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int DefaultAnnualDays { get; set; }
    public bool RequiresApproval { get; set; }
    public bool IsActive { get; set; }
}

public class CreateLeaveTypeDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int DefaultAnnualDays { get; set; }
    public bool RequiresApproval { get; set; }
}

public class UpdateLeaveTypeDto
{
    public string Name { get; set; } = string.Empty;
    public int DefaultAnnualDays { get; set; }
    public bool RequiresApproval { get; set; }
    public bool IsActive { get; set; }
}
