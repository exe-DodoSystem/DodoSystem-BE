using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SMEFLOWSystem.Application.DTOs.ShiftDtos
{
    public class ShiftCreateDto
    {
        [Required]
        [MaxLength(50)]
        public string Code { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [Range(0, 1440)]
        public int GracePeriodMinutes { get; set; }

        public bool IsCrossDay { get; set; }

        [Required]
        public List<ShiftSegmentCreateDto> Segments { get; set; } = new List<ShiftSegmentCreateDto>();
    }
}
