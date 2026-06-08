using AutoMapper;
using SMEFLOWSystem.Application.DTOs.HRDtos;
using SMEFLOWSystem.Application.Extensions;
using SMEFLOWSystem.Application.Interfaces.IRepositories;
using SMEFLOWSystem.Application.Interfaces.IServices;
using SMEFLOWSystem.Core.Entities;
using SMEFLOWSystem.SharedKernel.Interfaces;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SMEFLOWSystem.Application.Services;

public class ManualTimesheetService : IManualTimesheetService
{
    private readonly IManualMonthlyTimesheetRepository _manualTimesheetRepo;
    private readonly IEmployeeRepository _employeeRepo;
    private readonly ICurrentUserService _currentUser;
    private readonly IMapper _mapper;

    public ManualTimesheetService(
        IManualMonthlyTimesheetRepository manualTimesheetRepo,
        IEmployeeRepository employeeRepo,
        ICurrentUserService currentUser,
        IMapper mapper)
    {
        _manualTimesheetRepo = manualTimesheetRepo;
        _employeeRepo = employeeRepo;
        _currentUser = currentUser;
        _mapper = mapper;
    }

    public async Task<ManualMonthlyTimesheetDto> UpsertAsync(Guid tenantId, ManualMonthlyTimesheetUpsertDto dto)
    {
        if (!_currentUser.IsAdmin() && !_currentUser.IsHrManager())
        {
            throw new UnauthorizedAccessException("Bạn không có quyền thực hiện thao tác này.");
        }

        var emp = await _employeeRepo.GetByIdAsync(dto.EmployeeId);
        if (emp == null || emp.TenantId != tenantId)
        {
            throw new KeyNotFoundException("Không tìm thấy nhân viên.");
        }

        var existing = await _manualTimesheetRepo.GetByEmployeeMonthYearAsync(tenantId, dto.EmployeeId, dto.Month, dto.Year);

        if (existing == null)
        {
            var entity = _mapper.Map<ManualMonthlyTimesheet>(dto);
            entity.Id = Guid.NewGuid();
            entity.TenantId = tenantId;
            await _manualTimesheetRepo.AddAsync(entity);
            
            var created = await _manualTimesheetRepo.GetByIdAsync(entity.Id);
            return _mapper.Map<ManualMonthlyTimesheetDto>(created);
        }
        else
        {
            _mapper.Map(dto, existing);
            await _manualTimesheetRepo.UpdateAsync(existing);
            return _mapper.Map<ManualMonthlyTimesheetDto>(existing);
        }
    }

    public async Task<List<ManualMonthlyTimesheetDto>> GetByMonthAsync(Guid tenantId, int month, int year)
    {
        _currentUser.EnsureHrAccess();

        var list = await _manualTimesheetRepo.GetByTenantMonthYearAsync(tenantId, month, year);
        return _mapper.Map<List<ManualMonthlyTimesheetDto>>(list);
    }

    public async Task<bool> DeleteAsync(Guid tenantId, Guid id)
    {
        if (!_currentUser.IsAdmin() && !_currentUser.IsHrManager())
        {
            throw new UnauthorizedAccessException("Bạn không có quyền thực hiện thao tác này.");
        }

        var entity = await _manualTimesheetRepo.GetByIdAsync(id);
        if (entity == null || entity.TenantId != tenantId)
        {
            return false;
        }

        await _manualTimesheetRepo.DeleteAsync(entity);
        return true;
    }
}
