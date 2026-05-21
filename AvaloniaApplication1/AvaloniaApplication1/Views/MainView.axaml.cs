using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace AvaloniaApplication1.Views;

public partial class MainView : UserControl
{
    private JsonDocument? loadedJson;

    public MainView()
    {
        AvaloniaXamlLoader.Load(this);

        var loadButton = this.FindControl<Button>("LoadJsonButton");
        var generateButton = this.FindControl<Button>("GeneratePlanButton");

        if (loadButton != null)
            loadButton.Click += (_, _) => LoadJson();

        if (generateButton != null)
            generateButton.Click += (_, _) => GeneratePlan();
    }

    private void LoadJson()
    {
        var pathBox = this.FindControl<TextBox>("JsonPathBox");

        string path = pathBox?.Text ?? "";

        if (string.IsNullOrWhiteSpace(path))
        {
            SetStatus("Status: wpisz ścieżkę do pliku JSON.");
            return;
        }

        if (!File.Exists(path))
        {
            SetStatus("Status: nie znaleziono pliku JSON.");
            return;
        }

        try
        {
            string jsonText = File.ReadAllText(path);

            loadedJson?.Dispose();
            loadedJson = JsonDocument.Parse(jsonText);

            SetStatus("Status: JSON został poprawnie wczytany.");
        }
        catch (Exception ex)
        {
            SetStatus($"Status: błąd JSON: {ex.Message}");
        }
    }

    private void GeneratePlan()
    {
        if (loadedJson == null)
        {
            SetStatus("Status: najpierw wczytaj JSON.");
            return;
        }

        var instructors = GetInstructors();
        var courses = GetCourses();
        var rooms = GetRooms();

        if (instructors.Count == 0)
        {
            SetStatus("Status: brak prowadzących w JSON.");
            return;
        }

        if (courses.Count == 0)
        {
            SetStatus("Status: brak przedmiotów w JSON.");
            return;
        }

        if (rooms.Count == 0)
        {
            SetStatus("Status: brak sal w JSON.");
            return;
        }

        List<PlanItem> plan = BuildPlan(instructors, courses, rooms);

        var output = this.FindControl<TextBox>("PlanOutputBox");

        if (output != null)
            output.Text = FormatPlan(plan);

        DrawChart(plan);

        SetStatus("Status: wygenerowano plan zajęć z danych JSON.");
    }

    private List<InstructorInfo> GetInstructors()
    {
        var result = new List<InstructorInfo>();

        if (loadedJson == null)
            return result;

        var root = loadedJson.RootElement;

        if (!root.TryGetProperty("instructors", out var instructors))
            return result;

        foreach (var instructor in instructors.EnumerateArray())
        {
            var info = new InstructorInfo
            {
                Id = GetString(instructor, "id"),
                Name = GetString(instructor, "name"),
                PreferencesText = GetString(instructor, "preferences_text"),
                StartHour = 8,
                EndHour = 16
            };

            if (instructor.TryGetProperty("subjects", out var subjects))
            {
                foreach (var subject in subjects.EnumerateArray())
                {
                    info.Subjects.Add(subject.GetString() ?? "");
                }
            }

            ParsePreferences(info);

            result.Add(info);
        }

        return result;
    }

    private void ParsePreferences(InstructorInfo instructor)
    {
        string text = instructor.PreferencesText.ToLower();

        if (text.Contains("rano"))
        {
            instructor.StartHour = 8;
            instructor.EndHour = 12;
        }
        else if (text.Contains("popołudniu") || text.Contains("po południu"))
        {
            instructor.StartHour = 12;
            instructor.EndHour = 16;
        }
        else
        {
            instructor.StartHour = 8;
            instructor.EndHour = 16;
        }

        if (text.Contains("nie prowadzę w piątek") || text.Contains("nie prowadze w piatek"))
        {
            instructor.ForbiddenDays.Add("Piątek");
        }
    }

    private List<CourseInfo> GetCourses()
    {
        var result = new List<CourseInfo>();

        if (loadedJson == null)
            return result;

        var root = loadedJson.RootElement;

        if (!root.TryGetProperty("courses", out var courses))
            return result;

        foreach (var course in courses.EnumerateArray())
        {
            string direction = GetString(course, "direction");

            if (string.IsNullOrWhiteSpace(direction))
                direction = GetString(course, "kierunek");

            if (string.IsNullOrWhiteSpace(direction))
                direction = GetString(course, "field");

            if (string.IsNullOrWhiteSpace(direction))
                direction = "brak kierunku";

            result.Add(new CourseInfo
            {
                Id = GetString(course, "id"),
                Name = string.IsNullOrWhiteSpace(GetString(course, "name"))
                    ? GetString(course, "id")
                    : GetString(course, "name"),
                Direction = direction,
                Type = GetString(course, "type"),
                RequiredRoomType = GetString(course, "required_room_type")
            });
        }

        return result;
    }

    private List<RoomInfo> GetRooms()
    {
        var result = new List<RoomInfo>();

        if (loadedJson == null)
            return result;

        var root = loadedJson.RootElement;

        if (!root.TryGetProperty("rooms", out var rooms))
            return result;

        foreach (var room in rooms.EnumerateArray())
        {
            string id = GetString(room, "id");
            string name = GetString(room, "name");

            result.Add(new RoomInfo
            {
                Id = id,
                Name = string.IsNullOrWhiteSpace(name) ? id : name,
                Type = GetString(room, "type")
            });
        }

        return result;
    }

    private List<PlanItem> BuildPlan(
        List<InstructorInfo> instructors,
        List<CourseInfo> courses,
        List<RoomInfo> rooms)
    {
        string[] days =
        {
            "Poniedziałek",
            "Wtorek",
            "Środa",
            "Czwartek",
            "Piątek"
        };

        var plan = new List<PlanItem>();

        int roomIndex = 0;

        foreach (string day in days)
        {
            for (int hour = 8; hour < 16; hour += 2)
            {
                foreach (var instructor in instructors)
                {
                    if (instructor.ForbiddenDays.Contains(day))
                        continue;

                    if (hour < instructor.StartHour || hour >= instructor.EndHour)
                        continue;

                    var course = courses.FirstOrDefault(c =>
                        instructor.Subjects.Any(s =>
                            string.Equals(s, c.Id, StringComparison.OrdinalIgnoreCase)));

                    if (course == null)
                        continue;

                    RoomInfo room = FindRoomForCourse(course, rooms, ref roomIndex);

                    bool instructorBusy = plan.Any(p =>
                        p.Day == day &&
                        p.Hour == $"{hour}:00-{hour + 2}:00" &&
                        p.Instructor == instructor.Name);

                    bool roomBusy = plan.Any(p =>
                        p.Day == day &&
                        p.Hour == $"{hour}:00-{hour + 2}:00" &&
                        p.Room == room.Name);

                    if (instructorBusy || roomBusy)
                        continue;

                    plan.Add(new PlanItem
                    {
                        Day = day,
                        Hour = $"{hour}:00-{hour + 2}:00",
                        Instructor = instructor.Name,
                        Course = course.Name,
                        Direction = course.Direction,
                        Room = room.Name
                    });

                    break;
                }
            }
        }

        return plan;
    }

    private RoomInfo FindRoomForCourse(
        CourseInfo course,
        List<RoomInfo> rooms,
        ref int roomIndex)
    {
        var matchingRoom = rooms.FirstOrDefault(r =>
            !string.IsNullOrWhiteSpace(course.RequiredRoomType) &&
            string.Equals(r.Type, course.RequiredRoomType, StringComparison.OrdinalIgnoreCase));

        if (matchingRoom != null)
            return matchingRoom;

        if (roomIndex >= rooms.Count)
            roomIndex = 0;

        var room = rooms[roomIndex];
        roomIndex++;

        return room;
    }

    private string FormatPlan(List<PlanItem> plan)
    {
        var lines = new List<string>();

        lines.Add("DZIEŃ          GODZINA       PROWADZĄCY              PRZEDMIOT                 SALA          KIERUNEK");
        lines.Add("-------------------------------------------------------------------------------------------------------------");

        foreach (var item in plan)
        {
            lines.Add(
                $"{item.Day,-14} {item.Hour,-12} {item.Instructor,-22} {item.Course,-25} {item.Room,-13} {item.Direction}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private void DrawChart(List<PlanItem> plan)
    {
        var canvas = this.FindControl<Canvas>("ChartCanvas");

        if (canvas == null)
            return;

        canvas.Children.Clear();

        string[] days =
        {
            "Poniedziałek",
            "Wtorek",
            "Środa",
            "Czwartek",
            "Piątek"
        };

        int max = Math.Max(1, days.Max(day => plan.Count(p => p.Day == day)));

        double startX = 40;
        double baseY = 150;
        double barWidth = 60;
        double gap = 45;

        for (int i = 0; i < days.Length; i++)
        {
            int count = plan.Count(p => p.Day == days[i]);
            double barHeight = 100.0 * count / max;

            var bar = new Rectangle
            {
                Width = barWidth,
                Height = barHeight,
                Fill = Brushes.Black
            };

            Canvas.SetLeft(bar, startX + i * (barWidth + gap));
            Canvas.SetTop(bar, baseY - barHeight);

            canvas.Children.Add(bar);

            var label = new TextBlock
            {
                Text = days[i].Substring(0, 3),
                FontSize = 12
            };

            Canvas.SetLeft(label, startX + i * (barWidth + gap) + 15);
            Canvas.SetTop(label, baseY + 8);

            canvas.Children.Add(label);
        }

        var axis = new Line
        {
            StartPoint = new Avalonia.Point(20, baseY),
            EndPoint = new Avalonia.Point(570, baseY),
            Stroke = Brushes.Gray,
            StrokeThickness = 1
        };

        canvas.Children.Add(axis);
    }

    private string GetString(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var property))
        {
            return property.GetString() ?? "";
        }

        return "";
    }

    private void SetStatus(string text)
    {
        var status = this.FindControl<TextBlock>("StatusText");

        if (status != null)
            status.Text = text;
    }

    private class InstructorInfo
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string PreferencesText { get; set; } = "";
        public List<string> Subjects { get; set; } = new();
        public List<string> ForbiddenDays { get; set; } = new();
        public int StartHour { get; set; } = 8;
        public int EndHour { get; set; } = 16;
    }

    private class CourseInfo
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Direction { get; set; } = "";
        public string Type { get; set; } = "";
        public string RequiredRoomType { get; set; } = "";
    }

    private class RoomInfo
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Type { get; set; } = "";
    }

    private class PlanItem
    {
        public string Day { get; set; } = "";
        public string Hour { get; set; } = "";
        public string Instructor { get; set; } = "";
        public string Course { get; set; } = "";
        public string Direction { get; set; } = "";
        public string Room { get; set; } = "";
    }
}