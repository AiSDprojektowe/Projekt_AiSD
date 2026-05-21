using System;
using System.IO;
using System.Threading.Tasks;
using System.Text.Json;
using Projekt_AiSD.Models;
using Projekt_AiSD.Modules;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("Rozpoczynam wczytywanie danych...");

        string filePath = "data.json";

        string jsonText = File.ReadAllText(filePath);

        UniversityData data =
            JsonSerializer.Deserialize<UniversityData>(jsonText)
            ?? new UniversityData();

        Console.WriteLine($"Wczytano prowadzących: {data.Instructors.Count}");
        Console.WriteLine($"Wczytano sal: {data.Rooms.Count}");
        Console.WriteLine($"Wczytano przedmiotów: {data.Courses.Count}");

        Console.WriteLine(
            $"Pierwszy prowadzący: {data.Instructors[0].Name}");

        Console.WriteLine("\n=== TEST BIELIKA ===");

        LlmService llm = new LlmService();

        string tekst =
            "Nie prowadzę zajęć w piątki i wolę zajęcia rano.";

        try
        {
            var response = await llm.ParsePreferences(tekst);

            Console.WriteLine("Preferencje sparsowane poprawnie!");

            Console.WriteLine("\nODPOWIEDŹ BIELIKA:");

            Console.WriteLine(
                JsonSerializer.Serialize(
                    response,
                    new JsonSerializerOptions
                    {
                        WriteIndented = true
                    }));
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine("Błąd połączenia z LLM:");
            Console.WriteLine(ex.Message);

            return;
        }

        Console.ReadLine();
    }
}