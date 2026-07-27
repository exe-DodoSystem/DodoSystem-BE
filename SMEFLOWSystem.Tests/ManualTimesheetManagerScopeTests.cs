using AutoMapper;
using Microsoft.Extensions.DependencyInjection;
using SMEFLOWSystem.Application.DTOs.HRDtos;
using SMEFLOWSystem.Application.Interfaces.IRepositories;
using SMEFLOWSystem.Application.Interfaces.IServices;
using SMEFLOWSystem.Application.Mappings;
using SMEFLOWSystem.Application.Services;
using SMEFLOWSystem.Core.Entities;
using SMEFLOWSystem.Infrastructure.Repositories;
using SMEFLOWSystem.SharedKernel.Common;
using SMEFLOWSystem.SharedKernel.Interfaces;

namespace SMEFLOWSystem.Tests;

public sealed class ManualTimesheetManagerScopeTests
{
    [Fact]
    public async Task RepositoryMonthQuery_AppliesDepartmentDateAndTenantScopes()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var departmentA1 = Guid.NewGuid();
        var departmentA2 = Guid.NewGuid();
        var departmentB = Guid.NewGuid();
        await using var context = PhaseZeroTestContext.Create(tenantA);

        var employeeA1 = Employee(tenantA, departmentA1, "Employee A1");
        var employeeA2 = Employee(tenantA, departmentA2, "Employee A2");
        var employeeWithoutDepartment =
            Employee(tenantA, departmentId: null, "Employee without department");
        var employeeB = Employee(tenantB, departmentB, "Employee B");

        var targetA1 = Timesheet(tenantA, employeeA1, month: 7, year: 2026);
        var targetA2 = Timesheet(tenantA, employeeA2, month: 7, year: 2026);
        var targetWithoutDepartment = Timesheet(
            tenantA,
            employeeWithoutDepartment,
            month: 7,
            year: 2026);
        var wrongMonth = Timesheet(
            tenantA,
            employeeA1,
            month: 6,
            year: 2026);
        var wrongYear = Timesheet(
            tenantA,
            employeeA1,
            month: 7,
            year: 2025);
        var otherTenant = Timesheet(
            tenantB,
            employeeB,
            month: 7,
            year: 2026);

        context.Employees.AddRange(
            employeeA1,
            employeeA2,
            employeeWithoutDepartment,
            employeeB);
        context.ManualMonthlyTimesheets.AddRange(
            targetA1,
            targetA2,
            targetWithoutDepartment,
            wrongMonth,
            wrongYear,
            otherTenant);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var repository = new ManualMonthlyTimesheetRepository(context);

        var tenantWide = await repository.GetByTenantMonthYearAsync(
            tenantA,
            month: 7,
            year: 2026,
            departmentIds: null);
        var oneDepartment = await repository.GetByTenantMonthYearAsync(
            tenantA,
            month: 7,
            year: 2026,
            new[] { departmentA1 });
        var multipleDepartments = await repository.GetByTenantMonthYearAsync(
            tenantA,
            month: 7,
            year: 2026,
            new[] { departmentA1, departmentA2, departmentA1 });
        var noDepartments = await repository.GetByTenantMonthYearAsync(
            tenantA,
            month: 7,
            year: 2026,
            Array.Empty<Guid>());

        Assert.Equal(
            new[] { targetA1.Id, targetA2.Id, targetWithoutDepartment.Id }
                .OrderBy(id => id),
            tenantWide.Select(item => item.Id).OrderBy(id => id));
        Assert.Equal(targetA1.Id, Assert.Single(oneDepartment).Id);
        Assert.Equal(
            new[] { targetA1.Id, targetA2.Id }.OrderBy(id => id),
            multipleDepartments.Select(item => item.Id).OrderBy(id => id));
        Assert.Equal(
            multipleDepartments.Count,
            multipleDepartments.Select(item => item.Id).Distinct().Count());
        Assert.Empty(noDepartments);
    }

    [Fact]
    public async Task ServiceMonthQuery_ForwardsAccessibleDepartments()
    {
        var tenantId = Guid.NewGuid();
        var allowedDepartments = new[]
        {
            Guid.NewGuid(),
            Guid.NewGuid()
        };
        var employee = Employee(
            tenantId,
            allowedDepartments[0],
            "Visible employee");
        var repository = new RecordingManualTimesheetRepository
        {
            Result =
            [
                Timesheet(tenantId, employee, month: 7, year: 2026)
            ]
        };
        var authorization = new RecordingHrAuthorizationService
        {
            AccessibleDepartmentIds = allowedDepartments.ToList()
        };
        using var serviceProvider = new ServiceCollection()
            .AddLogging()
            .AddAutoMapper(_ => { }, typeof(HrMappingProfile).Assembly)
            .BuildServiceProvider();
        var service = new ManualTimesheetService(
            repository,
            null!,
            new CurrentUserServiceStub(RoleConstants.Manager),
            authorization,
            serviceProvider.GetRequiredService<IMapper>());

        var result = await service.GetByMonthAsync(
            tenantId,
            month: 7,
            year: 2026);

        Assert.Equal(1, authorization.ScopeCalls);
        Assert.Equal(tenantId, repository.LastTenantId);
        Assert.Equal(7, repository.LastMonth);
        Assert.Equal(2026, repository.LastYear);
        Assert.Equal(
            allowedDepartments,
            repository.LastDepartmentIds);
        Assert.Equal(employee.FullName, Assert.Single(result).EmployeeName);
    }

    private static Employee Employee(
        Guid tenantId,
        Guid? departmentId,
        string name)
    {
        return new Employee
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            DepartmentId = departmentId,
            FullName = name,
            Phone = "0900000000",
            Email = $"{Guid.NewGuid():N}@example.test",
            HireDate = new DateOnly(2026, 1, 1),
            BaseSalary = 1,
            Status = "Working",
            IsDeleted = false
        };
    }

    private static ManualMonthlyTimesheet Timesheet(
        Guid tenantId,
        Employee employee,
        int month,
        int year)
    {
        return new ManualMonthlyTimesheet
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            EmployeeId = employee.Id,
            Employee = employee,
            Month = month,
            Year = year,
            ActualWorkingDays = 20
        };
    }

    private sealed class RecordingHrAuthorizationService
        : IHrAuthorizationService
    {
        public List<Guid>? AccessibleDepartmentIds { get; init; }
        public int ScopeCalls { get; private set; }

        public Task<List<Guid>?> GetAccessibleDepartmentIdsAsync()
        {
            ScopeCalls++;
            return Task.FromResult(AccessibleDepartmentIds);
        }

        public Task EnsureDepartmentAccessAsync(Guid departmentId)
            => throw new NotSupportedException();

        public Task EnsureEmployeeAccessAsync(Employee employee)
            => throw new NotSupportedException();
    }

    private sealed class RecordingManualTimesheetRepository
        : IManualMonthlyTimesheetRepository
    {
        public List<ManualMonthlyTimesheet> Result { get; init; } = [];
        public Guid? LastTenantId { get; private set; }
        public int? LastMonth { get; private set; }
        public int? LastYear { get; private set; }
        public IReadOnlyCollection<Guid>? LastDepartmentIds { get; private set; }

        public Task<List<ManualMonthlyTimesheet>>
            GetByTenantMonthYearAsync(
                Guid tenantId,
                int month,
                int year,
                IReadOnlyCollection<Guid>? departmentIds)
        {
            LastTenantId = tenantId;
            LastMonth = month;
            LastYear = year;
            LastDepartmentIds = departmentIds;
            return Task.FromResult(Result);
        }

        public Task AddAsync(ManualMonthlyTimesheet timesheet)
            => throw new NotSupportedException();

        public Task UpdateAsync(ManualMonthlyTimesheet timesheet)
            => throw new NotSupportedException();

        public Task DeleteAsync(ManualMonthlyTimesheet timesheet)
            => throw new NotSupportedException();

        public Task<ManualMonthlyTimesheet?> GetByIdAsync(Guid id)
            => throw new NotSupportedException();

        public Task<ManualMonthlyTimesheet?> GetByEmployeeMonthYearAsync(
            Guid tenantId,
            Guid employeeId,
            int month,
            int year)
            => throw new NotSupportedException();
    }

    private sealed class CurrentUserServiceStub : ICurrentUserService
    {
        private readonly string _role;

        public CurrentUserServiceStub(string role)
        {
            _role = role;
        }

        public Guid? UserId { get; } = Guid.NewGuid();

        public bool IsInRole(string role)
            => string.Equals(_role, role, StringComparison.Ordinal);
    }
}
