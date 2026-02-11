using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SMEFLOWSystem.Application.DTOs.UserDtos
{
    public class LoginUserDto
    {
        public string FullName { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public bool IsDeleted { get; set; }
        public string Token { get; set; } = string.Empty;   

        public string TenantName { get; set; } = string.Empty;
    }
}
