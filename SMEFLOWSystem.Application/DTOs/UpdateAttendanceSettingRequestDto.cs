using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SMEFLOWSystem.Application.DTOs
{
    public class UpdateAttendanceSettingRequestDto
    {
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public int CheckInRadiusMeters { get; set; }
        public TimeSpan? WorkStartTime { get; set; }
        public TimeSpan? WorkEndTime { get; set; }
        public TimeSpan DayStartCutOffTime { get; set; }
        public int LateThresholdMinutes { get; set; }
        public int EarlyLeaveThresholdMinutes { get; set; }
        public int MinimumOTMinutes { get; set; }
        public int OTBlockMinutes { get; set; }
    }
}
