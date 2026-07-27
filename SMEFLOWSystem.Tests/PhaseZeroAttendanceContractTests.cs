using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using SMEFLOWSystem.Application.DTOs;
using SMEFLOWSystem.Application.DTOs.AttendanceDtos;
using SMEFLOWSystem.Application.Exceptions;
using SMEFLOWSystem.Application.Interfaces.IServices;
using SMEFLOWSystem.WebAPI.Controllers;
using SMEFLOWSystem.WebAPI.Exceptions;

namespace SMEFLOWSystem.Tests;

public sealed class PhaseZeroAttendanceContractTests
{
    [Theory]
    [InlineData(false, "FakeGPS: Phát hiện sử dụng phần mềm giả mạo vị trí.")]
    [InlineData(true, "FakeGPS: Phát hiện sử dụng phần mềm giả mạo vị trí.")]
    [InlineData(false, "BatBuocGPS: Vui lòng bật định vị GPS để chấm công.")]
    [InlineData(true, "BatBuocGPS: Vui lòng bật định vị GPS để chấm công.")]
    [InlineData(false, "NgoaiVung: Bạn đang ở ngoài vùng chấm công cho phép.")]
    [InlineData(true, "NgoaiVung: Bạn đang ở ngoài vùng chấm công cho phép.")]
    [Trait("Phase", "0")]
    [Trait("Gap", "BE-ATT-02")]
    public async Task BusinessValidation_ReturnsBadRequestWithMessage_ForBothTransports(
        bool multipart,
        string message)
    {
        var controller =
            CreateController(new BusinessRuleException(
                message,
                "ATTENDANCE_VALIDATION_FAILED"));

        using var problem = await ExecuteThroughExceptionHandlerAsync(
            controller,
            multipart);

        Assert.Equal(StatusCodes.Status400BadRequest, controller.Response.StatusCode);
        Assert.Equal(message, problem.RootElement.GetProperty("error").GetString());
        Assert.False(string.IsNullOrWhiteSpace(
            problem.RootElement.GetProperty("traceId").GetString()));
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
            new KeyNotFoundException(
                "Employee not found for current user."));

        using var problem = await ExecuteThroughExceptionHandlerAsync(
            controller,
            multipart);

        Assert.Equal(StatusCodes.Status404NotFound, controller.Response.StatusCode);
        Assert.Equal(
            "Employee not found for current user.",
            problem.RootElement.GetProperty("error").GetString());
        Assert.False(string.IsNullOrWhiteSpace(
            problem.RootElement.GetProperty("traceId").GetString()));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("Phase", "0")]
    [Trait("Gap", "BE-ATT-02")]
    public async Task ValidRequest_ReturnsOk_ForBothTransports(bool multipart)
    {
        var controller = CreateController();

        var result = multipart
            ? await controller.SubmitPunchForm(new SubmitPunchRequestDto(), null)
            : await controller.SubmitPunch(new SubmitPunchRequestDto());

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(
            "Punch submitted successfully",
            ReadStringProperty(ok.Value, "Message"));
    }

    private static AttendanceController CreateController(
        Exception? exception = null)
    {
        var userId = Guid.NewGuid();
        var identity = new ClaimsIdentity(
            new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString())
            },
            authenticationType: "PhaseZeroTest");

        return new AttendanceController(new StubAttendanceService(exception))
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

    private static async Task<JsonDocument> ExecuteThroughExceptionHandlerAsync(
        AttendanceController controller,
        bool multipart)
    {
        controller.Response.Body = new MemoryStream();

        try
        {
            _ = multipart
                ? await controller.SubmitPunchForm(
                    new SubmitPunchRequestDto(),
                    null)
                : await controller.SubmitPunch(new SubmitPunchRequestDto());
        }
        catch (Exception exception)
        {
            var handler = new ApiExceptionHandler(
                NullLogger<ApiExceptionHandler>.Instance);
            var handled = await handler.TryHandleAsync(
                controller.HttpContext,
                exception,
                CancellationToken.None);
            Assert.True(handled);
        }

        controller.Response.Body.Position = 0;
        return await JsonDocument.ParseAsync(controller.Response.Body);
    }

    private static string? ReadStringProperty(object? value, string propertyName)
    {
        return value?
            .GetType()
            .GetProperty(propertyName)?
            .GetValue(value) as string;
    }

    private sealed class StubAttendanceService : IAttendanceService
    {
        private readonly Exception? _exception;

        public StubAttendanceService(Exception? exception)
        {
            _exception = exception;
        }

        public Task<RawPunchLogDto> SubmitPunchAsync(
            Guid userId,
            SubmitPunchRequestDto request)
        {
            return _exception == null
                ? Task.FromResult(new RawPunchLogDto())
                : Task.FromException<RawPunchLogDto>(_exception);
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
