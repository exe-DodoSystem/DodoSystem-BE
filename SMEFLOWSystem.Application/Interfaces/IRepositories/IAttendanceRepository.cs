using SharedKernel.DTOs;
using SMEFLOWSystem.Core.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SMEFLOWSystem.Application.Interfaces.IRepositories
{
    public interface IAttendanceRepository
    {
        Task<Attendance?> GetByIdAsync(Guid id);
        Task<Attendance?> GetTodayByEmployeeIdAsync(Guid employeeId, DateOnly workDate);
        Task AddAsync(Attendance attendance);
        Task UpdateAsync(Attendance attendance);
        Task<(List<Attendance> Items, int TotalCount)> GetPagedAsync(Guid? departmentId, Guid? employeeId, DateOnly? fromDate, DateOnly? toDate, string? status, string? approvalStatus, string? search, int pageNumber, int pageSize, string? sortBy, string? sortDir);
        Task<List<Attendance>> GetByEmployeeIdAsync(Guid employeeId, DateOnly fromDate, DateOnly toDate);
    }
}
