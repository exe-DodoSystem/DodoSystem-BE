using System;
using System.Collections.Generic;

namespace SMEFLOWSystem.Application.DTOs.ShiftDtos
{
    public class WorkDayDto
    {
        public DateOnly Date { get; set; }
        public string DayOfWeekVi { get; set; } = string.Empty; // "Thứ Hai", "Thứ Ba", ..., "Chủ Nhật"
        public bool IsWorkDay { get; set; }        // false = ngày nghỉ theo ca
        public bool IsHoliday { get; set; }        // true = trùng ngày lễ
        public string? HolidayName { get; set; }
        public string? ShiftName { get; set; }
        public string? ShiftCode { get; set; }
        public List<ShiftSegmentDto> Segments { get; set; } = new();
    }
}
