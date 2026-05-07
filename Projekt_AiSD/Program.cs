using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Collections.Generic;
using Projekt_AiSD.Models;
using Projekt_AiSD.Modules;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("Rozpoczynam wczytywanie danych...");

        string filePath = "C:\\Users\\Marek\\Desktop\\AiSD_ntk\\Projekt_AiSD\\Projekt_AiSD\\data.json";
        string jsonText = File.ReadAllText(filePath);

        UniversityData data = JsonSerializer.Deserialize<UniversityData>(jsonText) ?? new UniversityData();

        Console.WriteLine($"Wczytano prowadzących: {data.Instructors.Count}");
        Console.WriteLine($"Wczytano sal: {data.Rooms.Count}");
        Console.WriteLine($"Wczytano przedmiotów: {data.Courses.Count}");

        Console.WriteLine($"Pierwszy prowadzący to: {data.Instructors[0].Name}, uczy: {data.Instructors[0].Subjects[0]}");

        
       
        
    }
}






