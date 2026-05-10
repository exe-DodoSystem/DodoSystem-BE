using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SMEFLOWSystem.Application.DTOs.ShiftDtos
{
    public class ShiftPatternCreateDto
    {
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [Range(1, 365)]
        public int CycleLengthDays { get; set; }

        [Required]
        public List<DayCreateDto> Days { get; set; } = new List<DayCreateDto>();
    }
}
