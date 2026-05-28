using System;

namespace SMEFLOWSystem.Application.DTOs.ShiftDtos
{
    public class DayCreateDto
    {
        public int DayIndex { get; set; }
        public Guid? ScheduledShiftId { get; set; }
    }
}
