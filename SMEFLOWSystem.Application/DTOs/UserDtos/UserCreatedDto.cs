using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SMEFLOWSystem.Application.DTOs.UserDtos
{
    public class UserCreatedDto
    {
        public string Email { get; set; } = string.Empty;

        public string PasswordHash { get; set; } = string.Empty;

        public string FullName { get; set; } = string.Empty;

        public string Phone { get; set; } = string.Empty;

        public bool IsActive { get; set; }
        public bool IsVerified { get; set; }
    }
}
