using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using SMEFLOWSystem.Application.DTOs.AuthDtos;
using SMEFLOWSystem.Application.Interfaces.IServices;
using SMEFLOWSystem.Application.Services;
using System.Security.Claims;

namespace SMEFLOWSystem.WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : Controller
    {
        private readonly IAuthService _authService;
        private readonly IOTPService _otpService;
        private readonly IUserService _userService;
        private readonly IEmailService _emailService;
        public AuthController(IAuthService authService, IOTPService otpService, IUserService userService, IEmailService emailService)
        {
            _authService = authService;
            _otpService = otpService;
            _userService = userService;
            _emailService = emailService;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequestDto request)
        {
            try
            {
                var result = await _authService.RegisterTenantAsync(request);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { Error = ex.Message });
            }
        }


        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequestDto request)
        {
            try
            {
                var user = await _authService.LoginAsync(request);
                return Ok(user);
            }
            catch (Exception ex)
            {
                return BadRequest(new { Error = ex.Message });
            }
        }

        [HttpPost("change-password")]
        [Authorize]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequestDto request)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userIdClaim == null || !Guid.TryParse(userIdClaim, out var userId))
                return Unauthorized("Không tìm thấy user");
            try
            {
                var (isSuccess, message) = await _authService.ChangePasswordAsync(userId, request);
                if (isSuccess)
                    return Ok(new { Message = message });
                else
                    return BadRequest(new { Error = message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Error = ex.Message });
            }
        }

        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequestDto request)
        {
            var user = await _userService.GetUserByEmailAsync(request.Email);
            if (user == null)
                return BadRequest("User không tồn tại");

            var otp = _otpService.GenerateOtp();
            await _otpService.StoreOtpAsync(request.Email, otp);
            await _emailService.SendOtpEmailAsync(request.Email, otp);

            return Ok("OTP đã được gửi đến email của bạn");
        }

        [HttpPost("reset-password-otp")]
        public async Task<IActionResult> ResetPasswordWithOtp([FromBody] ResetPasswordWithOtpDto request)
        {
            if (!await _otpService.VerifyOtpAsync(request.Email, request.Otp))
                return BadRequest("OTP không đúng hoặc hết hạn");

            var user = await _userService.GetUserByEmailAsync(request.Email);
            if (user == null)
                return BadRequest("User không tồn tại");

            await _userService.UpdatePasswordAsync(user.Id, request.NewPassword);
            //await _refreshTokenService.RevokeAllRefreshTokensAsync(user.Id);

            return Ok("Reset mật khẩu thành công");
        }
    }
}
