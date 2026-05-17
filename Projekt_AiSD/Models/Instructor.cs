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

        [JsonPropertyName("preferences_text")]
        public string PreferencesText { get; set; }

        [JsonPropertyName("subjects")]
        public List<string> Subjects { get; set; }

        // ZMIANA: Zamiast "max_hours_per_week" mamy godziny na semestr
        [JsonPropertyName("hours_per_semester")]
        public int HoursPerSemester { get; set; }

        // Nasza kieszeń na przetłumaczone dane od Bielika (z Etapu 2)
        public Preferences ParsedPreferences { get; set; }
    }
}