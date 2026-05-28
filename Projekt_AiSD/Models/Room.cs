using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;


namespace Projekt_AiSD.Models
{
    public class Room
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("capacity")]
        public int Capacity { get; set; }

        [JsonPropertyName("availability")]
        public Dictionary<string, HashSet<int>> Availability { get; set; } = new Dictionary<string, HashSet<int>>();
    }
}