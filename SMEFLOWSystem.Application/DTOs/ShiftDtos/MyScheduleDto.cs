using System;
using System.Collections.Generic;

namespace SMEFLOWSystem.Application.DTOs.ShiftDtos
{
    public class MyScheduleDto
    {
        public Guid AssignmentId { get; set; }
        public string ShiftPatternName { get; set; } = string.Empty;
        public DateOnly EffectiveStartDate { get; set; }
        public DateOnly? EffectiveEndDate { get; set; }
        public DateOnly FromDate { get; set; }
        public DateOnly ToDate { get; set; }
        public int TotalWorkDays { get; set; }    // tổng ngày có ca trong range
        public List<WorkDayDto> Days { get; set; } = new();
    }
}
