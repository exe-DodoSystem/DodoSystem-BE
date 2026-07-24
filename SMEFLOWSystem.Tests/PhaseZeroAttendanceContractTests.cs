using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SMEFLOWSystem.Application.DTOs;
using SMEFLOWSystem.Application.DTOs.AttendanceDtos;
using SMEFLOWSystem.Application.Interfaces.IServices;
using SMEFLOWSystem.WebAPI.Controllers;

namespace SMEFLOWSystem.Tests;

public sealed class PhaseZeroAttendanceContractTests
{
    [Fact]
    [Trait("Phase", "0")]
    [Trait("Gap", "BE-ATT-02")]
    public async Task BusinessValidation_ReturnsBadRequest_ForJsonTransport()
    {
        var controller = CreateController(
            new InvalidOperationException(
                "FakeGPS: Phát hiện sử dụng phần mềm giả mạo vị trí."));

        var result = await controller.SubmitPunch(new SubmitPunchRequestDto());

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [KnownBugFact("BE-ATT-02")]
    [Trait("Phase", "0")]
    [Trait("Gap", "BE-ATT-02")]
    public async Task BusinessValidation_ReturnsBadRequest_ForMultipartTransport()
    {
        var controller = CreateController(
            new InvalidOperationException(
                "FakeGPS: Phát hiện sử dụng phần mềm giả mạo vị trí."));

        var result = await controller.SubmitPunchForm(
            new SubmitPunchRequestDto(),
            null);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("Phase", "0")]
    [Trait("Gap", "BE-ATT-02")]
    public async Task MissingEmployee_ReturnsNotFound_ForBothTransports(
        bool multipart)
    {
        var controller = CreateController(
            new InvalidOperationException(
                "Employee not found for current user."));

        var result = multipart
            ? await controller.SubmitPunchForm(new SubmitPunchRequestDto(), null)
            : await controller.SubmitPunch(new SubmitPunchRequestDto());

        Assert.IsType<NotFoundObjectResult>(result);
    }

    private static AttendanceController CreateController(Exception exception)
    {
        var userId = Guid.NewGuid();
        var identity = new ClaimsIdentity(
            new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString())
            },
            authenticationType: "PhaseZeroTest");

        return new AttendanceController(new ThrowingAttendanceService(exception))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(identity)
                }
            }
        };
    }

    private sealed class ThrowingAttendanceService : IAttendanceService
    {
        private readonly Exception _exception;

        public ThrowingAttendanceService(Exception exception)
        {
            _exception = exception;
        }

        public Task<RawPunchLogDto> SubmitPunchAsync(
            Guid userId,
            SubmitPunchRequestDto request)
        {
            return Task.FromException<RawPunchLogDto>(_exception);
        }

        public Task<TodayAttendanceDto> GetMyTodayStatusAsync(Guid userId)
            => throw new NotSupportedException();

        public Task<List<MyAttendanceHistoryItemDto>> GetMyHistoryAsync(
            Guid userId,
            int month,
            int year)
            => throw new NotSupportedException();

        public Task<RawPunchLogDto> ManualPunchAsync(ManualPunchRequestDto request)
            => throw new NotSupportedException();

        public Task RecalculateAttendanceAsync(
            Guid employeeId,
            DateOnly fromDate,
            DateOnly toDate)
            => throw new NotSupportedException();

        public Task<TimesheetAppealDto> SubmitAppealAsync(
            Guid userId,
            SubmitAppealRequestDto request)
            => throw new NotSupportedException();

        public Task<List<TimesheetAppealDto>> GetMyAppealsAsync(Guid userId)
            => throw new NotSupportedException();

        public Task<TimesheetAppealDto> ProcessAppealAsync(
            Guid hrUserId,
            Guid appealId,
            ApproveAppealRequestDto request)
            => throw new NotSupportedException();

        public Task<List<TimesheetAppealDto>> GetPendingAppealsAsync()
            => throw new NotSupportedException();

        public Task<AttendanceSettingDto> GetSettingsAsync()
            => throw new NotSupportedException();

        public Task<AttendanceSettingDto> UpdateSettingsAsync(
            UpdateAttendanceSettingRequestDto request)
            => throw new NotSupportedException();

        public Task<List<HRMonthlyReportItemDto>> GetHRMonthlyReportAsync(
            int month,
            int year)
            => throw new NotSupportedException();

        public Task<PublicHolidayDto> CreatePublicHolidayAsync(
            CreatePublicHolidayDto dto)
            => throw new NotSupportedException();

        public Task<List<PublicHolidayDto>> GetPublicHolidaysAsync()
            => throw new NotSupportedException();

        public Task DeletePublicHolidayAsync(Guid id)
            => throw new NotSupportedException();
    }
}
