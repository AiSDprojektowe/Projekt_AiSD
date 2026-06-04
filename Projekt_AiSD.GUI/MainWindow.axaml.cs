using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage; // WAŻNE: Dodana biblioteka do obsługi Eksploratora plików
using System;
using System.Linq;
using System.Threading.Tasks;
using Projekt_AiSD.Models;
using Projekt_AiSD.Modules;

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

        private void RunOptimizationBtn_Click(object sender, RoutedEventArgs e)
        {
            Log("Uruchamianie silnika optymalizacji...");

            // Miejsce na uruchomienie Algorytmu HC

            Log("Optymalizacja zakończona. Plan wygenerowany!");
        }
    }
}