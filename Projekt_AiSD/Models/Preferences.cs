using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace Projekt_AiSD.Models
{
    public class Preferences
    {
        [JsonPropertyName("preferred_days")]
        public List<string> PreferredDays { get; set; } = new List<string>();
        
        [JsonPropertyName("preferred_hours_start")]
        public List<int> PreferredHoursStart { get; set; } = new List<int>();
        
        [JsonPropertyName("preferred_hours_end")]
        public List<int> PreferredHoursEnd { get; set; } = new List<int>();
        
        [JsonPropertyName("forbidden_slots")]
        public List<string> ForbiddenSlots { get; set; }
        
        [JsonPropertyName("min_start_hour")]
        public List<string> MinStartHour { get; set; }

        [JsonPropertyName("max_end_hour")]
        public List<string> MaxEndHour { get; set; }

    }
}