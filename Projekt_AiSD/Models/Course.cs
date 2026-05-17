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

        [JsonPropertyName("subject_id")]
        public string SubjectId { get; set; } // ZMIANA

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("group_id")]
        public string GroupId { get; set; } // NOWOŚĆ!

        [JsonPropertyName("students")]
        public int Students { get; set; }

        [JsonPropertyName("hours_per_semester")]
        public int HoursPerSemester { get; set; } // ZMIANA

        [JsonPropertyName("required_room_type")]
        public string RequiredRoomType { get; set; }
    }
}