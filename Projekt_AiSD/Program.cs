using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Collections.Generic;
using Projekt_AiSD.Models;
using Projekt_AiSD.Modules;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("Rozpoczynam wczytywanie danych...");

        string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data.json");
        string jsonText = File.ReadAllText(filePath);

        UniversityData data = JsonSerializer.Deserialize<UniversityData>(jsonText) ?? new UniversityData();

        Console.WriteLine($"Wczytano prowadzących: {data.Instructors.Count}");
        Console.WriteLine($"Wczytano sal: {data.Rooms.Count}");
        Console.WriteLine($"Wczytano przedmiotów: {data.Courses.Count}");

        Console.WriteLine($"Pierwszy prowadzący to: {data.Instructors[0].Name}, uczy: {data.Instructors[0].Subjects[0]}");

        
       
        
    }
}



/*[ DANE WEJŚCIOWE ]
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

Na następny tydzień : 
trzeba przetestować bielika @Julia Szczecina  ,  zrobić działające okno gdzie da się wrzucić plik tak żeby wylądował w tym samym folderze co projekt i żeby dało się z niego korzystać @Karolina Wołoszyn , zrobić parser danych i go przetestować z walidatorem z tymi danymi co przesyłałem i co są na Teams , zacząć myśleć nad algorytmami  : najpierw spełnianie ograniczeń twardych potem miękkich  -> @Marysia Głuch @Weronika Suwała , 

możecie jak chcecie na razie robić to lokalnie u siebie jak wam pasuje, ja spróbuję jak najszybciej ogarnąć tego gita
*/