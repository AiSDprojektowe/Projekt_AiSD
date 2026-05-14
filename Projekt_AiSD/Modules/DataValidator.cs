using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Projekt_AiSD.Models; 

namespace Projekt_AiSD.Modules
{
    public static class DataValidator
    {
        public static bool ValidateJson(string filePath)
        {
            Console.WriteLine($"--- Sprawdzanie pliku: {filePath} ---");

            if (!File.Exists(filePath))
            {
                Console.WriteLine("BŁĄD: Plik nie istnieje!");
                return false;
            }

            try
            {
                string json = File.ReadAllText(filePath);
                var data = JsonSerializer.Deserialize<UniversityData>(json);

                if (data == null) return false;

                bool isOk = true;

                // Sprawdzanie Instruktorów
                foreach (var inst in data.Instructors)
                {
                    if (inst.MaxHoursPerWeek < 0)
                    {
                        Console.WriteLine($"[!] Błąd danych: {inst.Name} ma ujemne godziny!");
                        isOk = false;
                    }
                }

                // Sprawdzanie Sal (Rooms)
                foreach (var room in data.Rooms)
                {
                    if (string.IsNullOrEmpty(room.Id))
                    {
                        Console.WriteLine("[!] Błąd danych: Jedna z sal nie ma nazwy/ID!");
                        isOk = false;
                    }
                }

                return isOk;
            }
            catch (JsonException ex)
            {
                Console.WriteLine("BŁĄD SKŁADNI JSON: " + ex.Message);
                return false;
            }
        }
    }
}