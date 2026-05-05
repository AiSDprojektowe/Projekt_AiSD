using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace Projekt_AiSD.Models
{
    public class Instructor
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("subjects")]
        public List<string> Subjects { get; set; }

        [JsonPropertyName("availability")]
        public Dictionary<string, List<int>> Availability { get; set; }

        [JsonPropertyName("preferences_text")]
        public string PreferencesText { get; set; }

        [JsonPropertyName("max_hours_per_week")]
        public int MaxHoursPerWeek { get; set; }
    }
}
