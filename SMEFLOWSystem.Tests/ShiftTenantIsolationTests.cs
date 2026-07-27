using SMEFLOWSystem.Core.Entities;
using SMEFLOWSystem.Infrastructure.Repositories;

namespace SMEFLOWSystem.Tests;

public sealed class ShiftTenantIsolationTests
{
    [Fact]
    public async Task DefaultQueries_OnlyReturnActiveRowsFromCurrentTenant()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var currentTenant =
            new PhaseZeroTestContext.MutableCurrentTenantService(tenantA);
        await using var context = PhaseZeroTestContext.Create(tenantA);

        context.Shifts.AddRange(
            Shift(tenantA, "A-ACTIVE", isDeleted: false),
            Shift(tenantA, "A-DELETED", isDeleted: true),
            Shift(tenantB, "B-ACTIVE", isDeleted: false));
        context.ShiftPatterns.AddRange(
            Pattern(tenantA, "A Active", isDeleted: false),
            Pattern(tenantA, "A Deleted", isDeleted: true),
            Pattern(tenantB, "B Active", isDeleted: false));
        await context.SaveChangesAsync();

        var shiftResult = await new ShiftRepository(context, currentTenant)
            .GetPagedAsync(null, false, 1, 20);
        var patternResult = await new ShiftPatternRepository(context, currentTenant)
            .GetPagedAsync(null, false, 1, 20);

        Assert.Equal(1, shiftResult.TotalCount);
        var shift = Assert.Single(shiftResult.Items);
        Assert.Equal(tenantA, shift.TenantId);
        Assert.Equal("A-ACTIVE", shift.Code);

        Assert.Equal(1, patternResult.TotalCount);
        var pattern = Assert.Single(patternResult.Items);
        Assert.Equal(tenantA, pattern.TenantId);
        Assert.Equal("A Active", pattern.Name);
    }

    [Fact]
    public async Task ShiftIncludeDeleted_SearchAndPagingStayTenantScoped()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var currentTenant =
            new PhaseZeroTestContext.MutableCurrentTenantService(tenantA);
        await using var context = PhaseZeroTestContext.Create(tenantA);

        context.Shifts.AddRange(
            Shift(tenantA, "ALPHA-ACTIVE", isDeleted: false),
            Shift(tenantA, "ALPHA-DELETED", isDeleted: true),
            Shift(tenantA, "BETA", isDeleted: false),
            Shift(tenantB, "ALPHA-FOREIGN", isDeleted: false));
        await context.SaveChangesAsync();

        var result = await new ShiftRepository(context, currentTenant)
            .GetPagedAsync(
                search: "ALPHA",
                includeDeleted: true,
                pageNumber: 2,
                pageSize: 1);

        Assert.Equal(2, result.TotalCount);
        var item = Assert.Single(result.Items);
        Assert.Equal(tenantA, item.TenantId);
        Assert.StartsWith("ALPHA", item.Code);
    }

    [Fact]
    public async Task ShiftPatternIncludeDeleted_SearchAndPagingStayTenantScoped()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var currentTenant =
            new PhaseZeroTestContext.MutableCurrentTenantService(tenantA);
        await using var context = PhaseZeroTestContext.Create(tenantA);

        context.ShiftPatterns.AddRange(
            Pattern(tenantA, "Alpha Active", isDeleted: false),
            Pattern(tenantA, "Alpha Deleted", isDeleted: true),
            Pattern(tenantA, "Beta", isDeleted: false),
            Pattern(tenantB, "Alpha Foreign", isDeleted: false));
        await context.SaveChangesAsync();

        var result = await new ShiftPatternRepository(context, currentTenant)
            .GetPagedAsync(
                search: "Alpha",
                includeDeleted: true,
                pageNumber: 2,
                pageSize: 1);

        Assert.Equal(2, result.TotalCount);
        var item = Assert.Single(result.Items);
        Assert.Equal(tenantA, item.TenantId);
        Assert.StartsWith("Alpha", item.Name);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShiftQuery_WithoutTenantIsRejected(bool includeDeleted)
    {
        var currentTenant =
            new PhaseZeroTestContext.MutableCurrentTenantService(null);
        await using var context = PhaseZeroTestContext.Create(null);
        var repository = new ShiftRepository(context, currentTenant);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => repository.GetPagedAsync(
                search: null,
                includeDeleted,
                pageNumber: 1,
                pageSize: 20));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShiftPatternQuery_WithoutTenantIsRejected(bool includeDeleted)
    {
        var currentTenant =
            new PhaseZeroTestContext.MutableCurrentTenantService(null);
        await using var context = PhaseZeroTestContext.Create(null);
        var repository = new ShiftPatternRepository(context, currentTenant);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => repository.GetPagedAsync(
                search: null,
                includeDeleted,
                pageNumber: 1,
                pageSize: 20));
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
}
