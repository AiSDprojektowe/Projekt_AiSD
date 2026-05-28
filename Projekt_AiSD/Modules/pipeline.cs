using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Projekt_AiSD.Models;

namespace Projekt_AiSD.Modules
{
    public class DataPipeline
    {
        public async Task<UniversityData> PrepareDataAsync(string jsonFilePath)
        {
            Console.WriteLine("1. Wczytywanie surowych danych z pliku JSON...");

            
            string rawJson = await File.ReadAllTextAsync(jsonFilePath);
            var universityData = JsonSerializer.Deserialize<UniversityData>(rawJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (universityData == null || universityData.Instructors == null)
            {
                throw new Exception("Błąd wczytywania danych. Plik jest pusty lub ma złą strukturę.");
            }

            Console.WriteLine("2. Uruchamianie modułu LLM Bielik...");
            LlmService llm = new LlmService();

            
            foreach (var instructor in universityData.Instructors)
            {
                if (!string.IsNullOrWhiteSpace(instructor.PreferencesText))
                {
                    Console.WriteLine($"-> Przetwarzanie preferencji: {instructor.Name}");

                    
                    var preferences = await llm.ParsePreferences(instructor.PreferencesText);

                    
                    instructor.ParsedPreferences = preferences;

                    
                    await Task.Delay(500);
                }
            }

            Console.WriteLine("3. Dane są kompletne i gotowe do optymalizacji!");

            return universityData;
        }
    }
}