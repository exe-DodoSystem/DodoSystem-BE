using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SMEFLOWSystem.Application.DTOs.ShiftDtos
{
    public class ShiftAssignmentBulkCreateDto
    {
        [Required]
        public Guid ShiftPatternId { get; set; }

        [Required]
        [MinLength(1, ErrorMessage = "Must provide at least one employee id")]
        public List<Guid> EmployeeIds { get; set; } = new List<Guid>();

        [Required]
        public DateOnly EffectiveStartDate { get; set; }
    }
}
