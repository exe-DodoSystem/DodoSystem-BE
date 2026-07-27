using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SMEFLOWSystem.Application.DTOs.Leave;
using SMEFLOWSystem.Application.Interfaces.IRepositories;
using SMEFLOWSystem.Application.Interfaces.IServices;
using SMEFLOWSystem.Application.Services;
using SMEFLOWSystem.Core.Entities;
using SMEFLOWSystem.Infrastructure.Repositories;
using SMEFLOWSystem.SharedKernel.Common;
using SMEFLOWSystem.SharedKernel.Interfaces;
using SMEFLOWSystem.WebAPI.Controllers;
using System.Security.Claims;

namespace SMEFLOWSystem.Tests;

public sealed class LeaveRequestManagerScopeTests
{
    [Fact]
    public async Task RepositoryLists_ApplyDepartmentStatusAndTenantScopes()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var departmentA1 = Guid.NewGuid();
        var departmentA2 = Guid.NewGuid();
        await using var context = PhaseZeroTestContext.Create(tenantA);

        var employeeA1 = Employee(tenantA, departmentA1, "Employee A1");
        var employeeA2 = Employee(tenantA, departmentA2, "Employee A2");
        var employeeB = Employee(tenantB, Guid.NewGuid(), "Employee B");
        var leaveTypeA = LeaveType(tenantA, "ANNUAL-A");
        var leaveTypeB = LeaveType(tenantB, "ANNUAL-B");

        var pendingA1 = Request(tenantA, employeeA1, leaveTypeA);
        var rejectedA1 = Request(tenantA, employeeA1, leaveTypeA);
        rejectedA1.Reject(Guid.NewGuid(), "Outside pending list");
        var pendingA2 = Request(tenantA, employeeA2, leaveTypeA);
        var pendingB = Request(tenantB, employeeB, leaveTypeB);

        context.Employees.AddRange(employeeA1, employeeA2, employeeB);
        context.LeaveTypes.AddRange(leaveTypeA, leaveTypeB);
        context.LeaveRequests.AddRange(
            pendingA1,
            rejectedA1,
            pendingA2,
            pendingB);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var repository = new LeaveRequestRepository(context);

        var managerPending =
            await repository.GetPendingAsync(new[] { departmentA1 });
        var managerAll =
            await repository.GetAllAsync(new[] { departmentA1 });
        var tenantAdminAll = await repository.GetAllAsync(null);
        var unassignedManagerPending =
            await repository.GetPendingAsync(Array.Empty<Guid>());

        var visiblePending = Assert.Single(managerPending);
        Assert.Equal(pendingA1.Id, visiblePending.Id);
        Assert.Equal(
            new[] { pendingA1.Id, rejectedA1.Id }.OrderBy(id => id),
            managerAll.Select(request => request.Id).OrderBy(id => id));
        Assert.Equal(
            new[] { pendingA1.Id, rejectedA1.Id, pendingA2.Id }
                .OrderBy(id => id),
            tenantAdminAll.Select(request => request.Id).OrderBy(id => id));
        Assert.Empty(unassignedManagerPending);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task WriteActions_AuthorizeEmployeeBeforeMutatingRequest(
        bool approve)
    {
        var tenantId = Guid.NewGuid();
        var employee = Employee(
            tenantId,
            Guid.NewGuid(),
            "Out of scope employee");
        var leaveType = LeaveType(tenantId, "ANNUAL");
        var request = Request(tenantId, employee, leaveType);
        var repository = new RecordingLeaveRequestRepository
        {
            Request = request
        };
        var authorization = new RecordingHrAuthorizationService
        {
            ThrowOnEmployeeAccess = true
        };
        var service = CreateService(
            tenantId,
            repository,
            authorization);

        if (approve)
        {
            await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => service.ApproveLeaveRequestAsync(
                    Guid.NewGuid(),
                    request.Id,
                    new ApproveLeaveRequestDto()));
        }
        else
        {
            await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => service.RejectLeaveRequestAsync(
                    Guid.NewGuid(),
                    request.Id,
                    new RejectLeaveRequestDto
                    {
                        RejectReason = "Outside assigned department"
                    }));
        }

        Assert.Same(employee, authorization.LastEmployee);
        Assert.Equal("Pending", request.Status);
        Assert.Null(request.ApprovedByUserId);
        Assert.Null(request.RejectedByUserId);
        Assert.Equal(0, repository.UpdateCalls);
    }

    [Fact]
    public async Task ListActions_ForwardAccessibleDepartmentsToRepository()
    {
        var tenantId = Guid.NewGuid();
        var allowedDepartments = new List<Guid>
        {
            Guid.NewGuid(),
            Guid.NewGuid()
        };
        var repository = new RecordingLeaveRequestRepository();
        var authorization = new RecordingHrAuthorizationService
        {
            AccessibleDepartmentIds = allowedDepartments
        };
        var service = CreateService(
            tenantId,
            repository,
            authorization);

        await service.GetPendingRequestsAsync();
        await service.GetAllRequestsAsync();

        Assert.Equal(
            allowedDepartments,
            repository.LastPendingDepartmentIds);
        Assert.Equal(
            allowedDepartments,
            repository.LastAllDepartmentIds);
    }

    [Theory]
    [InlineData(RoleConstants.TenantAdmin)]
    [InlineData(RoleConstants.HrManager)]
    public async Task ElevatedHrRoles_HaveTenantWideReadAndWriteAccess(
        string role)
    {
        var managerDepartments = new RecordingManagerDepartmentRepository();
        var authorization = new HrAuthorizationService(
            new CurrentUserServiceStub(role),
            managerDepartments);

        var departmentIds =
            await authorization.GetAccessibleDepartmentIdsAsync();
        await authorization.EnsureEmployeeAccessAsync(
            Employee(Guid.NewGuid(), Guid.NewGuid(), "Any employee"));

        Assert.Null(departmentIds);
        Assert.Equal(0, managerDepartments.DepartmentListCalls);
        Assert.Equal(0, managerDepartments.ExistsCalls);
    }

    [Fact]
    public async Task Manager_ReadAndWriteAccessUseAssignedDepartments()
    {
        var assignedDepartment = Guid.NewGuid();
        var otherDepartment = Guid.NewGuid();
        var managerDepartments = new RecordingManagerDepartmentRepository(
            assignedDepartment);
        var authorization = new HrAuthorizationService(
            new CurrentUserServiceStub(RoleConstants.Manager),
            managerDepartments);

        var departmentIds =
            await authorization.GetAccessibleDepartmentIdsAsync();
        await authorization.EnsureEmployeeAccessAsync(
            Employee(Guid.NewGuid(), assignedDepartment, "In scope"));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => authorization.EnsureEmployeeAccessAsync(
                Employee(Guid.NewGuid(), otherDepartment, "Out of scope")));

        Assert.Equal(new[] { assignedDepartment }, departmentIds);
        Assert.Equal(1, managerDepartments.DepartmentListCalls);
        Assert.Equal(2, managerDepartments.ExistsCalls);
    }

    [Fact]
    public async Task ManagerWithoutDepartments_HasEmptyReadScopeAndNoWriteAccess()
    {
        var managerDepartments = new RecordingManagerDepartmentRepository();
        var authorization = new HrAuthorizationService(
            new CurrentUserServiceStub(RoleConstants.Manager),
            managerDepartments);

        var departmentIds =
            await authorization.GetAccessibleDepartmentIdsAsync();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => authorization.EnsureEmployeeAccessAsync(
                Employee(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    "Unassigned manager target")));

        Assert.NotNull(departmentIds);
        Assert.Empty(departmentIds);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ControllerWriteActions_MapScopeFailureToForbidden(
        bool approve)
    {
        var controller = new LeaveRequestController(
            new ForbiddenLeaveRequestService())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(
                        new ClaimsIdentity(
                            new[]
                            {
                                new Claim(
                                    ClaimTypes.NameIdentifier,
                                    Guid.NewGuid().ToString())
                            },
                            "Test"))
                }
            }
        };

        IActionResult result = approve
            ? await controller.Approve(
                Guid.NewGuid(),
                new ApproveLeaveRequestDto())
            : await controller.Reject(
                Guid.NewGuid(),
                new RejectLeaveRequestDto
                {
                    RejectReason = "Outside assigned department"
                });

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, objectResult.StatusCode);
    }

    private static LeaveRequestService CreateService(
        Guid tenantId,
        ILeaveRequestRepository repository,
        IHrAuthorizationService authorization)
    {
        return new LeaveRequestService(
            repository,
            null!,
            null!,
            null!,
            new PhaseZeroTestContext.MutableCurrentTenantService(tenantId),
            null!,
            null!,
            null!,
            authorization);
    }

    private static Employee Employee(
        Guid tenantId,
        Guid departmentId,
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

    private static LeaveType LeaveType(Guid tenantId, string code)
    {
        return new LeaveType
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Code = code,
            Name = code,
            DefaultAnnualDays = 12,
            RequiresApproval = true,
            IsActive = true,
            IsDeleted = false
        };
    }

    private static LeaveRequest Request(
        Guid tenantId,
        Employee employee,
        LeaveType leaveType)
    {
        return new LeaveRequest(
            tenantId,
            employee.Id,
            leaveType.Id,
            leaveType.Name,
            reasonNote: null,
            attachmentUrl: null)
        {
            Employee = employee,
            LeaveTypeNavigation = leaveType
        };
    }

    private sealed class RecordingHrAuthorizationService
        : IHrAuthorizationService
    {
        public List<Guid>? AccessibleDepartmentIds { get; init; }
        public bool ThrowOnEmployeeAccess { get; init; }
        public Employee? LastEmployee { get; private set; }

        public Task<List<Guid>?> GetAccessibleDepartmentIdsAsync()
            => Task.FromResult(AccessibleDepartmentIds);

        public Task EnsureDepartmentAccessAsync(Guid departmentId)
            => Task.CompletedTask;

        public Task EnsureEmployeeAccessAsync(Employee employee)
        {
            LastEmployee = employee;
            if (ThrowOnEmployeeAccess)
            {
                throw new UnauthorizedAccessException(
                    "Employee is outside the assigned departments.");
            }

            return Task.CompletedTask;
        }
    }

    private sealed class RecordingLeaveRequestRepository
        : ILeaveRequestRepository
    {
        public LeaveRequest? Request { get; init; }
        public int UpdateCalls { get; private set; }
        public IReadOnlyCollection<Guid>? LastPendingDepartmentIds
            { get; private set; }
        public IReadOnlyCollection<Guid>? LastAllDepartmentIds
            { get; private set; }

        public Task<List<LeaveRequestSegment>>
            GetApprovedSegmentsByEmployeeDateAsync(
                Guid employeeId,
                DateOnly leaveDate)
            => Task.FromResult(new List<LeaveRequestSegment>());

        public Task<List<LeaveRequestSegment>>
            GetApprovedSegmentsForEmployeesAsync(
                List<Guid> employeeIds,
                DateOnly minDate,
                DateOnly maxDate)
            => Task.FromResult(new List<LeaveRequestSegment>());

        public Task<LeaveRequest?> GetByIdAsync(Guid id)
            => Task.FromResult(Request?.Id == id ? Request : null);

        public Task<List<LeaveRequest>> GetByEmployeeAsync(Guid employeeId)
            => Task.FromResult(new List<LeaveRequest>());

        public Task<List<LeaveRequest>> GetPendingAsync(
            IReadOnlyCollection<Guid>? departmentIds)
        {
            LastPendingDepartmentIds = departmentIds;
            return Task.FromResult(new List<LeaveRequest>());
        }

        public Task<List<LeaveRequest>> GetAllAsync(
            IReadOnlyCollection<Guid>? departmentIds)
        {
            LastAllDepartmentIds = departmentIds;
            return Task.FromResult(new List<LeaveRequest>());
        }

        public Task AddAsync(LeaveRequest leaveRequest)
            => Task.CompletedTask;

        public Task UpdateAsync(LeaveRequest leaveRequest)
        {
            UpdateCalls++;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(LeaveRequest leaveRequest)
            => Task.CompletedTask;
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

    private sealed class RecordingManagerDepartmentRepository
        : IManagerDepartmentRepository
    {
        private readonly List<Guid> _departmentIds;

        public RecordingManagerDepartmentRepository(
            params Guid[] departmentIds)
        {
            _departmentIds = departmentIds.ToList();
        }

        public int DepartmentListCalls { get; private set; }
        public int ExistsCalls { get; private set; }

        public Task<List<Guid>> GetDepartmentIdsByUserIdAsync(Guid userId)
        {
            DepartmentListCalls++;
            return Task.FromResult(_departmentIds.ToList());
        }

        public Task<bool> ExistsAsync(Guid userId, Guid departmentId)
        {
            ExistsCalls++;
            return Task.FromResult(_departmentIds.Contains(departmentId));
        }

        public Task<List<ManagerDepartment>> GetByUserIdAsync(Guid userId)
            => throw new NotSupportedException();

        public Task AddAsync(ManagerDepartment entity)
            => throw new NotSupportedException();

        public Task RemoveAsync(Guid userId, Guid departmentId)
            => throw new NotSupportedException();

        public Task RemoveAllByUserIdAsync(Guid userId)
            => throw new NotSupportedException();
    }

    private sealed class ForbiddenLeaveRequestService
        : ILeaveRequestService
    {
        public Task<LeaveRequestDto> ApproveLeaveRequestAsync(
            Guid hrUserId,
            Guid requestId,
            ApproveLeaveRequestDto dto)
            => Task.FromException<LeaveRequestDto>(
                new UnauthorizedAccessException("Forbidden"));

        public Task<LeaveRequestDto> RejectLeaveRequestAsync(
            Guid hrUserId,
            Guid requestId,
            RejectLeaveRequestDto dto)
            => Task.FromException<LeaveRequestDto>(
                new UnauthorizedAccessException("Forbidden"));

        public Task<LeaveRequestDto> SubmitLeaveRequestAsync(
            Guid userId,
            SubmitLeaveRequestDto dto)
            => throw new NotSupportedException();

        public Task<LeaveRequestDto> CancelLeaveRequestAsync(
            Guid userId,
            Guid requestId)
            => throw new NotSupportedException();

        public Task<List<LeaveRequestDto>> GetMyLeaveRequestsAsync(Guid userId)
            => throw new NotSupportedException();

        public Task<List<LeaveBalanceDto>> GetMyBalancesAsync(
            Guid userId,
            int year)
            => throw new NotSupportedException();

        public Task<List<LeaveRequestDto>> GetPendingRequestsAsync()
            => throw new NotSupportedException();

        public Task<List<LeaveRequestDto>> GetAllRequestsAsync()
            => throw new NotSupportedException();

        public Task<List<LeaveBalanceDto>> GetLeaveBalancesReportAsync(int year)
            => throw new NotSupportedException();

        public Task<LeaveBalanceDto> UpdateLeaveBalanceAsync(
            Guid balanceId,
            UpdateLeaveBalanceDto dto)
            => throw new NotSupportedException();

        public Task<List<LeaveTypeDto>> GetLeaveTypesAsync()
            => throw new NotSupportedException();

        public Task<LeaveTypeDto> CreateLeaveTypeAsync(
            CreateLeaveTypeDto dto)
            => throw new NotSupportedException();

        public Task<LeaveTypeDto> UpdateLeaveTypeAsync(
            Guid typeId,
            UpdateLeaveTypeDto dto)
            => throw new NotSupportedException();

        public Task DeleteLeaveTypeAsync(Guid typeId)
            => throw new NotSupportedException();
    }
}
