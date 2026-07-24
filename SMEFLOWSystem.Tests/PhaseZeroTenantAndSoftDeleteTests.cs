using Microsoft.EntityFrameworkCore;
using SMEFLOWSystem.Application.DTOs.AttendanceDtos;
using SMEFLOWSystem.Core.Entities;
using SMEFLOWSystem.Infrastructure.Repositories;

namespace SMEFLOWSystem.Tests;

public sealed class PhaseZeroTenantAndSoftDeleteTests
{
    [KnownBugFact("BE-HR-01")]
    [Trait("Phase", "0")]
    [Trait("Gap", "BE-HR-01")]
    public async Task ShiftIncludeDeleted_NeverReturnsAnotherTenant()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        await using var context = PhaseZeroTestContext.Create(tenantA);

        context.Shifts.AddRange(
            Shift(tenantA, "A-ACTIVE", isDeleted: false),
            Shift(tenantA, "A-DELETED", isDeleted: true),
            Shift(tenantB, "B-ACTIVE", isDeleted: false),
            Shift(tenantB, "B-DELETED", isDeleted: true));
        await context.SaveChangesAsync();

        var repository = new ShiftRepository(context);
        var (items, total) = await repository.GetPagedAsync(
            search: null,
            includeDeleted: true,
            pageNumber: 1,
            pageSize: 20);

        Assert.Equal(2, total);
        Assert.All(items, item => Assert.Equal(tenantA, item.TenantId));
        Assert.Contains(items, item => item.Code == "A-ACTIVE");
        Assert.Contains(items, item => item.Code == "A-DELETED");
    }

    [KnownBugFact("BE-HR-01")]
    [Trait("Phase", "0")]
    [Trait("Gap", "BE-HR-01")]
    public async Task ShiftPatternIncludeDeleted_NeverReturnsAnotherTenant()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        await using var context = PhaseZeroTestContext.Create(tenantA);

        context.ShiftPatterns.AddRange(
            Pattern(tenantA, "A Active", isDeleted: false),
            Pattern(tenantA, "A Deleted", isDeleted: true),
            Pattern(tenantB, "B Active", isDeleted: false),
            Pattern(tenantB, "B Deleted", isDeleted: true));
        await context.SaveChangesAsync();

        var repository = new ShiftPatternRepository(context);
        var (items, total) = await repository.GetPagedAsync(
            search: null,
            includeDeleted: true,
            pageNumber: 1,
            pageSize: 20);

        Assert.Equal(2, total);
        Assert.All(items, item => Assert.Equal(tenantA, item.TenantId));
        Assert.Contains(items, item => item.Name == "A Active");
        Assert.Contains(items, item => item.Name == "A Deleted");
    }

    [Fact]
    [Trait("Phase", "0")]
    [Trait("Gap", "BE-MGR-02")]
    public async Task EmployeeDefaultQueries_ExcludeSoftDeletedEmployees()
    {
        var tenantId = Guid.NewGuid();
        var departmentId = Guid.NewGuid();
        var active = Employee(tenantId, departmentId, "Active", isDeleted: false);
        var deleted = Employee(tenantId, departmentId, "Deleted", isDeleted: true);
        await using var context = PhaseZeroTestContext.Create(tenantId);

        context.Departments.Add(Department(tenantId, departmentId));
        context.Employees.AddRange(active, deleted);
        await context.SaveChangesAsync();

        var repository = new EmployeeRepository(context);
        var byId = await repository.GetByIdAsync(deleted.Id);
        var (paged, total) = await repository.GetPagedAsync(
            departmentId: null,
            positionId: null,
            roleId: null,
            status: null,
            includeResigned: false,
            search: null,
            pageNumber: 1,
            pageSize: 20,
            sortBy: null,
            sortDir: null);
        var byDepartment = await repository.GetByDepartmentIdAsync(departmentId);

        Assert.Null(byId);
        Assert.Equal(1, total);
        Assert.Equal(active.Id, Assert.Single(paged).Id);
        Assert.Equal(active.Id, Assert.Single(byDepartment).Id);
    }

    [Fact]
    [Trait("Phase", "0")]
    [Trait("Gap", "BE-MGR-02")]
    public async Task EmployeeIncludeDeletedLookup_RemainsTenantScoped()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var deletedA = Employee(tenantA, null, "Deleted A", isDeleted: true);
        var deletedB = Employee(tenantB, null, "Deleted B", isDeleted: true);
        await using var context = PhaseZeroTestContext.Create(tenantA);

        context.Employees.AddRange(deletedA, deletedB);
        await context.SaveChangesAsync();

        var repository = new EmployeeRepository(context);

        Assert.NotNull(await repository.GetByIdIncludeDeletedAsync(deletedA.Id, tenantA));
        Assert.Null(await repository.GetByIdIncludeDeletedAsync(deletedB.Id, tenantA));
    }

    [Fact]
    [Trait("Phase", "0")]
    [Trait("Gap", "BE-MGR-02")]
    public async Task RestoredEmployee_ReappearsInDefaultQueries()
    {
        var tenantId = Guid.NewGuid();
        var deleted = Employee(tenantId, null, "Restored", isDeleted: true);
        await using var context = PhaseZeroTestContext.Create(tenantId);

        context.Employees.Add(deleted);
        await context.SaveChangesAsync();

        var repository = new EmployeeRepository(context);
        var employee = await repository.GetByIdIncludeDeletedAsync(deleted.Id, tenantId);
        Assert.NotNull(employee);

        employee!.IsDeleted = false;
        await repository.UpdateAsync(employee);

        Assert.NotNull(await repository.GetByIdAsync(deleted.Id));
    }

    [KnownBugFact("BE-ATT-01")]
    [Trait("Phase", "0")]
    [Trait("Gap", "BE-ATT-01")]
    public void SubmitPunchContract_ExposesClientRequestId()
    {
        Assert.NotNull(typeof(SubmitPunchRequestDto).GetProperty("ClientRequestId"));
    }

    [KnownBugFact("BE-ATT-01")]
    [Trait("Phase", "0")]
    [Trait("Gap", "BE-ATT-01")]
    public async Task RawPunchLogModel_HasUniqueClientRequestIdIndex()
    {
        await using var context = PhaseZeroTestContext.Create(Guid.NewGuid());
        var entityType = context.Model.FindEntityType(typeof(RawPunchLog));
        var property = entityType?.FindProperty("ClientRequestId");

        Assert.NotNull(property);

        var uniqueIndex = entityType!.GetIndexes()
            .SingleOrDefault(index =>
                index.IsUnique &&
                index.Properties.Contains(property!));

        Assert.NotNull(uniqueIndex);
    }

    private static Shift Shift(Guid tenantId, string code, bool isDeleted)
    {
        return new Shift
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Code = code,
            Name = code,
            IsDeleted = isDeleted
        };
    }

    private static ShiftPattern Pattern(
        Guid tenantId,
        string name,
        bool isDeleted)
    {
        return new ShiftPattern
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = name,
            CycleLengthDays = 7,
            IsDeleted = isDeleted
        };
    }

    private static Department Department(Guid tenantId, Guid id)
    {
        return new Department
        {
            Id = id,
            TenantId = tenantId,
            Name = "Engineering",
            IsDeleted = false
        };
    }

    private static Employee Employee(
        Guid tenantId,
        Guid? departmentId,
        string name,
        bool isDeleted)
    {
        return new Employee
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            DepartmentId = departmentId,
            FullName = name,
            Phone = "0900000000",
            Email = $"{Guid.NewGuid():N}@example.test",
            HireDate = new DateOnly(2025, 1, 1),
            BaseSalary = 1,
            Status = "Working",
            IsDeleted = isDeleted
        };
    }
}
