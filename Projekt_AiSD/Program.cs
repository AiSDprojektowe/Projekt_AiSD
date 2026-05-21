using Projekt_AiSD.Models;
using Projekt_AiSD.Modules;
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;
using static System.Runtime.InteropServices.JavaScript.JSType;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("╔══════════════════════════════════════════════╗");
        Console.WriteLine("║   Optymalizator Planu Zajęć - Projekt AiSD   ║");
        Console.WriteLine("╚══════════════════════════════════════════════╝\n");


        string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data.json");

        Console.WriteLine("Krok 1: Walidacja danych...\n");
        if (!DataValidator.ValidateJson(filePath))
        {
            Console.WriteLine("\n❌ Walidacja nie powiodła się! Sprawdź dane w pliku data.json");
            return;
        }

        Console.WriteLine("\nDane zostały zaakceptowane. Wczytywanie...\n");

        string jsonText = File.ReadAllText(filePath);
        UniversityData data = JsonSerializer.Deserialize<UniversityData>(jsonText) ?? new UniversityData();

        Console.WriteLine($"Wczytano prowadzących: {data.Instructors.Count}");
        Console.WriteLine($"Wczytano sal: {data.Rooms.Count}");
        Console.WriteLine($"Wczytano przedmiotów: {data.Courses.Count}");
        Console.WriteLine($"Wczytano grup studenckich: {data.StudentGroups.Count}");

        if (data.Instructors.Count > 0)
        {
            Console.WriteLine($"\nPierwszy prowadzący: {data.Instructors[0].Name}");
            if (data.Instructors[0].Subjects != null && data.Instructors[0].Subjects.Count > 0)
            {
                Console.WriteLine($"Przedmioty: {string.Join(", ", data.Instructors[0].Subjects)}");
            }
        }

        
        Console.WriteLine("\n" + new string('=', 50));
        Console.WriteLine("Krok 2: Test połączenia z modelem Bielik...\n");
        await TestLlmService();

        Console.WriteLine("\nNaciśnij Enter, aby zakończyć...");
        Console.ReadLine();
    }

    static async Task TestLlmService()
    {
        try
        {
            Console.WriteLine("Inicjalizacja LlmService...");
            var llmService = new LlmService();

            Console.WriteLine("Wysyłanie test prompt do modelu Bielik...\n");

            string testText = "Chcę pracować w poniedziałki i czwartki. Chciałbym zaczynać pracę o 9 rano, ale najwcześniej mogę zacząć o 8 rano. Chciałbym kończyć o 15.";

            Console.WriteLine($"Testowy tekst: \"{testText}\"\n");

            var preferences = await llmService.ParsePreferences(testText);

            if (preferences != null)
            {
                Console.WriteLine("  Model Bielik ODPOWIADA! Poprawnie sparsował dane:\n");
                Console.WriteLine($"   • Preferowane dni: {(preferences.PreferredDays?.Count > 0 ? string.Join(", ", preferences.PreferredDays) : "brak")}");
                Console.WriteLine($"   • Preferowane godziny startu: {(preferences.PreferredHoursStart > 0 ? string.Join(", ", preferences.PreferredHoursStart) : "brak")}");
                Console.WriteLine($"   • Preferowane godziny końca: {(preferences.PreferredHoursEnd > 0 ? string.Join(", ", preferences.PreferredHoursEnd) : "brak")}");
                Console.WriteLine($"   • Zakazane sloty: {(preferences.ForbiddenSlots?.Count > 0 ? string.Join(", ", preferences.ForbiddenSlots) : "brak")}");
                Console.WriteLine($"   • Minimalny start: {(preferences.MinStartHour > 0 ? string.Join(", ", preferences.MinStartHour) : "brak")}");
                Console.WriteLine($"   • Maksymalne zakończenie: {(preferences.MaxEndHour > 0 ? string.Join(", ", preferences.MaxEndHour) : "brak")}");
            }
            else
            {
                Console.WriteLine("❌ Model Bielik NIE ODPOWIADA lub zwrócił niepoprawne dane!");
                Console.WriteLine("   Sprawdź:");
                Console.WriteLine("   1. Czy model jest uruchomiony (http://localhost:1234)?");
                Console.WriteLine("   2. Czy zmienną LLM_API_URL ustawiono poprawnie?");
                Console.WriteLine("   3. Czy 'SpeakLeash/bielik-11b-v3.0-instruct:Q4_K_M' jest załadowany?");
            }
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine("❌ BŁĄD POŁĄCZENIA:");
            Console.WriteLine($"   {ex.Message}");
            Console.WriteLine("\n   Upewnij się, że:");
            Console.WriteLine("   1. Model Bielik jest uruchomiony na http://localhost:1234");
            Console.WriteLine("   2. API odpowiada na /v1/chat/completions");
        }
        catch (Exception ex)
        {
            Console.WriteLine("❌ BŁĄD NIEZNANY:");
            Console.WriteLine($"   {ex.Message}");
            Console.WriteLine($"\n   Stack trace: {ex.StackTrace}");
        }
    }
}
     

/*                                             data.json(wczytanie)
                                                        ↓
                                                    Program.cs(deserializacja)
                                                        ↓
                                                    DataValidator.ValidateJson()(sprawdzenie podstawowe)
                                                        ↓
                                                    LlmService(analiza preferences_text)
                                                        ↓
                                                    OptimizationEngine(generowanie planu)


[ DANE WEJŚCIOWE ]
                                 |
                                 v
                     +-----------------------+
                     | Plik: data.json       |
                     | (Prowadzący, Sale,    |
                     |  Przedmioty)          |
                     +-----------------------+
                                 |
                                 v
======================================================================
  MODUŁ 1: PARSER I WALIDATOR (Odpowiedzialność: Inżynier Danych)
======================================================================
                                 |
        +-------------------------------------------------+
        | 1. Wczytanie JSON (Deserializacja)              |
        | 2. Walidacja typów i zakresów wartości          |
        | 3. Utworzenie wewnętrznych obiektów C#          |
        +-------------------------------------------------+
                                 |
                                 v
======================================================================
  MODUŁ 3: ANALIZA LLM (Odpowiedzialność: Inżynier Danych/LLM)
======================================================================
                                 |
        +-------------------------------------------------+
        | 1. Pobranie tekstu "preferences_text"           |
        | 2. Wysłanie promptu do modelu Bielik 11b        | (możliwe że porcjami)
        | 3. Odebranie ustrukturyzowanych danych JSON     |
        | 4. Przypisanie "twardych" preferencji do klas   |
        +-------------------------------------------------+
                                 |
           (Dane są teraz kompletne i sformalizowane)
                                 v
======================================================================
  MODUŁ 2: SILNIK OPTYMALIZACJI (Odpowiedzialność: Inż. Algorytmów)
======================================================================
                                 |
        +-------------------------------------------------+
        | ETAP A: Algorytm Konstruktywny (Baza)           |
        | -> Szuka planu bez błędów. Bezwzględnie spełnia |
        |    Ograniczenia Twarde (HC-1 do HC-8):          |
        |    * Brak kolizji sal i prowadzących            |
        |    * Zgodność pojemności i typu sal             |
        +-------------------------------------------------+
                                 |
        +-------------------------------------------------+
        | ETAP B: Algorytm Poprawiający (Heurystyka)      |
        | -> Modyfikuje ułożony plan by podbić wynik.     |
        | -> Maksymalizuje zaspokojenie Ograniczeń        |
        |    Miękkich (SC-1 do SC-6):                     |
        |    * Preferencje z LLM, minimalizacja "okienek" |
        | -> Zapisuje logi wartości funkcji celu          |
        +-------------------------------------------------+
                                 |
                          (Gotowy Plan)
                                 v
======================================================================
  MODUŁ 4: WIZUALIZACJA (Odpowiedzialność: Inżynier Wizualizacji)
======================================================================
                                 |
        +-------------------------------------------------+
        | Generowanie statycznych plików (np. HTML/PNG):  |
        | 1. Siatka planu zajęć (Dni x Godziny)           |
        | 2. Wykres zbieżności funkcji celu               |
        | 3. Raport statystyczny (mapa ciepła sal)        |
        +-------------------------------------------------+
                                 |
                                 v
                       [ WYNIKI KOŃCOWE ]
                                 |
    +----------------------------+----------------------------+
    |                            |                            |
    v                            v                            v
[ wynik.json ]             [ plan.html ]              [ wykres.png ]

*/