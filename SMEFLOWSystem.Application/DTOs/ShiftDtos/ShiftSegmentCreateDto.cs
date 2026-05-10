using System;

namespace SMEFLOWSystem.Application.DTOs.ShiftDtos
{
    public class ShiftSegmentCreateDto
    {
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public int StartDayOffset { get; set; }
        public int EndDayOffset { get; set; }
    }
}
