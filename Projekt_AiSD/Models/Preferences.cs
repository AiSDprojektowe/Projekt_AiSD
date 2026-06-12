using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Projekt_AiSD.Models
{
    public class Preferences
    {
        [JsonPropertyName("preferred_days")]
        public List<string> PreferredDays { get; set; } = new List<string>();

        [JsonPropertyName("preferred_hours_start")]
        public int PreferredHoursStart { get; set; }

        [JsonPropertyName("preferred_hours_end")]
        public int PreferredHoursEnd { get; set; }


        [JsonPropertyName("forbidden_slots")]
        public Dictionary<string, List<int>> ForbiddenSlots { get; set; } = new Dictionary<string, List<int>>();

        [JsonPropertyName("min_start_hour")]
        public int MinStartHour { get; set; }

        [JsonPropertyName("max_end_hour")]
        public int MaxEndHour { get; set; }
    }
}