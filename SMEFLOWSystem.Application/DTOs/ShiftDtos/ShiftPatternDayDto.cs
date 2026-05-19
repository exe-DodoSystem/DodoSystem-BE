using System;

namespace SMEFLOWSystem.Application.DTOs.ShiftDtos
{
    public class ShiftPatternDayDto
    {
        public Guid Id { get; set; }
        public int DayIndex { get; set; }
        public Guid? ScheduledShiftId { get; set; }
        
        // Trả về kèm Shift details cho FE dễ render
        public ShiftDto? ScheduledShift { get; set; }
    }
}
