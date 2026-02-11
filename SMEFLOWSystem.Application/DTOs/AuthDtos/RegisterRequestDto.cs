using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SMEFLOWSystem.Application.DTOs.AuthDtos
{
    public class RegisterRequestDto
    {
        public string CompanyName { get; set; } = string.Empty;
        public int[] ModuleIds { get; set; } = Array.Empty<int>();
        public string AdminFullName { get; set; } = string.Empty;
        public string AdminEmail { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
    }
}
