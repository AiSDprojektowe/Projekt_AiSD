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
    static void Main(string[] args)
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