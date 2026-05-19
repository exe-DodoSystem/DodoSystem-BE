using System;
using System.Collections.Generic;

namespace SMEFLOWSystem.Application.DTOs.ShiftDtos
{
    public class ShiftPatternDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int CycleLengthDays { get; set; }
        public bool IsDeleted { get; set; }
        public List<ShiftPatternDayDto> Days { get; set; } = new List<ShiftPatternDayDto>();
    }
}
