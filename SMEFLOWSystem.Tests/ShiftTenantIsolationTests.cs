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

    [Fact]
    public async Task ShiftIncludeDeleted_FiltersCrossTenantSegments()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var currentTenant =
            new PhaseZeroTestContext.MutableCurrentTenantService(tenantA);
        await using var context = PhaseZeroTestContext.Create(tenantA);
        var shift = Shift(tenantA, "A-SHIFT", isDeleted: false);
        context.Shifts.Add(shift);
        context.ShiftSegments.AddRange(
            Segment(tenantA, shift),
            Segment(tenantB, shift));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var result = await new ShiftRepository(context, currentTenant)
            .GetPagedAsync(null, true, 1, 20);

        var storedShift = Assert.Single(result.Items);
        var segment = Assert.Single(storedShift.Segments);
        Assert.Equal(tenantA, segment.TenantId);
    }

    [Fact]
    public async Task ShiftPatternIncludeDeleted_FiltersCrossTenantNavigations()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var currentTenant =
            new PhaseZeroTestContext.MutableCurrentTenantService(tenantA);
        await using var context = PhaseZeroTestContext.Create(tenantA);
        var pattern = Pattern(tenantA, "Tenant A pattern", isDeleted: false);
        var shiftA = Shift(tenantA, "A-SHIFT", isDeleted: false);
        var shiftB = Shift(tenantB, "B-SHIFT", isDeleted: false);
        context.ShiftPatterns.Add(pattern);
        context.Shifts.AddRange(shiftA, shiftB);
        context.ShiftPatternDays.AddRange(
            PatternDay(tenantA, pattern.Id, shiftA.Id, dayIndex: 0),
            PatternDay(tenantB, pattern.Id, shiftA.Id, dayIndex: 1),
            PatternDay(tenantA, pattern.Id, shiftB.Id, dayIndex: 2));
        context.ShiftSegments.AddRange(
            Segment(tenantA, shiftA),
            Segment(tenantB, shiftA));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var result = await new ShiftPatternRepository(
                context,
                currentTenant)
            .GetPagedAsync(null, true, 1, 20);

        var storedPattern = Assert.Single(result.Items);
        var day = Assert.Single(storedPattern.Days);
        Assert.Equal(tenantA, day.TenantId);
        Assert.Equal(shiftA.Id, day.ScheduledShiftId);
        Assert.Equal(tenantA, day.ScheduledShift!.TenantId);
        var segment = Assert.Single(day.ScheduledShift.Segments);
        Assert.Equal(tenantA, segment.TenantId);
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

    private static ShiftSegment Segment(Guid tenantId, Shift shift)
    {
        return new ShiftSegment
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ShiftId = shift.Id,
            Shift = shift,
            StartTime = new TimeSpan(8, 0, 0),
            EndTime = new TimeSpan(17, 0, 0)
        };
    }

    private static ShiftPatternDay PatternDay(
        Guid tenantId,
        Guid patternId,
        Guid shiftId,
        int dayIndex)
    {
        return new ShiftPatternDay
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ShiftPatternId = patternId,
            ScheduledShiftId = shiftId,
            DayIndex = dayIndex
        };
    }
}
