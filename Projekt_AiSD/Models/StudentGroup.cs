using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace Projekt_AiSD.Models
{
    public class StudentGroup
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("year")]
        public int Year { get; set; }

        [JsonPropertyName("students")]
        public int Students { get; set; }
    }
}