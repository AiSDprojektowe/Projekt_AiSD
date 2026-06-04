using Projekt_AiSD.Models;
using System;
using System.Collections.Generic;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Projekt_AiSD.Modules
{
    public class LlmService
    {
        private readonly HttpClient _httpClient;


        private const string ApiUrl = "http://149.156.194.192:8088/v1/chat/completions";


        private const string Token = "bsk-ee761fa7d676f98fa7f2caed36cb41bbd4b2c88bdb31e36afac829415dfb0ec2";

        public LlmService()
        {
            _httpClient = new HttpClient();


            if (!string.IsNullOrWhiteSpace(Token))
            {
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token);
            }
        }

        // NOWA METODA: Przyjmuje paczkę prowadzących i zwraca Słownik <ID_Prowadzącego, Preferencje>
        public async Task<Dictionary<string, Preferences>?> ParsePreferencesBatch(Instructor[] instructorsBatch)
        {
            var systemPrompt =
                """
                Jesteś parserem preferencji prowadzących.
                Zwracaj WYŁĄCZNIE poprawny JSON bez markdown formatting.
                Wymagany format to obiekt, gdzie KLUCZEM jest ID prowadzącego, a WARTOŚCIĄ jego zdekodowane preferencje.
                Dni: "monday", "tuesday", "wednesday", "thursday", "friday". Godziny to liczby od 8 do 18.
                """;

            // Budujemy dynamiczny tekst zapytania z ID każdego prowadzącego z paczki
            var promptBuilder = new StringBuilder();
            promptBuilder.AppendLine("Przeanalizuj poniższe teksty i zwróć ich preferencje w formacie JSON:");

            foreach (var instructor in instructorsBatch)
            {
                promptBuilder.AppendLine($"ID: {instructor.Id}");
                promptBuilder.AppendLine($"Tekst: \"{instructor.PreferencesText}\"");
                promptBuilder.AppendLine("---");
            }

            var userPrompt = promptBuilder.ToString() +
                "\nOczekiwany format JSON:\n" +
                "{\n" +
                "  \"I01\": { \"preferred_days\": [\"monday\"], \"preferred_hours_start\": 8, \"preferred_hours_end\": 18, \"forbidden_slots\": [], \"min_start_hour\": 8, \"max_end_hour\": 18 },\n" +
                "  \"I02\": { ... }\n" +
                "}";

            var body = new
            {
                model = "SpeakLeash/bielik-11b-v3.0-instruct:Q4_K_M",
                messages = new object[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userPrompt }
                },
                temperature = 0
            };

            var json = JsonSerializer.Serialize(body);
            var response = await _httpClient.PostAsync(
                ApiUrl,
                new StringContent(json, Encoding.UTF8, "application/json"));

            response.EnsureSuccessStatusCode();

            var responseText = await response.Content.ReadAsStringAsync();
            return ParseBatchResponse(responseText);
        }

        // Dekoder dla nowej, słownikowej struktury
        private Dictionary<string, Preferences>? ParseBatchResponse(string response)
        {
            try
            {
                using var doc = JsonDocument.Parse(response);
                var content = doc.RootElement
                        .GetProperty("choices")[0]
                        .GetProperty("message")
                        .GetProperty("content")
                        .GetString();

                if (string.IsNullOrWhiteSpace(content)) return null;

                content = content.Replace("```json", "").Replace("```", "").Trim();

                return JsonSerializer.Deserialize<Dictionary<string, Preferences>>(content,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LLM ERROR] Błąd parsowania paczki JSON: {ex.Message}");
                return null;
            }
        }
    }

}