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


        public async Task<Dictionary<string, Preferences>?> ParsePreferencesBatch(Instructor[] instructorsBatch)
        {
            var systemPrompt =
                """
                Jesteś wysoce precyzyjnym parserem preferencji prowadzących uczelni. 
                Analizujesz tekst i zwracasz WYŁĄCZNIE czysty JSON.
                
                ZASADY PARSOWANIA:
                1. Dni tygodnia to ściśle: "monday", "tuesday", "wednesday", "thursday", "friday".
                2. Godziny to liczby całkowite od 8 do 20.
                3. "MOGĘ" -> określa preferred_days, preferred_hours_start, preferred_hours_end, min_start_hour, max_end_hour.
                4. "NIE MOGĘ" (lub inne zakazy godzinowe) -> MUSZĄ zostać przekonwertowane na forbidden_slots. 
                   Pole forbidden_slots to obiekt (słownik), gdzie kluczem jest nazwa dnia w j. angielskim, a wartością lista WSZYSTKICH zakazanych godzin. 
                   Przykład: "nie mogę w piątek po 14" -> "friday": [14, 15, 16, 17, 18, 19, 20]. "nie mogę w środę rano do 10" -> "wednesday": [8, 9]. "nie mogę w poniedziałki" -> "monday": [8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20].
                5. Zwracasz obiekt JSON, gdzie KLUCZEM jest ID prowadzącego.
                """;

            var promptBuilder = new StringBuilder();
            promptBuilder.AppendLine("Przeanalizuj poniższe teksty i zwróć ich preferencje w wymaganym formacie JSON:");

            foreach (var instructor in instructorsBatch)
            {
                promptBuilder.AppendLine($"ID: {instructor.Id}");
                promptBuilder.AppendLine($"Tekst: \"{instructor.PreferencesText}\"");
                promptBuilder.AppendLine("---");
            }

            var userPrompt = promptBuilder.ToString() +
                "\nOczekiwany format JSON (ZWRÓĆ TYLKO TO, BEZ KOMENTARZY):\n" +
                "{\n" +
                "  \"ID_PROWADZACEGO\": {\n" +
                "    \"preferred_days\": [\"monday\", \"tuesday\"],\n" +
                "    \"preferred_hours_start\": 8,\n" +
                "    \"preferred_hours_end\": 16,\n" +
                "    \"forbidden_slots\": {\n" +
                "      \"friday\": [8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20],\n" +
                "      \"wednesday\": [8, 9]\n" +
                "    },\n" +
                "    \"min_start_hour\": 8,\n" +
                "    \"max_end_hour\": 20\n" +
                "  }\n" +
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