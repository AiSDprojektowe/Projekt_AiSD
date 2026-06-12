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