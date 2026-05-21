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

        string filePath = "data.json";
        string jsonText = File.ReadAllText(filePath);

        UniversityData data = JsonSerializer.Deserialize<UniversityData>(jsonText) ?? new UniversityData();
        // Sprawdzenie poprawności danych
        if (ValidateBasicData(data))
        {
            Console.WriteLine("Dane zostały poprawnie wczytane.");

            Console.WriteLine($"Wczytano prowadzących: {data.Instructors.Count}");
            Console.WriteLine($"Wczytano sal: {data.Rooms.Count}");
            Console.WriteLine($"Wczytano przedmiotów: {data.Courses.Count}");

            Console.WriteLine(
                $"Pierwszy prowadzący: {data.Instructors[0].Name}, " +
                $"uczy: {data.Instructors[0].Subjects[0]}"
            );
        }
        else
        {
            Console.WriteLine("Dane są niepoprawne.");
        }

        Console.ReadLine();
    }


    // Funkcja sprawdzająca czy dane zostały poprawnie odczytane
    static bool ValidateBasicData(UniversityData data)
    {
        // Czy istnieją prowadzący
        if (data.Instructors == null || data.Instructors.Count == 0)
            return false;

        // Czy istnieją sale
        if (data.Rooms == null || data.Rooms.Count == 0)
            return false;

        // Czy istnieją kursy
        if (data.Courses == null || data.Courses.Count == 0)
            return false;

        return true;
    }
}






