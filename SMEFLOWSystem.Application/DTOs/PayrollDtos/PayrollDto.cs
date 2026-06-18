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


        public decimal TotalOTHours { get; set; }

        public decimal BaseSalarySnapshot { get; set; } // Lương cứng lúc tính
        public decimal BasePay { get; set; }            // Lương theo ngày công (BaseSalary * (Actual/Standard))
        public decimal OTPay { get; set; }              // Lương OT
        public decimal PenaltyFee { get; set; }         // Phạt

        public decimal StructuredBonus { get; set; }
        public decimal StructuredDeduction { get; set; }

        public decimal? CustomBonus { get; set; }       // Tiền thưởng
        public decimal CustomDeduction { get; set; }    // Khấu trừ khác

        public decimal NetSalary { get; set; }          // Thực nhận

        public ShareKernel.Common.Enum.PayrollStatus Status { get; set; } = ShareKernel.Common.Enum.PayrollStatus.Draft;

        // Ghi chú & Ngày tháng
        public string? Notes { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public bool IsTimesheetBased { get; set; } = true;

        public List<BonusDeductionEntryDto> BonusEntries { get; set; } = new();
        public List<BonusDeductionEntryDto> DeductionEntries { get; set; } = new();
    }

}
