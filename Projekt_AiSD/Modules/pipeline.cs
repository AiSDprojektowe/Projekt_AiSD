using System;
using System.IO;
using System.Linq; 
using System.Text.Json;
using System.Threading.Tasks;
using Projekt_AiSD.Models;

namespace Projekt_AiSD.Modules
{
    public class DataPipeline
    {
       
        private const string CacheFileName = "cached_data.json";

        public async Task<UniversityData> PrepareDataAsync(string jsonFilePath)
        {
            
            if (File.Exists(CacheFileName))
            {
                Console.WriteLine("[CACHE] Znaleziono gotowe dane! Pomijam odpytywanie modelu Bielik...");
                string cachedJson = await File.ReadAllTextAsync(CacheFileName);
                var cachedData = JsonSerializer.Deserialize<UniversityData>(cachedJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return cachedData;
            }

           
            Console.WriteLine("[API] Brak cache. Wczytywanie surowych danych z pliku JSON...");
            string rawJson = await File.ReadAllTextAsync(jsonFilePath);
            var universityData = JsonSerializer.Deserialize<UniversityData>(rawJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (universityData == null || universityData.Instructors == null)
            {
                throw new Exception("Błąd wczytywania danych. Plik wejściowy jest pusty.");
            }

            
            Console.WriteLine("[API] Uruchamianie modułu LLM Bielik w trybie PACZKOWYM (Batching)...");
            LlmService llm = new LlmService();

            var instructorsWithPrefs = universityData.Instructors
                .Where(i => !string.IsNullOrWhiteSpace(i.PreferencesText))
                .ToArray();

            var chunks = instructorsWithPrefs.Chunk(5);
            int currentChunk = 1;
            int totalChunks = chunks.Count();

            foreach (var batch in chunks)
            {
                Console.WriteLine($"-> Wysyłam paczkę {currentChunk} z {totalChunks} (Prowadzący: {string.Join(", ", batch.Select(b => b.Id))})...");

                var batchResults = await llm.ParsePreferencesBatch(batch);

                if (batchResults != null)
                {
                    foreach (var instructor in batch)
                    {
                        if (batchResults.TryGetValue(instructor.Id, out var parsedPrefs))
                        {
                            instructor.ParsedPreferences = parsedPrefs;
                        }
                    }
                }
                else
                {
                    Console.WriteLine($"[UWAGA] Paczka {currentChunk} zwróciła błąd lub pusty wynik!");
                }

                currentChunk++;
                await Task.Delay(1000);
            }

            Console.WriteLine("[CACHE] Zapisywanie przetworzonych danych na dysk...");
            string jsonToSave = JsonSerializer.Serialize(universityData, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            await File.WriteAllTextAsync(CacheFileName, jsonToSave);

            Console.WriteLine("Dane są kompletne i gotowe do optymalizacji!");
            return universityData;
        }
    }
}
