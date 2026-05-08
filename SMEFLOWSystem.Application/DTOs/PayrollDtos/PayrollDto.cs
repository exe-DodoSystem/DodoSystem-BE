using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SMEFLOWSystem.Application.DTOs.PayrollDtos
{
    public class PayrollDto
    {
        public Guid Id { get; set; }
        public Guid EmployeeId { get; set; }

        public string EmployeeCode { get; set; } = string.Empty;
        public string EmployeeName { get; set; } = string.Empty;
        public string? DepartmentName { get; set; }                                               
        public int Month { get; set; }
        public int Year { get; set; }
        public int StandardWorkingDays { get; set; }
        public int ActualWorkingDays { get; set; }
        public int TotalLateMinutes { get; set; }
        public int TotalEarlyLeaveMinutes { get; set; }
        public int AbsentDays { get; set; }


        public decimal BaseSalarySnapshot { get; set; } // Lương cứng lúc tính
        public decimal BasePay { get; set; }            // Lương theo ngày công (BaseSalary * (Actual/Standard))

        public decimal? Bonus { get; set; }             // Tiền thưởng
        public decimal Deduction { get; set; }          // Tiền phạt (Đi trễ, về sớm, vắng...)

        public decimal TotalSalary { get; set; }        // Thực nhận: BasePay + Bonus - Deduction

        public string Status { get; set; } = string.Empty;

        // Ghi chú & Ngày tháng
        public string? Notes { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

}
