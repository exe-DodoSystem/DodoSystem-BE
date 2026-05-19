using System;

namespace SMEFLOWSystem.Application.DTOs.ShiftDtos
{
    public class ShiftSegmentDto
    {
        public Guid Id { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public int StartDayOffset { get; set; }
        public int EndDayOffset { get; set; }
    }
}
