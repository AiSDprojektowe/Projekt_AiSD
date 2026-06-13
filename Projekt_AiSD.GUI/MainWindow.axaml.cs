using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using System.Diagnostics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Projekt_AiSD.Models;
using Projekt_AiSD.Modules;

namespace Projekt_AiSD.GUI
{
    public partial class MainWindow : Window
    {
        private UniversityData _universityData;
        private List<DisplayLesson> _displayPlan = new List<DisplayLesson>();
        private List<DisplayLesson> _filteredPlan = new List<DisplayLesson>();
        private IReadOnlyList<int> _hardHistory = Array.Empty<int>();
        private IReadOnlyList<int> _softHistory = Array.Empty<int>();

        public MainWindow()
        {
            InitializeComponent();
            Opened += (_, _) => InitializeFilters();
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

                // 2. Pobieramy pełną ścieżkę do wybranego pliku
                var selectedFile = files[0];
                string filePath = selectedFile.TryGetLocalPath();

                if (string.IsNullOrEmpty(filePath))
                {
                    throw new Exception("Nie można odczytać poprawnej ścieżki do wybranego pliku.");
                }

                Log($"Wybrano plik: {filePath}");
                Log("Rozpoczynam wczytywanie danych i analizę LLM (lub ładuję z Cache)...");

                // 3. Odpalamy fabrykę danych
                DataPipeline pipeline = new DataPipeline();
                _universityData = await pipeline.PrepareDataAsync(filePath);

                Log("Sukces! Dane z JSON i LLM załadowane pomyślnie.");

                // 4. Wrzucamy prowadzących do tabeli na start
                var tabela = this.FindControl<DataGrid>("PlanDataGrid");
                if (tabela != null && _universityData.Instructors != null)
                {
                    tabela.ItemsSource = _universityData.Instructors;
                    Log("Tabela DataGrid została uzupełniona prowadzącymi.");
                }

                InitializeFilters();

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
                    _hardHistory = engine.LastHardConvergenceHistory;

                    return engine.OptimizeSoftConstraints(basePlan, _universityData.Instructors, _universityData.Rooms);
                });

                _softHistory = engine.LastConvergenceHistory;

                Log($"Optymalizacja zakończona! Wygenerowano {finalPlan.Count} kafelków zajęć.");

                var convergenceCanvas = this.FindControl<Canvas>("ConvergenceCanvas");
                if (convergenceCanvas != null)
                {
                    DrawConvergencePlot(convergenceCanvas, _softHistory);
                    Log($"Wykres zbieżności odświeżony ({_softHistory.Count} punktów).");
                    ExportConvergenceChartToHtml(_softHistory);
                    Log("Wykres zapisany do HTML i otwarty w przeglądarce.");
                }

                // Tłumaczenie ID na nazwy wyświetlane
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
                        CourseType = course != null ? PolishCourseType(course.Type) : "-",
                        GroupName = course != null ? course.GroupId : "-",
                        InstructorName = inst != null ? inst.Name : lesson.InstructorId,
                        RoomName = room != null ? $"{room.Name} ({room.Id})" : lesson.RoomId
                    };
                })
                .OrderBy(l => l.DayName)
                .ThenBy(l => l.TimeRange)
                .ToList();

                _displayPlan = displayPlan;
                ApplyFilters();

                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    if (PlanDataGrid != null)
                    {
                        PlanDataGrid.ItemsSource = _filteredPlan;
                        Log($"Tabela odświeżona! Wyświetlam {_filteredPlan.Count} wierszy.");
                    }
                    else
                    {
                        Log("BŁĄD UI: Nie znaleziono kontrolki PlanDataGrid!");
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

        private void InitializeFilters()
        {
            if (_universityData == null) return;

            var instructorCombo = this.FindControl<ComboBox>("FilterInstructorCombo");
            var roomCombo = this.FindControl<ComboBox>("FilterRoomCombo");
            var groupCombo = this.FindControl<ComboBox>("FilterGroupCombo");

            if (instructorCombo != null)
            {
                instructorCombo.ItemsSource = new[] { "Wszyscy" }.Concat(_universityData.Instructors?.Select(i => i.Name) ?? Enumerable.Empty<string>()).ToList();
                instructorCombo.SelectedIndex = 0;
                instructorCombo.SelectionChanged -= FiltersChanged;
                instructorCombo.SelectionChanged += FiltersChanged;
            }

            if (roomCombo != null)
            {
                roomCombo.ItemsSource = new[] { "Wszystkie" }.Concat(_universityData.Rooms?.Select(r => r.Name) ?? Enumerable.Empty<string>()).ToList();
                roomCombo.SelectedIndex = 0;
                roomCombo.SelectionChanged -= FiltersChanged;
                roomCombo.SelectionChanged += FiltersChanged;
            }

            if (groupCombo != null)
            {
                groupCombo.ItemsSource = new[] { "Wszystkie" }.Concat(_universityData.Courses?.Select(c => c.GroupId).Where(g => !string.IsNullOrWhiteSpace(g)).Distinct() ?? Enumerable.Empty<string>()).ToList();
                groupCombo.SelectedIndex = 0;
                groupCombo.SelectionChanged -= FiltersChanged;
                groupCombo.SelectionChanged += FiltersChanged;
            }

            ApplyFilters();
        }

        private void FiltersChanged(object? sender, SelectionChangedEventArgs e)
        {
            ApplyFilters();
            var tabela = this.FindControl<DataGrid>("PlanDataGrid");
            if (tabela != null)
            {
                tabela.ItemsSource = _filteredPlan;
            }
        }

        private void ApplyFilters()
        {
            IEnumerable<DisplayLesson> query = _displayPlan;
            var instructorCombo = this.FindControl<ComboBox>("FilterInstructorCombo");
            var roomCombo = this.FindControl<ComboBox>("FilterRoomCombo");
            var groupCombo = this.FindControl<ComboBox>("FilterGroupCombo");

            string instructor = instructorCombo?.SelectedItem as string;
            string room = roomCombo?.SelectedItem as string;
            string group = groupCombo?.SelectedItem as string;

            if (!string.IsNullOrWhiteSpace(instructor) && instructor != "Wszyscy")
            {
                query = query.Where(x => x.InstructorName == instructor);
            }

            if (!string.IsNullOrWhiteSpace(room) && room != "Wszystkie")
            {
                query = query.Where(x => x.RoomName.StartsWith(room));
            }

            if (!string.IsNullOrWhiteSpace(group) && group != "Wszystkie")
            {
                query = query.Where(x => x.GroupName == group);
            }

            _filteredPlan = query.ToList();
        }

        private static void DrawConvergencePlot(Canvas canvas, IReadOnlyList<int> values)
        {
            canvas.Children.Clear();

            if (values == null || values.Count == 0)
            {
                canvas.Children.Add(new TextBlock
                {
                    Text = "Brak danych do wykresu zbieżności.",
                    Foreground = Brushes.Gray,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
                });
                return;
            }

            double width = canvas.Bounds.Width > 0 ? canvas.Bounds.Width : 900;
            double height = canvas.Bounds.Height > 0 ? canvas.Bounds.Height : 500;

            double leftMargin = 60;
            double rightMargin = 20;
            double topMargin = 25;
            double bottomMargin = 50;

            double plotWidth = Math.Max(1, width - leftMargin - rightMargin);
            double plotHeight = Math.Max(1, height - topMargin - bottomMargin);

            int minValue = values.Min();
            int maxValue = values.Max();
            if (minValue == maxValue)
            {
                minValue -= 1;
                maxValue += 1;
            }

            double ValueToY(double value)
            {
                double normalized = (value - minValue) / (double)(maxValue - minValue);
                return topMargin + (1.0 - normalized) * plotHeight;
            }

            double IndexToX(int index)
            {
                if (values.Count == 1) return leftMargin + plotWidth / 2.0;
                return leftMargin + (index * plotWidth / (values.Count - 1));
            }

            canvas.Children.Add(new Line
            {
                StartPoint = new Point(leftMargin, topMargin),
                EndPoint = new Point(leftMargin, topMargin + plotHeight),
                Stroke = Brushes.DimGray,
                StrokeThickness = 1
            });

            canvas.Children.Add(new Line
            {
                StartPoint = new Point(leftMargin, topMargin + plotHeight),
                EndPoint = new Point(leftMargin + plotWidth, topMargin + plotHeight),
                Stroke = Brushes.DimGray,
                StrokeThickness = 1
            });

            var polyline = new Polyline
            {
                Stroke = Brushes.DodgerBlue,
                StrokeThickness = 2
            };

            for (int i = 0; i < values.Count; i++)
            {
                polyline.Points.Add(new Point(IndexToX(i), ValueToY(values[i])));
            }

            canvas.Children.Add(polyline);

            var title = new TextBlock
            {
                Text = "Zbieżność funkcji celu",
                FontSize = 18,
                FontWeight = FontWeight.Bold,
                Foreground = Brushes.Black
            };
            Canvas.SetLeft(title, leftMargin);
            Canvas.SetTop(title, 0);
            canvas.Children.Add(title);

            var yAxisLabel = new TextBlock
            {
                Text = "Kolizje",
                Foreground = Brushes.Gray
            };
            Canvas.SetLeft(yAxisLabel, 0);
            Canvas.SetTop(yAxisLabel, topMargin + 18);
            canvas.Children.Add(yAxisLabel);

            var xAxisLabel = new TextBlock
            {
                Text = "Iteracja",
                Foreground = Brushes.Gray
            };
            Canvas.SetLeft(xAxisLabel, leftMargin + plotWidth / 2.0 - 30);
            Canvas.SetTop(xAxisLabel, topMargin + plotHeight + 25);
            canvas.Children.Add(xAxisLabel);

            var minLabel = new TextBlock
            {
                Text = minValue.ToString(),
                Foreground = Brushes.Gray
            };
            Canvas.SetLeft(minLabel, 10);
            Canvas.SetTop(minLabel, ValueToY(minValue) - 10);
            canvas.Children.Add(minLabel);

            var maxLabel = new TextBlock
            {
                Text = maxValue.ToString(),
                Foreground = Brushes.Gray
            };
            Canvas.SetLeft(maxLabel, 10);
            Canvas.SetTop(maxLabel, ValueToY(maxValue) - 10);
            canvas.Children.Add(maxLabel);

            var startLabel = new TextBlock
            {
                Text = "0",
                Foreground = Brushes.Gray
            };
            Canvas.SetLeft(startLabel, leftMargin - 5);
            Canvas.SetTop(startLabel, topMargin + plotHeight + 5);
            canvas.Children.Add(startLabel);

            var endLabel = new TextBlock
            {
                Text = (values.Count - 1).ToString(),
                Foreground = Brushes.Gray
            };
            Canvas.SetLeft(endLabel, leftMargin + plotWidth - 10);
            Canvas.SetTop(endLabel, topMargin + plotHeight + 5);
            canvas.Children.Add(endLabel);
        }

        private static string PolishCourseType(string? type)
        {
            return type?.ToLowerInvariant() switch
            {
                "lecture" => "Wykład",
                "lab" => "Laboratorium",
                "laboratory" => "Laboratorium",
                "exercise" => "Ćwiczenia",
                "tutorial" => "Ćwiczenia",
                "seminar" => "Seminarium",
                "project" => "Projekt",
                "practical" => "Zajęcia praktyczne",
                "theory" => "Teoria",
                "wyklad" => "Wykład",
                "laboratorium" => "Laboratorium",
                "cwiczenia" => "Ćwiczenia",
                _ when string.IsNullOrWhiteSpace(type) => "-",
                _ => char.ToUpper(type[0]) + type[1..]
            };
        }

        private static void ExportConvergenceChartToHtml(IReadOnlyList<int> values)
        {
            if (values == null || values.Count == 0) return;

            int minValue = values.Min();
            int maxValue = values.Max();
            if (minValue == maxValue)
            {
                minValue -= 1;
                maxValue += 1;
            }

            var valuesJson = string.Join(",", values);

            var html = $@"<!doctype html>
<html lang='pl'>
<head>
    <meta charset='utf-8' />
    <meta name='viewport' content='width=device-width, initial-scale=1' />
    <title>Wykres zbieżności</title>
    <style>
        body {{ margin: 0; font-family: Arial, sans-serif; background: linear-gradient(180deg, #eef2f7 0%, #f7f9fc 100%); color: #1f2937; }}
        .wrap {{ max-width: 1200px; margin: 24px auto; padding: 16px; }}
        .card {{ background: white; border: 1px solid #d8dde3; border-radius: 16px; box-shadow: 0 10px 35px rgba(15, 23, 42, .10); overflow: hidden; }}
        .header {{ padding: 18px 22px; border-bottom: 1px solid #e7ebef; display: flex; justify-content: space-between; gap: 16px; align-items: flex-end; flex-wrap: wrap; }}
        .header h1 {{ margin: 0; font-size: 24px; }}
        .header p {{ margin: 6px 0 0; color: #6b7280; }}
        .stats {{ display: flex; gap: 14px; flex-wrap: wrap; }}
        .stat {{ background: #f8fafc; border: 1px solid #e5e7eb; border-radius: 12px; padding: 10px 14px; min-width: 120px; }}
        .stat .label {{ display: block; color: #6b7280; font-size: 12px; text-transform: uppercase; letter-spacing: .04em; }}
        .stat .value {{ display: block; font-size: 18px; font-weight: 700; margin-top: 4px; }}
        .chart {{ width: 100%; height: 620px; padding: 10px; box-sizing: border-box; }}
        canvas {{ width: 100%; height: 100%; display: block; border-radius: 12px; background: #fff; }}
    </style>
</head>
<body>
    <div class='wrap'>
        <div class='card'>
            <div class='header'>
                <div>
                    <h1>Wykres zbieżności</h1>
                    <p>Im niżej przebiega linia, tym mniej kolizji i lepszy plan.</p>
                </div>
                <div class='stats'>
                    <div class='stat'><span class='label'>Punktów</span><span class='value'>{values.Count}</span></div>
                    <div class='stat'><span class='label'>Start</span><span class='value'>{values[0]}</span></div>
                    <div class='stat'><span class='label'>Koniec</span><span class='value'>{values[values.Count - 1]}</span></div>
                </div>
            </div>
            <div class='chart'>
                <canvas id='chart'></canvas>
            </div>
        </div>
    </div>
    <script>
        const data = [{valuesJson}];
        const canvas = document.getElementById('chart');
        const ctx = canvas.getContext('2d');

        function resize() {{
            const rect = canvas.getBoundingClientRect();
            const dpr = window.devicePixelRatio || 1;
            canvas.width = Math.max(1, Math.floor(rect.width * dpr));
            canvas.height = Math.max(1, Math.floor(rect.height * dpr));
            ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
            draw();
        }}

        function draw() {{
            const rect = canvas.getBoundingClientRect();
            const width = rect.width;
            const height = rect.height;
            if (!width || !height || !data.length) return;

            const padding = {{ left: 70, right: 30, top: 30, bottom: 60 }};
            const plotWidth = width - padding.left - padding.right;
            const plotHeight = height - padding.top - padding.bottom;
            const min = Math.min(...data);
            const max = Math.max(...data);
            const yMin = min === max ? min - 1 : min;
            const yMax = min === max ? max + 1 : max;
            const stepX = data.length === 1 ? 0 : plotWidth / (data.length - 1);

            ctx.clearRect(0, 0, width, height);

            ctx.fillStyle = '#ffffff';
            ctx.fillRect(0, 0, width, height);

            ctx.strokeStyle = '#e5e7eb';
            ctx.lineWidth = 1;
            ctx.font = '12px Arial';
            ctx.fillStyle = '#6b7280';
            ctx.textAlign = 'right';
            ctx.textBaseline = 'middle';

            const gridLines = 5;
            for (let i = 0; i <= gridLines; i++) {{
                const t = i / gridLines;
                const y = padding.top + t * plotHeight;
                ctx.beginPath();
                ctx.moveTo(padding.left, y);
                ctx.lineTo(width - padding.right, y);
                ctx.stroke();
                const value = Math.round(yMax - t * (yMax - yMin));
                ctx.fillText(String(value), padding.left - 10, y);
            }}

            ctx.strokeStyle = '#9ca3af';
            ctx.beginPath();
            ctx.moveTo(padding.left, padding.top);
            ctx.lineTo(padding.left, height - padding.bottom);
            ctx.lineTo(width - padding.right, height - padding.bottom);
            ctx.stroke();

            ctx.strokeStyle = '#1d4ed8';
            ctx.fillStyle = '#1d4ed8';
            ctx.lineWidth = 3;
            ctx.beginPath();
            data.forEach((value, index) => {{
                const x = data.length === 1 ? padding.left + plotWidth / 2 : padding.left + index * stepX;
                const ratio = (value - yMin) / (yMax - yMin);
                const y = padding.top + (1 - ratio) * plotHeight;
                if (index === 0) ctx.moveTo(x, y); else ctx.lineTo(x, y);
            }});
            ctx.stroke();

            data.forEach((value, index) => {{
                const x = data.length === 1 ? padding.left + plotWidth / 2 : padding.left + index * stepX;
                const ratio = (value - yMin) / (yMax - yMin);
                const y = padding.top + (1 - ratio) * plotHeight;
                ctx.beginPath();
                ctx.arc(x, y, 4, 0, Math.PI * 2);
                ctx.fill();
            }});

            ctx.fillStyle = '#111827';
            ctx.textAlign = 'left';
            ctx.textBaseline = 'alphabetic';
            ctx.font = 'bold 18px Arial';
            ctx.fillText('Zbieżność funkcji celu', padding.left, 22);

            ctx.font = '12px Arial';
            ctx.fillStyle = '#6b7280';
            ctx.textAlign = 'center';
            ctx.textBaseline = 'top';
            const tickCount = Math.min(8, data.length - 1);
            for (let i = 0; i <= tickCount; i++) {{
                const index = tickCount === 0 ? 0 : Math.round(i * (data.length - 1) / tickCount);
                const x = data.length === 1 ? padding.left + plotWidth / 2 : padding.left + index * stepX;
                ctx.fillText(String(index), x, height - padding.bottom + 10);
            }}

            ctx.save();
            ctx.translate(18, height / 2);
            ctx.rotate(-Math.PI / 2);
            ctx.textAlign = 'center';
            ctx.fillText('Kolizje', 0, 0);
            ctx.restore();

            ctx.fillStyle = '#6b7280';
            ctx.textAlign = 'center';
            ctx.textBaseline = 'top';
            ctx.font = '12px Arial';
            ctx.fillText('Iteracja', padding.left + plotWidth / 2, height - 14);
        }}

        window.addEventListener('resize', resize);
        resize();
    </script>
</body>
</html>";

            var htmlPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "projekt_aisd_wykres_zbieznosci.html");
            File.WriteAllText(htmlPath, html, Encoding.UTF8);

            Process.Start(new ProcessStartInfo
            {
                FileName = htmlPath,
                UseShellExecute = true
            });
        }
    }

    public class DisplayLesson
    {
        public string DayName { get; set; }
        public string TimeRange { get; set; }
        public string CourseName { get; set; }
        public string CourseType { get; set; }
        public string GroupName { get; set; }
        public string InstructorName { get; set; }
        public string RoomName { get; set; }
    }
}