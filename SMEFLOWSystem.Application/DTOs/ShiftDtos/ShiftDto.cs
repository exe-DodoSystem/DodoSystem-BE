using System;
using System.Collections.Generic;

namespace SMEFLOWSystem.Application.DTOs.ShiftDtos
{
    public class ShiftDto
    {
        public Guid Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int GracePeriodMinutes { get; set; }
        public bool IsCrossDay { get; set; }
        public bool IsDeleted { get; set; }
        public List<ShiftSegmentDto> Segments { get; set; } = new List<ShiftSegmentDto>();
    }
}
