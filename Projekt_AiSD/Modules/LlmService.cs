using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Net.Http.Headers;
using Projekt_AiSD.Models;
                                                    //149.156.194.192   API bielika
namespace Projekt_AiSD.Modules
{
    internal class LlmService
    {
        private readonly HttpClient _httpClient;

        public LlmService()
        {
            _httpClient = new HttpClient();
            
            var apiUrl = Environment.GetEnvironmentVariable("LLM_API_URL") ?? "http://localhost:1234/v1/";
            
            var token = Environment.GetEnvironmentVariable("LLM_API_TOKEN");
            
            _httpClient.BaseAddress = new Uri(apiUrl);

            if (!string.IsNullOrWhiteSpace(token))
            {
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
        }

        public async Task<Preferences?> ParsePreferences(string text)
        {
            var systemPrompt =
                """
                Jesteś parserem preferencji prowadzących.
                Zwracaj WYŁĄCZNIE poprawny JSON.
                """;

            var userPrompt =
                $"Tekst:\n" +
                $"\"{text}\"\n\n" +
                "Zwróć JSON:\n" +
                "{\n" +
                " \"preferred_days\": [], \n" +
                " \"preferred_hours_start\": null, \n" +
                " \"preferred_hours_end\": null, \n" +
                " \"forbidden_slots\": [], \n" +
                " \"min_start_hour\": null\n" +
                "}";

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
                "chat/completions",
                new StringContent(json, Encoding.UTF8, "application/json"));
            response.EnsureSuccessStatusCode();
            
            var responseText = await response.Content.ReadAsStringAsync();
            
            return ParsePreferencesResponse(responseText);
        }

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
                    .Replace("'''json", "")
                    .Replace("'''", "")
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
