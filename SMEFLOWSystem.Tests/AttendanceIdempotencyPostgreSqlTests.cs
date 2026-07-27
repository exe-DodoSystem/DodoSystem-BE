using Microsoft.EntityFrameworkCore;
using SMEFLOWSystem.Core.Entities;
using SMEFLOWSystem.Infrastructure.Data;
using SMEFLOWSystem.Infrastructure.Repositories;

namespace SMEFLOWSystem.Tests;

public sealed class AttendanceIdempotencyPostgreSqlTests
{
    [PostgreSqlFact]
    [Trait("Phase", "6")]
    [Trait("Gap", "BE-ATT-01")]
    public async Task ConcurrentInsert_WithSameKey_PersistsExactlyOneRow()
    {
        var connectionString = Environment.GetEnvironmentVariable(
            PostgreSqlFactAttribute.ConnectionStringVariable);
        Assert.False(string.IsNullOrWhiteSpace(connectionString));

        var tenantId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();
        var clientRequestId = Guid.NewGuid().ToString("D");
        var options = new DbContextOptionsBuilder<SMEFLOWSystemContext>()
            .UseNpgsql(connectionString)
            .Options;

        await using (var setupContext = CreateContext(options, tenantId))
        {
            await setupContext.Database.MigrateAsync();
            setupContext.Tenants.Add(new Tenant
            {
                Id = tenantId,
                Name = "Phase 6 integration tenant",
                Status = "Active",
                CreatedAt = DateTime.UtcNow,
                IsDeleted = false
            });
            setupContext.Employees.Add(new Employee
            {
                Id = employeeId,
                TenantId = tenantId,
                FullName = "Phase 6 integration employee",
                Phone = "0900000000",
                Email = $"{employeeId:N}@example.test",
                HireDate = new DateOnly(2026, 1, 1),
                BaseSalary = 1,
                Status = "Working",
                IsDeleted = false
            });
            await setupContext.SaveChangesAsync();
        }

        try
        {
            const int requestCount = 8;
            var results = await Task.WhenAll(
                Enumerable.Range(0, requestCount)
                    .Select(async _ =>
                    {
                        await using var context =
                            CreateContext(options, tenantId);
                        var repository =
                            new RawPunchLogRepository(context);
                        return await repository.AddIdempotentAsync(
                            new RawPunchLog
                            {
                                Id = Guid.NewGuid(),
                                TenantId = tenantId,
                                EmployeeId = employeeId,
                                ClientRequestId = clientRequestId,
                                Timestamp = DateTime.UtcNow,
                                PunchType = "Auto",
                                IsProcessed = false
                            });
                    }));

            await using var assertionContext =
                CreateContext(options, tenantId);
            var stored = await assertionContext.RawPunchLogs
                .Where(log =>
                    log.EmployeeId == employeeId &&
                    log.ClientRequestId == clientRequestId)
                .ToListAsync();

            Assert.Single(stored);
            Assert.Single(
                results.Select(result => result.PunchLog.Id).Distinct());
            Assert.Equal(1, results.Count(result => result.IsNew));
        }
        finally
        {
            await using var cleanupContext =
                CreateContext(options, tenantId);
            var logs = await cleanupContext.RawPunchLogs
                .Where(log => log.EmployeeId == employeeId)
                .ToListAsync();
            cleanupContext.RawPunchLogs.RemoveRange(logs);

            var employee = await cleanupContext.Employees
                .SingleOrDefaultAsync(item => item.Id == employeeId);
            if (employee != null)
            {
                cleanupContext.Employees.Remove(employee);
            }

            var tenant = await cleanupContext.Tenants
                .SingleOrDefaultAsync(item => item.Id == tenantId);
            if (tenant != null)
            {
                cleanupContext.Tenants.Remove(tenant);
            }

            await cleanupContext.SaveChangesAsync();
        }
    }

    private static SMEFLOWSystemContext CreateContext(
        DbContextOptions<SMEFLOWSystemContext> options,
        Guid tenantId)
    {
        return new SMEFLOWSystemContext(
            options,
            new PhaseZeroTestContext.MutableCurrentTenantService(tenantId));
    }
}
