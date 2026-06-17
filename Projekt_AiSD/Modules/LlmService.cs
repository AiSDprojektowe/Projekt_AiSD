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
                
                KRYTYCZNE ZASADY PARSOWANIA:
                1. Dni tygodnia to ściśle: "monday", "tuesday", "wednesday", "thursday", "friday".
                2. Godziny to liczby całkowite od 8 do 20.
                3. ZAKAZY: Wszelkie frazy typu "NIE MOGĘ", "brak dostępności" MUSZĄ trafić do klucza o nazwie dokładnie "ForbiddenSlots" (przez duże F, bez podkreślników) jako lista godzin.
                4. LOGIKA CZASU: Jeśli ktoś pisze "tylko od 12 do 18" lub "brak dostępności przed 12", oznacza to, że godziny 8, 9, 10, 11 SĄ ZAKAZANE (forbidden_slots) we wszystkie dni.
                5. DOSTĘPNOŚĆ AWARYJNA: Traktuj ją jako normalny czas dostępny. NIE DODAWAJ tych godzin do forbidden_slots!
                6. Zwracasz wyłącznie obiekt JSON, gdzie KLUCZEM jest ID prowadzącego. Żadnego tekstu poza JSONem.
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
            var result = new Dictionary<string, Preferences>();
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

                using var contentDoc = JsonDocument.Parse(content);

                foreach (var instructorProp in contentDoc.RootElement.EnumerateObject())
                {
                    string instructorId = instructorProp.Name;
                    var pref = new Preferences();

                    try
                    {
                        if (instructorProp.Value.TryGetProperty("forbidden_slots", out var fs))
                        {
                            if (fs.ValueKind == JsonValueKind.Object)
                            {
                                foreach (var dayProp in fs.EnumerateObject())
                                {
                                    var hours = new List<int>();
                                    if (dayProp.Value.ValueKind == JsonValueKind.Array)
                                    {
                                        foreach (var h in dayProp.Value.EnumerateArray())
                                        {
                                            if (h.ValueKind == JsonValueKind.Number && h.TryGetInt32(out int hour))
                                            {
                                                hours.Add(hour);
                                            }
                                        }
                                    }
                                    pref.ForbiddenSlots[dayProp.Name] = hours;
                                }
                            }
                        }

                        if (instructorProp.Value.TryGetProperty("preferred_days", out var pd) && pd.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var day in pd.EnumerateArray())
                            {
                                if (day.ValueKind == JsonValueKind.String) pref.PreferredDays.Add(day.GetString());
                            }
                        }

                        if (instructorProp.Value.TryGetProperty("preferred_hours_start", out var phs) && phs.ValueKind == JsonValueKind.Number)
                            pref.PreferredHoursStart = phs.GetInt32();

                        if (instructorProp.Value.TryGetProperty("preferred_hours_end", out var phe) && phe.ValueKind == JsonValueKind.Number)
                            pref.PreferredHoursEnd = phe.GetInt32();

                        if (instructorProp.Value.TryGetProperty("min_start_hour", out var msh) && msh.ValueKind == JsonValueKind.Number)
                            pref.MinStartHour = msh.GetInt32();

                        if (instructorProp.Value.TryGetProperty("max_end_hour", out var meh) && meh.ValueKind == JsonValueKind.Number)
                            pref.MaxEndHour = meh.GetInt32();

                        result[instructorId] = pref;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[LLM WARNING] Blad przy osobie {instructorId}, ale ratuje reszte paczki! Blad: {ex.Message}");
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LLM ERROR] Krytyczny blad struktury odpowiedzi API: {ex.Message}");
                return result.Count > 0 ? result : null;
            }
        }
    }

}