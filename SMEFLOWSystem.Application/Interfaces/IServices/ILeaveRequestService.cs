using SMEFLOWSystem.Application.DTOs.Leave;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SMEFLOWSystem.Application.Interfaces.IServices;

public interface ILeaveRequestService
{
    // Employee actions
    Task<LeaveRequestDto> SubmitLeaveRequestAsync(Guid userId, SubmitLeaveRequestDto dto);
    Task<LeaveRequestDto> CancelLeaveRequestAsync(Guid userId, Guid requestId);
    Task<List<LeaveRequestDto>> GetMyLeaveRequestsAsync(Guid userId);
    Task<List<LeaveBalanceDto>> GetMyBalancesAsync(Guid userId, int year);

    // HR/Manager actions
    Task<LeaveRequestDto> ApproveLeaveRequestAsync(Guid hrUserId, Guid requestId, ApproveLeaveRequestDto dto);
    Task<LeaveRequestDto> RejectLeaveRequestAsync(Guid hrUserId, Guid requestId, RejectLeaveRequestDto dto);
    Task<List<LeaveRequestDto>> GetPendingRequestsAsync();
    Task<List<LeaveRequestDto>> GetAllRequestsAsync();
    Task<List<LeaveBalanceDto>> GetLeaveBalancesReportAsync(int year);
    Task<LeaveBalanceDto> UpdateLeaveBalanceAsync(Guid balanceId, UpdateLeaveBalanceDto dto);

    // Leave Type management
    Task<List<LeaveTypeDto>> GetLeaveTypesAsync();
    Task<LeaveTypeDto> CreateLeaveTypeAsync(CreateLeaveTypeDto dto);
    Task<LeaveTypeDto> UpdateLeaveTypeAsync(Guid typeId, UpdateLeaveTypeDto dto);
    Task DeleteLeaveTypeAsync(Guid typeId);
}
