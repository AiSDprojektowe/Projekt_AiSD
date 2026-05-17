using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;


namespace Projekt_AiSD.Models
{
    public class UniversityData
    {
        // Ignorujemy metadata, chyba że chcecie je gdzieś wyświetlać
        [JsonPropertyName("instructors")]
        public List<Instructor> Instructors { get; set; }

        [JsonPropertyName("rooms")]
        public List<Room> Rooms { get; set; }

        [JsonPropertyName("student_groups")]
        public List<StudentGroup> StudentGroups { get; set; } // NOWOŚĆ!

        [JsonPropertyName("courses")]
        public List<Course> Courses { get; set; }
    }
}