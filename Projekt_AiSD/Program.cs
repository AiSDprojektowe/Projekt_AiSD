using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Collections.Generic;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("Rozpoczynam wczytywanie danych...");

        string filePath = "C:\\Users\\Marek\\Desktop\\AiSD_ntk\\Projekt_AiSD\\Projekt_AiSD\\dane_json.txt";
        string jsonText = File.ReadAllText(filePath);

        UniversityData data = JsonSerializer.Deserialize<UniversityData>(jsonText);

        Console.WriteLine($"Wczytano prowadzących: {data.Instructors.Count}");
        Console.WriteLine($"Wczytano sal: {data.Rooms.Count}");
        Console.WriteLine($"Wczytano przedmiotów: {data.Courses.Count}");

        Console.WriteLine($"Pierwszy prowadzący to: {data.Instructors[0].Name}, uczy: {data.Instructors[0].Subjects[0]}");

        Console.ReadLine();
        Console.WriteLine("kurwa");
    }
}

// Główny kontener na wszystko
public class UniversityData
{
    [JsonPropertyName("instructors")]
    public List<Instructor> Instructors { get; set; }

    [JsonPropertyName("rooms")]
    public List<Room> Rooms { get; set; }

    [JsonPropertyName("courses")]
    public List<Course> Courses { get; set; }
}

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

public class Room
{
    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; }

    [JsonPropertyName("capacity")]
    public int Capacity { get; set; }

    [JsonPropertyName("availability")]
    public Dictionary<string, List<int>> Availability { get; set; }
}

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