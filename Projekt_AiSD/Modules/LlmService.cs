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

        public LlmService()
        {
            _httpClient = new HttpClient();
            
            
            if (!string.IsNullOrWhiteSpace(Token))
            {
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token);
            }
        }
        //tworzymy metodę wysyłającą tekst do modelu, która prosi model o JSON i zamienia odpowiedź na Preferences
        public async Task<Preferences?> ParsePreferences(string text)
        {
            var systemPrompt =
                """
                Jesteś parserem preferencji prowadzących.
                Zwracaj WYŁĄCZNIE poprawny JSON bez markdown formatting.
                Dni: "monday", "tuesday", "wednesday", "thursday", "friday"
                Godziny: liczby od 8 do 18
                """;
            //wstawia tekst użytkownika i mówi modelowi co zrobić
            var userPrompt =
                $"Tekst:\n" +
                $"\"{text}\"\n\n" +
                "Zwróć JSON:\n" +
                "{\n" +
                " \"preferred_days\": [\"monday\"], \n" +
                " \"preferred_hours_start\": [8], \n" +
                " \"preferred_hours_end\": [18], \n" +
                " \"forbidden_slots\": [], \n" +
                " \"min_start_hour\": [8],\n" +
                " \"max_end_hour\": [18]\n" +
                "}";

    "Tekst:\n" +
    "\"Nie prowadzę w piątki\"\n\n" +

    "JSON:\n" +

    "{\n" +
    "  \"preferred_days\": [],\n" +
    "  \"preferred_hours_start\": null,\n" +
    "  \"preferred_hours_end\": null,\n" +
    "  \"forbidden_slots\": [\n" +
    "    {\n" +
    "      \"day\": \"Fri\",\n" +
    "      \"hours\": [8,9,10,11,12,13,14,15,16]\n" +
    "    }\n" +
    "  ],\n" +
    "  \"min_start_hour\": null\n" +
    "}\n\n" +

    "Przykład 2:\n\n" +

    "Tekst:\n" +
    "\"Lubię zajęcia rano\"\n\n" +

    "JSON:\n" +

    "{\n" +
    "  \"preferred_days\": [],\n" +
    "  \"preferred_hours_start\": 8,\n" +
    "  \"preferred_hours_end\": 12,\n" +
    "  \"forbidden_slots\": [],\n" +
    "  \"min_start_hour\": null\n" +
    "}\n\n" +

    "Zwróć WYŁĄCZNIE JSON bez komentarzy i bez markdown.";
            
          
            
            //tworzy anonimowy obiekt do wysyłania jako JSON
            var body = new
            {
                model = "SpeakLeash/bielik-11b-v3.0-instruct:Q4_K_M",
                messages = new object[]
                {
                    new
                    {
                        role = "system",
                        content = systemPrompt
                    },
                    new
                    {
                        role = "user",
                        content = userPrompt
                    }
                },
                temperature = 0
            };
            
            var json = JsonSerializer.Serialize(body);
            var response = await _httpClient.PostAsync(
                ApiUrl,
                new StringContent(json, Encoding.UTF8, "application/json"));
            response.EnsureSuccessStatusCode();
            
            var responseText = await response.Content.ReadAsStringAsync();
            
            return ParsePreferencesResponse(responseText);
        }
        //przetwarza odpowiedź JSON API
        private Preferences? ParsePreferencesResponse(string response)
        {
            try
            {
                using var doc = JsonDocument.Parse(response);
                var content = doc.RootElement
                        .GetProperty("choices")[0]
                        .GetProperty("message")
                        .GetProperty("content")
                        .GetString();
                
                if (string.IsNullOrWhiteSpace(content))
                    return null;

                content = content
                    .Replace("```json", "")
                    .Replace("```", "")
                    .Trim();
                return JsonSerializer.Deserialize<Preferences>(content,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return null;
            }
        }
    }
}