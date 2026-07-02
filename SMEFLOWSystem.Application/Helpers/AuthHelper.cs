using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using SMEFLOWSystem.Core.Entities;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BCryptNet = BCrypt.Net.BCrypt;

namespace SMEFLOWSystem.Application.Helpers
{
    public static class AuthHelper
    {
        public static string GenerateJwtToken(User user, IConfiguration config, bool isExpired = false)
        {
            // 1. Tạo danh sách Claims cơ bản
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Name, user.FullName),    
                
                // [QUAN TRỌNG NHẤT] Phải có TenantId để hệ thống biết User thuộc công ty nào
                new Claim("tenantId", user.TenantId.ToString()),
                new Claim("isExpired", isExpired.ToString().ToLower())
            };

            // 2. Thêm Roles vào Claims
            if (user.UserRoles != null && user.UserRoles.Any())
            {
                foreach (var userRole in user.UserRoles)
                {
                    // Giả sử bảng Role có thuộc tính Name (vd: "TenantAdmin", "Employee")
                    if (userRole.Role != null)
                    {
                        claims.Add(new Claim(ClaimTypes.Role, userRole.Role.Name));
                    }
                }
            }

            // 3. Cấu hình Key và Sigining
            var secretKey = config["Jwt:Secret"];
            if (string.IsNullOrEmpty(secretKey)) throw new Exception("JWT Secret chưa được cấu hình trong appsettings.json");

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            // 4. Tạo Token
            var token = new JwtSecurityToken(
                issuer: config["Jwt:Issuer"],
                audience: config["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddHours(24),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public static string HashPassword(string password)
        {
            return BCryptNet.HashPassword(password);
        }

        public static bool VerifyPassword(string password, string passwordHash)
        {
            return BCryptNet.Verify(password, passwordHash);
        }
        public static string GenerateOrderNumber()
        {
            return $"SUB-{DateTime.UtcNow.Ticks.ToString().Substring(10)}";
        }
    }
}