using System.Text.Json;
using Projekt_AiSD.Models;

namespace Projekt_AiSD.Modules;

// Klasa odpowiedzialna za wczytywanie danych z pliku JSON
public static class DataParser
{
    // Funkcja wczytuje plik data.json i zamienia go na obiekt UniversityData
    public static UniversityData LoadFromJson(string filePath)
    {
        // Sprawdza, czy plik istnieje
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Nie znaleziono pliku: {filePath}");
        }

        // Wczytuje całą zawartość pliku JSON jako tekst
        string json = File.ReadAllText(filePath);

        // Ustawienia pozwalają ignorować wielkość liter w nazwach pól
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        // Zamienia tekst JSON na obiekt UniversityData
        UniversityData? data = JsonSerializer.Deserialize<UniversityData>(json, options);

        // Sprawdza, czy parser poprawnie odczytał dane
        if (data == null)
        {
            throw new Exception("Nie udało się sparsować pliku JSON.");
        }

        // Zwraca gotowe dane do dalszego użycia w programie
        return data;
    }

    // Funkcja sprawdza, czy podstawowe listy w danych istnieją i nie są puste
    public static bool ValidateBasicData(UniversityData data)
    {
        if (data.Instructors == null || data.Instructors.Count == 0)
            return false;

        if (data.Rooms == null || data.Rooms.Count == 0)
            return false;

        if (data.Courses == null || data.Courses.Count == 0)
            return false;

        return true;
    }
}