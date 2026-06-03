using System;
using System.Threading.Tasks;
using SMEFLOWSystem.Application.DTOs.DashboardDtos;

namespace SMEFLOWSystem.Application.Interfaces.IServices;

public interface IDashboardService
{
    Task<AdminDashboardDto> GetAdminDashboardAsync(Guid tenantId, int month, int year);
    Task<ManagerDashboardDto> GetManagerDashboardAsync(Guid tenantId, Guid userId, int month, int year);
    Task<EmployeeDashboardDto> GetEmployeeDashboardAsync(Guid userId, int month, int year);
}
