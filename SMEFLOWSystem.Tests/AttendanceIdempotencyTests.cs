using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using SMEFLOWSystem.Application.DTOs.AttendanceDtos;
using SMEFLOWSystem.Application.Exceptions;
using SMEFLOWSystem.Application.Interfaces.IRepositories;
using SMEFLOWSystem.Application.Interfaces.IServices;
using SMEFLOWSystem.Application.Options;
using SMEFLOWSystem.Application.Services;
using SMEFLOWSystem.Core.Entities;
using SMEFLOWSystem.Infrastructure.Data.Configurations;
using SMEFLOWSystem.Infrastructure.Repositories;
using SMEFLOWSystem.SharedKernel.Interfaces;

namespace SMEFLOWSystem.Tests;

public sealed class AttendanceIdempotencyTests
{
    [Theory]
    [InlineData(
        RawPunchLogConfiguration.IdempotencyIndexName,
        true)]
    [InlineData("UX_SomeOtherConstraint", false)]
    public void Repository_OnlyClassifiesItsIdempotencyConstraintAsRetry(
        string constraintName,
        bool expected)
    {
        var postgresException = new PostgresException(
            messageText: "duplicate key",
            severity: "ERROR",
            invariantSeverity: "ERROR",
            sqlState: PostgresErrorCodes.UniqueViolation,
            constraintName: constraintName);
        var updateException = new DbUpdateException(
            "Database update failed.",
            postgresException);
        var classifier = typeof(RawPunchLogRepository).GetMethod(
            "IsIdempotencyConstraintViolation",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(classifier);
        Assert.Equal(
            expected,
            Assert.IsType<bool>(
                classifier!.Invoke(null, new object[] { updateException })));
    }

    [Fact]
    public async Task SequentialRetry_ReturnsSameRowWithoutSecondUploadOrNotification()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var employee = Employee(tenantId, userId);
        var repository = new AtomicPunchLogRepository();
        var cloudinary = new RecordingCloudinaryService();
        var realtime = new RecordingRealtimeNotificationService();
        var service = CreateService(
            repository,
            new EmployeeRepositoryStub(employee),
            new CurrentTenantServiceStub(tenantId),
            cloudinary,
            realtime);
        var requestId = Guid.NewGuid();

        var first = await service.SubmitPunchAsync(
            userId,
            Request(
                requestId.ToString("B").ToUpperInvariant(),
                selfieBase64: "first-image"));
        var retry = await service.SubmitPunchAsync(
            userId,
            Request(
                requestId.ToString("D"),
                selfieBase64: "retry-image"));

        Assert.Equal(first.Id, retry.Id);
        Assert.Equal(requestId.ToString("D"), first.ClientRequestId);
        Assert.Equal(first.ClientRequestId, retry.ClientRequestId);
        Assert.Equal(1, repository.StoredCount);
        Assert.Equal(1, cloudinary.UploadCalls);
        Assert.Equal(1, realtime.PunchReceivedCalls);
    }

    [Fact]
    public async Task ConcurrentRetry_CreatesOneRowAndOneNotification()
    {
        const int requestCount = 8;
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var employee = Employee(tenantId, userId);
        var repository = new AtomicPunchLogRepository
        {
            ForcedConcurrentLookups = requestCount
        };
        var realtime = new RecordingRealtimeNotificationService();
        var service = CreateService(
            repository,
            new EmployeeRepositoryStub(employee),
            new CurrentTenantServiceStub(tenantId),
            new RecordingCloudinaryService(),
            realtime);
        var requestId = Guid.NewGuid().ToString("D");

        var results = await Task.WhenAll(
            Enumerable.Range(0, requestCount)
                .Select(_ => service.SubmitPunchAsync(
                    userId,
                    Request(requestId))));

        Assert.Single(results.Select(result => result.Id).Distinct());
        Assert.Equal(1, repository.StoredCount);
        Assert.Equal(requestCount, repository.IdempotentInsertCalls);
        Assert.Equal(1, realtime.PunchReceivedCalls);
    }

    [Fact]
    public async Task DifferentKeys_CreateIndependentRows()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var repository = new AtomicPunchLogRepository();
        var service = CreateService(
            repository,
            new EmployeeRepositoryStub(Employee(tenantId, userId)),
            new CurrentTenantServiceStub(tenantId));

        var first = await service.SubmitPunchAsync(
            userId,
            Request(Guid.NewGuid().ToString("D")));
        var second = await service.SubmitPunchAsync(
            userId,
            Request(Guid.NewGuid().ToString("D")));

        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(2, repository.StoredCount);
    }

    [Fact]
    public async Task SameKey_ForDifferentEmployees_CreatesIndependentRows()
    {
        var tenantId = Guid.NewGuid();
        var firstUserId = Guid.NewGuid();
        var secondUserId = Guid.NewGuid();
        var repository = new AtomicPunchLogRepository();
        var service = CreateService(
            repository,
            new EmployeeRepositoryStub(
                Employee(tenantId, firstUserId),
                Employee(tenantId, secondUserId)),
            new CurrentTenantServiceStub(tenantId));
        var requestId = Guid.NewGuid().ToString("D");

        var first = await service.SubmitPunchAsync(
            firstUserId,
            Request(requestId));
        var second = await service.SubmitPunchAsync(
            secondUserId,
            Request(requestId));

        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(2, repository.StoredCount);
    }

    [Fact]
    public async Task SameKey_ForDifferentTenants_CreatesIndependentRows()
    {
        var firstTenantId = Guid.NewGuid();
        var secondTenantId = Guid.NewGuid();
        var firstUserId = Guid.NewGuid();
        var secondUserId = Guid.NewGuid();
        var repository = new AtomicPunchLogRepository();
        var employees = new EmployeeRepositoryStub(
            Employee(firstTenantId, firstUserId),
            Employee(secondTenantId, secondUserId));
        var firstService = CreateService(
            repository,
            employees,
            new CurrentTenantServiceStub(firstTenantId));
        var secondService = CreateService(
            repository,
            employees,
            new CurrentTenantServiceStub(secondTenantId));
        var requestId = Guid.NewGuid().ToString("D");

        var first = await firstService.SubmitPunchAsync(
            firstUserId,
            Request(requestId));
        var second = await secondService.SubmitPunchAsync(
            secondUserId,
            Request(requestId));

        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(2, repository.StoredCount);
    }

    [Fact]
    public async Task LegacyClient_UsesConfiguredDedupWindow()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var repository = new AtomicPunchLogRepository();
        var cloudinary = new RecordingCloudinaryService();
        var realtime = new RecordingRealtimeNotificationService();
        var service = CreateService(
            repository,
            new EmployeeRepositoryStub(Employee(tenantId, userId)),
            new CurrentTenantServiceStub(tenantId),
            cloudinary,
            realtime,
            dedupWindowMinutes: 2);

        var first = await service.SubmitPunchAsync(
            userId,
            Request(clientRequestId: null, selfieBase64: "first-image"));
        var retry = await service.SubmitPunchAsync(
            userId,
            Request(clientRequestId: null, selfieBase64: "retry-image"));

        Assert.Equal(first.Id, retry.Id);
        Assert.Equal(1, repository.StoredCount);
        Assert.Equal(1, cloudinary.UploadCalls);
        Assert.Equal(1, realtime.PunchReceivedCalls);
    }

    [Fact]
    public async Task ClientRequestId_OverMaximumLength_IsRejectedBeforeUpload()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var cloudinary = new RecordingCloudinaryService();
        var repository = new AtomicPunchLogRepository();
        var service = CreateService(
            repository,
            new EmployeeRepositoryStub(Employee(tenantId, userId)),
            new CurrentTenantServiceStub(tenantId),
            cloudinary);

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.SubmitPunchAsync(
                userId,
                Request(new string('x', 101), selfieBase64: "image")));

        Assert.Contains("100", exception.Message);
        Assert.Equal(
            "ATTENDANCE_INVALID_CLIENT_REQUEST_ID",
            exception.ErrorCode);
        Assert.Equal(0, repository.StoredCount);
        Assert.Equal(0, cloudinary.UploadCalls);
    }

    private static AttendanceService CreateService(
        IRawPunchLogRepository repository,
        IEmployeeRepository employeeRepository,
        ICurrentTenantService currentTenant,
        RecordingCloudinaryService? cloudinary = null,
        RecordingRealtimeNotificationService? realtime = null,
        int dedupWindowMinutes = 2)
    {
        return new AttendanceService(
            repository,
            employeeRepository,
            null!,
            new AttendanceSettingRepositoryStub(),
            currentTenant,
            null!,
            cloudinary ?? new RecordingCloudinaryService(),
            null!,
            null!,
            realtime ?? new RecordingRealtimeNotificationService(),
            NullLogger<AttendanceService>.Instance,
            null!,
            null!,
            Options.Create(new AttendanceResolutionOptions
            {
                DedupWindowMinutes = dedupWindowMinutes
            }));
    }

    private static SubmitPunchRequestDto Request(
        string? clientRequestId,
        string? selfieBase64 = null)
    {
        return new SubmitPunchRequestDto
        {
            ClientRequestId = clientRequestId,
            SelfieBase64 = selfieBase64,
            PunchType = "Auto"
        };
    }

    private static Employee Employee(Guid tenantId, Guid userId)
    {
        return new Employee
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = userId,
            FullName = $"Employee {userId:N}",
            Phone = "0900000000",
            Email = $"{userId:N}@example.test",
            HireDate = new DateOnly(2026, 1, 1),
            BaseSalary = 1,
            Status = "Working",
            IsDeleted = false
        };
    }

    private sealed class AtomicPunchLogRepository : IRawPunchLogRepository
    {
        private readonly ConcurrentDictionary<
            (Guid TenantId, Guid EmployeeId, string ClientRequestId),
            RawPunchLog> _keyedLogs = new();
        private readonly List<RawPunchLog> _legacyLogs = [];
        private readonly object _legacyLock = new();
        private readonly TaskCompletionSource _concurrentLookupGate =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _lookupParticipants;
        private int _idempotentInsertCalls;

        public int ForcedConcurrentLookups { get; init; }
        public int IdempotentInsertCalls => _idempotentInsertCalls;

        public int StoredCount
        {
            get
            {
                lock (_legacyLock)
                {
                    return _keyedLogs.Count + _legacyLogs.Count;
                }
            }
        }

        public Task AddAsync(RawPunchLog punchLog)
        {
            lock (_legacyLock)
            {
                _legacyLogs.Add(punchLog);
            }

            return Task.CompletedTask;
        }

        public Task<(RawPunchLog PunchLog, bool IsNew)> AddIdempotentAsync(
            RawPunchLog punchLog)
        {
            Interlocked.Increment(ref _idempotentInsertCalls);

            if (punchLog.ClientRequestId == null)
            {
                lock (_legacyLock)
                {
                    _legacyLogs.Add(punchLog);
                }

                return Task.FromResult((punchLog, true));
            }

            var key = (
                punchLog.TenantId,
                punchLog.EmployeeId,
                punchLog.ClientRequestId);
            var winner = _keyedLogs.GetOrAdd(key, punchLog);
            return Task.FromResult(
                (winner, ReferenceEquals(winner, punchLog)));
        }

        public async Task<RawPunchLog?> GetByClientRequestIdAsync(
            Guid tenantId,
            Guid employeeId,
            string clientRequestId)
        {
            var key = (tenantId, employeeId, clientRequestId);
            _keyedLogs.TryGetValue(key, out var existing);

            if (existing == null && ForcedConcurrentLookups > 0)
            {
                var participants =
                    Interlocked.Increment(ref _lookupParticipants);
                if (participants >= ForcedConcurrentLookups)
                {
                    _concurrentLookupGate.TrySetResult();
                }

                await _concurrentLookupGate.Task.WaitAsync(
                    TimeSpan.FromSeconds(5));
            }

            return existing;
        }

        public Task<RawPunchLog?> GetLatestByEmployeePunchTypeAsync(
            Guid tenantId,
            Guid employeeId,
            string punchType,
            DateTime sinceUtc)
        {
            lock (_legacyLock)
            {
                return Task.FromResult(
                    _legacyLogs
                        .Where(log =>
                            log.TenantId == tenantId &&
                            log.EmployeeId == employeeId &&
                            log.PunchType == punchType &&
                            log.Timestamp >= sinceUtc)
                        .OrderByDescending(log => log.Timestamp)
                        .FirstOrDefault());
            }
        }

        public Task<List<RawPunchLog>> GetUnprocessedBatchAsync(int batchSize)
            => throw new NotSupportedException();

        public Task MarkProcessedAsync(IEnumerable<Guid> punchLogIds)
            => throw new NotSupportedException();

        public Task MarkUnprocessedForRecalculateAsync(
            Guid employeeId,
            DateTime fromDate,
            DateTime toDate)
            => throw new NotSupportedException();

        public Task<List<RawPunchLog>> GetByEmployeeAndDateRangeAsync(
            Guid employeeId,
            DateTime fromDate,
            DateTime toDate)
            => throw new NotSupportedException();

        public Task IncrementRetryCountAsync(IEnumerable<Guid> logIds)
            => throw new NotSupportedException();
    }

    private sealed class EmployeeRepositoryStub : IEmployeeRepository
    {
        private readonly Dictionary<Guid, Employee> _employeesByUserId;

        public EmployeeRepositoryStub(params Employee[] employees)
        {
            _employeesByUserId = employees.ToDictionary(
                employee => employee.UserId!.Value);
        }

        public Task<Employee?> GetByUserIdAsync(Guid userId)
        {
            _employeesByUserId.TryGetValue(userId, out var employee);
            return Task.FromResult(employee);
        }

        public Task<Employee?> GetByIdAsync(Guid id)
            => throw new NotSupportedException();

        public Task<Employee?> GetByIdIncludeDeletedAsync(
            Guid id,
            Guid tenantId)
            => throw new NotSupportedException();

        public Task<List<Employee>> GetAllActiveEmployeeByTenantId(Guid tenantId)
            => throw new NotSupportedException();

        public Task<List<Employee>> GetByIdsAsync(List<Guid> employeeIds)
            => throw new NotSupportedException();

        public Task AddAsync(Employee employee)
            => throw new NotSupportedException();

        public Task<Employee> UpdateAsync(Employee employee)
            => throw new NotSupportedException();

        public Task SoftDeleteResignedAsync(Employee employee)
            => throw new NotSupportedException();

        public Task<List<Employee>> GetByDepartmentIdAsync(Guid departmentId)
            => throw new NotSupportedException();

        public Task<(List<Employee> Items, int TotalCount)> GetPagedAsync(
            Guid tenantId,
            Guid? departmentId,
            Guid? positionId,
            int? roleId,
            string? status,
            bool includeResigned,
            bool includeDeleted,
            string? search,
            int pageNumber,
            int pageSize,
            string? sortBy,
            string? sortDir)
            => throw new NotSupportedException();
    }

    private sealed class AttendanceSettingRepositoryStub
        : IAttendanceSettingRepository
    {
        public Task<TenantAttendanceSetting?> GetByTenantIdAsync(Guid tenantId)
            => Task.FromResult<TenantAttendanceSetting?>(null);

        public Task UpsertAsync(TenantAttendanceSetting setting)
            => throw new NotSupportedException();
    }

    private sealed class CurrentTenantServiceStub : ICurrentTenantService
    {
        public CurrentTenantServiceStub(Guid tenantId)
        {
            TenantId = tenantId;
        }

        public Guid? TenantId { get; private set; }

        public void SetTenantId(Guid? tenantId)
        {
            TenantId = tenantId;
        }
    }

    private sealed class RecordingCloudinaryService : ICloudinaryService
    {
        private int _uploadCalls;
        public int UploadCalls => _uploadCalls;

        public Task<string> UploadBase64Async(
            string base64Image,
            string folder)
        {
            var call = Interlocked.Increment(ref _uploadCalls);
            return Task.FromResult($"https://image.test/{call}");
        }

        public Task<string> UploadFileAsync(
            Stream fileStream,
            string fileName,
            string folder)
            => throw new NotSupportedException();

        public Task DeleteAsync(string publicId)
            => throw new NotSupportedException();
    }

    private sealed class RecordingRealtimeNotificationService
        : IRealtimeNotificationService
    {
        private int _punchReceivedCalls;
        public int PunchReceivedCalls => _punchReceivedCalls;

        public Task NotifyPunchReceivedAsync(Guid userId, object data)
        {
            Interlocked.Increment(ref _punchReceivedCalls);
            return Task.CompletedTask;
        }

        public Task NotifyAttendanceUpdatedAsync(
            Guid userId,
            Guid tenantId,
            object data)
            => throw new NotSupportedException();

        public Task NotifyAppealProcessedAsync(Guid userId, object data)
            => throw new NotSupportedException();

        public Task NotifyPayrollPublishedAsync(Guid userId, object data)
            => throw new NotSupportedException();

        public Task NotifyDashboardRefreshAsync(Guid tenantId)
            => throw new NotSupportedException();

        public Task NotifyAppealSubmittedAsync(Guid tenantId, object data)
            => throw new NotSupportedException();

        public Task NotifyPayrollPaidAsync(Guid userId, object data)
            => throw new NotSupportedException();

        public Task NotifyShiftAssignedAsync(Guid userId, object data)
            => throw new NotSupportedException();

        public Task NotifyBonusDeductionEntryAddedAsync(Guid userId, object data)
            => throw new NotSupportedException();

        public Task NotifyAttendanceManualAdjustedAsync(
            Guid userId,
            object data)
            => throw new NotSupportedException();

        public Task NotifyPayrollGeneratedAsync(Guid userId, object data)
            => throw new NotSupportedException();

        public Task NotifyEmployeeOnboardedAsync(Guid tenantId, object data)
            => throw new NotSupportedException();
    }
}
