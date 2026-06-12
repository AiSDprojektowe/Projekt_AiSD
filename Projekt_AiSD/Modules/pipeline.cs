using System;
using System.IO;
using System.Linq; // WAŻNE: To ta biblioteka pozwala na dzielenie na paczki (.Chunk)
using System.Text.Json;
using System.Threading.Tasks;
using Projekt_AiSD.Models;

namespace Projekt_AiSD.Modules
{
    public class DataPipeline
    {
        // Nazwa pliku do przechowywania zamrożonych danych
        private const string CacheFileName = "cached_data.json";

        public async Task<UniversityData> PrepareDataAsync(string jsonFilePath)
        {
            // 1. MECHANIZM CACHE
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

            // 2. WCZYTYWANIE DANYCH JSON
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

            // 3. TRYB PACZKOWY (BATCHING)
            Console.WriteLine("[API] Uruchamianie modułu LLM Bielik w trybie PACZKOWYM (Batching)...");
            LlmService llm = new LlmService();

            // Filtrujemy i dzielimy na paczki po 5 osób
            var instructorsWithPrefs = universityData.Instructors
                .Where(i => !string.IsNullOrWhiteSpace(i.PreferencesText))
                .ToArray();

            var chunks = instructorsWithPrefs.Chunk(5);
            int currentChunk = 1;
            int totalChunks = chunks.Count();

            foreach (var batch in chunks)
            {
                Console.WriteLine($"-> Wysyłam paczkę {currentChunk} z {totalChunks} (Prowadzący: {string.Join(", ", batch.Select(b => b.Id))})...");

                // TUTAJ ZMIANA: Używamy nowej metody Batch
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
                await Task.Delay(1000); // Oddech dla serwera
            }

            // 4. ZAPIS DO CACHE
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