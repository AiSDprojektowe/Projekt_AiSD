using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage; 
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
                var topLevel = TopLevel.GetTopLevel(this);
                if (topLevel == null) throw new Exception("Nie można uzyskać dostępu do okna aplikacji.");

                var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                {
                    Title = "Wybierz plik z danymi uczelni",
                    AllowMultiple = false,
                    FileTypeFilter = new[]
                    {
                        new FilePickerFileType("Pliki JSON") { Patterns = new[] { "*.json" } }
                    }
                });

                if (files == null || !files.Any())
                {
                    Log("Anulowano wybór pliku.");
                    return;
                }

                var selectedFile = files[0];
                string filePath = selectedFile.TryGetLocalPath();

                if (string.IsNullOrEmpty(filePath))
                {
                    throw new Exception("Nie można odczytać poprawnej ścieżki do wybranego pliku.");
                }

                Log($"Wybrano plik: {filePath}");
                Log("Rozpoczynam wczytywanie danych i analizę LLM (lub ładuję z Cache)...");

                DataPipeline pipeline = new DataPipeline();
                _universityData = await pipeline.PrepareDataAsync(filePath);

                Log("Sukces! Dane z JSON i LLM załadowane pomyślnie.");

                var tabela = this.FindControl<DataGrid>("PlanDataGrid");
                if (tabela != null && _universityData.Instructors != null)
                {
                    tabela.ItemsSource = _universityData.Instructors;
                    Log("Tabela DataGrid została uzupełniona prowadzącymi.");
                }

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
                
                if (_universityData == null || _universityData.Instructors == null ||
                    _universityData.Rooms == null || _universityData.Courses == null)
                {
                    throw new Exception("Brak pełnych danych! Najpierw wczytaj i przetwórz plik JSON.");
                }

                
                OptimizationEngine engine = new OptimizationEngine();

                Log("Krok 1/2: Generowanie planu bazowego (Ograniczenia Twarde)...");

                
                var finalPlan = await Task.Run(() =>
                {
                   
                    var basePlan = engine.RunOptimization(_universityData.Instructors, _universityData.Rooms, _universityData.Courses);

                    
                    return engine.OptimizeSoftConstraints(basePlan, _universityData.Instructors, _universityData.Rooms);
                });

                Log($"Optymalizacja zakończona! Wygenerowano {finalPlan.Count} kafelków zajęć.");

                try
                {
                    Visualization.GenerateHtmlReport(finalPlan, _universityData.Instructors, _universityData.Rooms, _universityData.Courses);
                    Log("Zapisano elegancki raport HTML na dysku!");
                }
                catch (Exception ex)
                {
                    Log($"BŁĄD przy generowaniu HTML: {ex.Message}");
                }
  

               
                var displayPlan = finalPlan.Select(lesson => {
                    
                    var course = _universityData.Courses.FirstOrDefault(c => c.Id == lesson.CourseId);
                    var inst = _universityData.Instructors.FirstOrDefault(i => i.Id == lesson.InstructorId);
                    var room = _universityData.Rooms.FirstOrDefault(r => r.Id == lesson.RoomId);

                    
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
                .OrderBy(l => l.DayName)       
                .ThenBy(l => l.TimeRange)      
                .ToList();

                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    
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

                        if (System.IO.File.Exists(plotPath))
                        {
                            if (ConvergencePlotImage != null)
                            {
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