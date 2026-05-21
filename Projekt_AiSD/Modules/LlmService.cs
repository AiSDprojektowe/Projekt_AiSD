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
using System.Net.Http.Headers;
using Projekt_AiSD.Models;
/*
 System - podstawowe klasy np. console, exception, environment
 System.Collections.Generic - pozwala robic listy i słowniki
 System.Text - udostępnia operacje na tekście i kodowaniach
 System.Text.Json - pozwala pracować z JSON-em 
 System.Net.Http.Headers - pozwala ustalać nagłówki HTTP
 Projekt_AiSD.Models - importuje własne modele projektu
*/
//namespace organizuje kod w "folder logiczny"
namespace Projekt_AiSD.Modules
{
    //tworzę klasę odpowiedzialną za komunikację z LLM-em, dostępną tylko wewnątrz projektu
    internal class LlmService
    {
        // prywatne pole klasy przechowujące klienta HTTP do wysyłania requestów API
        private readonly HttpClient _httpClient;

        public LlmService()
        {
            //tworzymy klienta
            _httpClient = new HttpClient();
            
            var apiUrl = Environment.GetEnvironmentVariable("LLM_API_URL") ?? "http://localhost:1234/v1/";
            
            var token = Environment.GetEnvironmentVariable("LLM_API_TOKEN");
            
            _httpClient.BaseAddress = new Uri(apiUrl);

            if (!string.IsNullOrWhiteSpace(token))
            {
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token);
            }
        }
        //tworzymy metodę wysyłającą tekst do modelu, która prosi model o JSON i zamienia odpowiedź na Preferences
        public async Task<Preferences?> ParsePreferences(string text)
        {
            //instrukcja dla modelu, która mówi kim ma być i jak odpowiadać
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
                //wybór modelu
                model = "SpeakLeash/bielik-11b-v3.0-instruct:Q4_K_M",
                //lista wiadomości dla chat API
                messages = new object[]
                {
                    //instrukcja systemowa
                    new
                    {
                        role = "system",
                        content = systemPrompt
                    },
                    //wiadomość użytkownika
                    new
                    {
                        role = "user",
                        content = userPrompt
                    }
                },
                /*
                ustawia model na maksymalnie przewidywalny:
                0-->stabilne odpowiedzi
                1+-->kreatywne
                 */
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
            //próbuje wykonać kod
            try
            {
                //parsuje JSON odpowiedzi API
                using var doc = JsonDocument.Parse(response);
                /*
                idzie po strukturze JSON:
                -bierze pierwszy element choices
                -wchodzi do message
                -pobiera content
                -konwertuje na string
                 */
                var content = doc.RootElement
                        .GetProperty("choices")[0]
                        .GetProperty("message")
                        .GetProperty("content")
                        .GetString();
                
                //sprawdzanie pustej odpowiedzi
                if (string.IsNullOrWhiteSpace(content))
                    return null;

                content = content
                    .Replace("```json", "")
                    .Replace("```", "")
                    .Trim();
                return JsonSerializer.Deserialize<Preferences>(content,
                    new JsonSerializerOptions
                    {
                        //ignoruje wielkość liter
                        PropertyNameCaseInsensitive = true
                    });
            }
            //jeśli coś się wywali to łapie wątek, program się nie crashuje
            catch (Exception ex)
            {
                //wypisuje błąd do konsoli
                Console.WriteLine(ex);
                return null;
            }
        }
    }
}