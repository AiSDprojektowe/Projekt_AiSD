using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace Projekt_AiSD.Models
{
    public class Preferences
    {
        [JsonPropertyName("preffered_days")]
        public List<string> PrefferedDays { get; set; }
        
        [JsonPropertyName("preffered_hours_start")]
        public List<string> PrefferedHoursStart { get; set; }
        
        [JsonPropertyName("preffered_hours_end")]
        public List<string> PrefferedHoursEnd { get; set; }
        
        [JsonPropertyName("forbidden_slots")]
        public List<string> ForbiddenSlots { get; set; }
        
        [JsonPropertyName("min_start_hour")]
        public List<string> MinStartHour { get; set; }
    }
}