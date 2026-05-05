using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace Projekt_AiSD.Models
{
    public class Course
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("students")]
        public int Students { get; set; }

        [JsonPropertyName("hours_per_week")]
        public int HoursPerWeek { get; set; }

        [JsonPropertyName("required_room_type")]
        public string RequiredRoomType { get; set; }
    }
}
