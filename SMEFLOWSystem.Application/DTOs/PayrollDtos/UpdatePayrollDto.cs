using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SMEFLOWSystem.Application.DTOs.PayrollDtos
{
    public class UpdatePayrollDto
    {
        public decimal? CustomBonus { get; set; }            
        public decimal? CustomDeduction { get; set; }

        public string? Reason { get; set; } // Lý do thay đổi (dành cho việc chỉnh sửa phạt/thưởng)
    }
}
