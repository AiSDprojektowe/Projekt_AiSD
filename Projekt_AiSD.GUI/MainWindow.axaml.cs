using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage; // WAŻNE: Dodana biblioteka do obsługi Eksploratora plików
using System;
using System.Linq;
using System.Threading.Tasks;
using Projekt_AiSD.Models;
using Projekt_AiSD.Modules;
using Avalonia.Media.Imaging;

namespace Projekt_AiSD.GUI
{
    public partial class MainWindow : Window
    {
        private UniversityData _universityData;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Log(string message)
        {
            var logConsole = this.FindControl<TextBlock>("LogConsole");
            if (logConsole != null)
            {
                logConsole.Text += $"\n[{DateTime.Now:HH:mm:ss}] {message}";
            }
        }

        private async void LoadDataBtn_Click(object sender, RoutedEventArgs e)
        {
            var loadBtn = this.FindControl<Button>("LoadDataBtn");
            if (loadBtn != null) loadBtn.IsEnabled = false;

            try
            {
                // 1. Otwieramy okno wyboru pliku (Eksplorator)
                var topLevel = TopLevel.GetTopLevel(this);
                if (topLevel == null) throw new Exception("Nie można uzyskać dostępu do okna aplikacji.");

                var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                {
                    Title = "Wybierz plik z danymi uczelni",
                    AllowMultiple = false, // Wybieramy tylko jeden plik
                    FileTypeFilter = new[]
                    {
                        new FilePickerFileType("Pliki JSON") { Patterns = new[] { "*.json" } }
                    }
                });

                // Jeśli użytkownik kliknął "Anuluj" i zamknął okno
                if (files == null || !files.Any())
                {
                    Log("Anulowano wybór pliku.");
                    return;
                }

                // 2. Pobieramy pełną ścieżkę do wybranego pliku
                var selectedFile = files[0];
                string filePath = selectedFile.TryGetLocalPath();

                if (string.IsNullOrEmpty(filePath))
                {
                    throw new Exception("Nie można odczytać poprawnej ścieżki do wybranego pliku.");
                }

                Log($"Wybrano plik: {filePath}");
                Log("Rozpoczynam wczytywanie danych i analizę LLM (lub ładuję z Cache)...");

                // 3. Odpalamy naszą fabrykę podając dynamiczną ścieżkę, którą wskazał użytkownik!
                DataPipeline pipeline = new DataPipeline();
                _universityData = await pipeline.PrepareDataAsync(filePath);

                Log("Sukces! Dane z JSON i LLM załadowane pomyślnie.");

                // 4. Wrzucamy prowadzących do tabeli
                var tabela = this.FindControl<DataGrid>("PlanDataGrid");
                if (tabela != null && _universityData.Instructors != null)
                {
                    tabela.ItemsSource = _universityData.Instructors;
                    Log("Tabela DataGrid została uzupełniona prowadzącymi.");
                }

                // 5. Odblokowujemy przycisk optymalizacji
                var optBtn = this.FindControl<Button>("RunOptimizationBtn");
                if (optBtn != null) optBtn.IsEnabled = true;
            }
            catch (Exception ex)
            {
                Log($"BŁĄD: {ex.Message}");
            }
            finally
            {
                if (loadBtn != null) loadBtn.IsEnabled = true;
            }
        }

        private async void RunOptimizationBtn_Click(object sender, RoutedEventArgs e)
        {
            Log("Uruchamianie silnika optymalizacji...");

            var optBtn = this.FindControl<Button>("RunOptimizationBtn");
            if (optBtn != null) optBtn.IsEnabled = false;

            try
            {
                // Weryfikacja, czy dane na pewno zostały wczytane
                if (_universityData == null || _universityData.Instructors == null ||
                    _universityData.Rooms == null || _universityData.Courses == null)
                {
                    throw new Exception("Brak pełnych danych! Najpierw wczytaj i przetwórz plik JSON.");
                }

                // Tworzymy instancję Twojego silnika z Modułu 2
                OptimizationEngine engine = new OptimizationEngine();

                Log("Krok 1/2: Generowanie planu bazowego (Ograniczenia Twarde)...");

                // Wyrzucamy ciężkie obliczenia do osobnego wątku w tle (Task.Run)
                var finalPlan = await Task.Run(() =>
                {
                    // 1. Uruchamiamy algorytm naiwny (HC)
                    var basePlan = engine.RunOptimization(_universityData.Instructors, _universityData.Rooms, _universityData.Courses);

                    // 2. Wrzucamy wynik do algorytmu optymalizującego (SC)
                    return engine.OptimizeSoftConstraints(basePlan, _universityData.Instructors, _universityData.Rooms);
                });

                Log($"Optymalizacja zakończona! Wygenerowano {finalPlan.Count} kafelków zajęć.");

                // Sukces! Podmieniamy dane w tabeli. 
                // Zamiast listy prowadzących, DataGrid pokaże teraz gotowy plan zajęć.
                // --- PROCES TŁUMACZENIA ID NA NAZWY ---
                var displayPlan = finalPlan.Select(lesson => {
                    // Wyszukujemy pełne obiekty na podstawie ID
                    var course = _universityData.Courses.FirstOrDefault(c => c.Id == lesson.CourseId);
                    var inst = _universityData.Instructors.FirstOrDefault(i => i.Id == lesson.InstructorId);
                    var room = _universityData.Rooms.FirstOrDefault(r => r.Id == lesson.RoomId);

                    // Tłumaczymy angielskie skróty i wymuszamy sortowanie dodając cyfry
                    string polishDay = lesson.Day switch
                    {
                        "Mon" => "1. Poniedziałek",
                        "Tue" => "2. Wtorek",
                        "Wed" => "3. Środa",
                        "Thu" => "4. Czwartek",
                        "Fri" => "5. Piątek",
                        _ => lesson.Day
                    };

                    return new DisplayLesson
                    {
                        DayName = polishDay,
                        TimeRange = $"{lesson.StartHour}:00 - {lesson.EndHour}:00",
                        CourseName = course != null ? course.Name : lesson.CourseId,
                        GroupName = course != null ? course.GroupId : "-",
                        InstructorName = inst != null ? inst.Name : lesson.InstructorId,
                        RoomName = room != null ? $"{room.Name} ({room.Id})" : lesson.RoomId
                    };

                })
                .OrderBy(l => l.DayName)       // Sortujemy najpierw po dniach
                .ThenBy(l => l.TimeRange)      // Potem po godzinach
                .ToList();

                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    // W nowoczesnej Avalonii odwołujemy się do tabeli bezpośrednio po jej nazwie z XAML-a:
                    if (PlanDataGrid != null)
                    {
                        PlanDataGrid.ItemsSource = displayPlan;
                        Log($"Tabela odświeżona! Wyświetlam {displayPlan.Count} wierszy.");
                    }
                    else
                    {
                        Log("BŁĄD UI: Nie znaleziono kontrolki PlanDataGrid!");
                    }

                    try
                    {
                        string plotPath = "wykres_zbieznosci.png";

                        // Sprawdzamy, czy silnik na pewno wygenerował plik
                        if (System.IO.File.Exists(plotPath))
                        {
                            if (ConvergencePlotImage != null)
                            {
                                // Avalonia wymaga specjalnego obiektu Bitmap do wyświetlania obrazków z dysku
                                ConvergencePlotImage.Source = new Bitmap(plotPath);
                                Log("Wykres zbieżności został załadowany do interfejsu.");
                            }
                        }
                        else
                        {
                            Log("BŁĄD: Nie znaleziono pliku wykresu na dysku.");
                        }
                    }
                    catch (Exception ex)
                    {
                        Log($"BŁĄD GUI przy ładowaniu wykresu: {ex.Message}");
                    }
                });

                var tabela = this.FindControl<DataGrid>("PlanDataGrid");
                if (tabela != null)
                {
                    // Wrzucamy nasze piękne, przetłumaczone i posortowane wiersze!
                    tabela.ItemsSource = displayPlan;
                }
            }
            catch (Exception ex)
            {
                Log($"BŁĄD SILNIKA: {ex.Message}");
            }
            finally
            {
                if (optBtn != null) optBtn.IsEnabled = true;
            }
        }
    }
    // Klasa reprezentująca jeden, ładny wiersz w naszej tabeli
    public class DisplayLesson
    {
        public string DayName { get; set; }
        public string TimeRange { get; set; }
        public string CourseName { get; set; }
        public string GroupName { get; set; }
        public string InstructorName { get; set; }
        public string RoomName { get; set; }
    }
}