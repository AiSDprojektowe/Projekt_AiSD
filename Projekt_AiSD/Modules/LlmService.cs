using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
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
        //konstruktor
        public LlmService()
        {
            //tworzymy klienta
            _httpClient = new HttpClient();
            //próbuje pobrać zmienną środowiskową LLM_API_URL, jeśli nie istnieje ustawia domyślny adres
            var apiUrl = Environment.GetEnvironmentVariable("LLM_API_URL") ?? "http://localhost:1234/v1/";
            //pobiera tokena API z systemu
            var token = Environment.GetEnvironmentVariable("LLM_API_TOKEN");
            //ustawia bazowy URL dla requestów
            _httpClient.BaseAddress = new Uri(apiUrl);
            //sprawdza czy istnieje token, czy nie jest pusty, czy nie zawiera samych spacji
            if (!string.IsNullOrWhiteSpace(token))
            {
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
        }
        //tworzymy metodę wysyłającą tekst do modelu, która prosi model o JSON i zamienia odpowiedź na Preferences
        public async Task<Preferences?> ParsePreferences(string text)
        {
            //instrukcja dla modelu, która mówi kim ma być i jak odpowiadać
            var systemPrompt =
                """
                Jesteś parserem preferencji prowadzących.
                Zwracaj WYŁĄCZNIE poprawny JSON.
                """;
            //wstawia tekst użytkownika i mówi modelowi co zrobić
            var userPrompt =
    "Tekst preferencji prowadzącego:\n" +
    "\"" + text + "\"\n\n" +

    "Przykładowe dane systemu planowania:\n\n" +

    "{\n" +
    "  \"availability\": {\n" +
    "    \"Mon\": [8,9,10,11],\n" +
    "    \"Tue\": [8,9,10,11,12],\n" +
    "    \"Wed\": [8,9,10],\n" +
    "    \"Thu\": [10,11,12],\n" +
    "    \"Fri\": [8,9,10]\n" +
    "  }\n" +
    "}\n\n" +

    "Twoim zadaniem jest wyciągnięcie preferencji prowadzącego " +
    "i zwrócenie WYŁĄCZNIE poprawnego JSON-a.\n\n" +

    "Dozwolone dni:\n" +
    "Mon, Tue, Wed, Thu, Fri\n\n" +

    "Format odpowiedzi:\n\n" +

    "{\n" +
    "  \"preferred_days\": [],\n" +
    "  \"preferred_hours_start\": null,\n" +
    "  \"preferred_hours_end\": null,\n" +
    "  \"forbidden_slots\": [],\n" +
    "  \"min_start_hour\": null\n" +
    "}\n\n" +

    "Zasady:\n" +
    "- preferred_days = lista preferowanych dni\n" +
    "- preferred_hours_start = preferowana godzina rozpoczęcia\n" +
    "- preferred_hours_end = preferowana godzina zakończenia\n" +
    "- forbidden_slots = zakazane terminy\n" +
    "- min_start_hour = najwcześniejsza akceptowalna godzina rozpoczęcia\n" +
    "- godziny zapisuj jako liczby całkowite\n" +
    "- jeśli brak informacji użyj null albo []\n" +
    "- forbidden_slots musi mieć format:\n\n" +

    "[\n" +
    "  {\n" +
    "    \"day\": \"Fri\",\n" +
    "    \"hours\": [8,9,10,11]\n" +
    "  }\n" +
    "]\n\n" +

    "Przykład 1:\n\n" +

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
            //zmienia obiekt C# na JSON
            var json = JsonSerializer.Serialize(body);
            //wysyła request HTTP POST, treść requwstu: JSON, UTF-8, typ application/json
            var response = await _httpClient.PostAsync(
                "chat/completions",
                new StringContent(json, Encoding.UTF8, "application/json"));
            //jeśli API zwróci błąd --> rzuca wątek
            response.EnsureSuccessStatusCode();
            //pobiera odpowiedź jako string
            var responseText = await response.Content.ReadAsStringAsync();
            //parsuje odpowiedź do modelu do obiektu Preferences
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
                
                //czyszczenie odpowiedzi
                content = content
                    .Replace("'''json", "")
                    .Replace("'''", "")
                    .Trim();
                //z JSON-a tworzy obiekt Preferences
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
