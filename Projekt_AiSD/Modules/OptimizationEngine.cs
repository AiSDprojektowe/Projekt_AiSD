using Projekt_AiSD.Models;
using ScottPlot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.IO;

namespace Projekt_AiSD.Modules
{
    public class ScheduledLesson
    {
        public string CourseId { get; set; }
        public string GroupId { get; set; }
        public string InstructorId { get; set; }
        public string RoomId { get; set; }
        public string Day { get; set; }
        public int StartHour { get; set; }
        public int EndHour { get; set; }
    }

    public class OptimizationEngine
    {
        private static readonly string[] Days = { "Mon", "Tue", "Wed", "Thu", "Fri" };
        private const int StartDayHour = 8;
        private const int EndDayHour = 20;
        private const int TotalHoursPerDay = EndDayHour - StartDayHour;

        private class InstructorPref
        {
            public HashSet<string> PreferredDays { get; set; } = new HashSet<string>();
            public int PreferredHoursStart { get; set; } = 8;
            public int PreferredHoursEnd { get; set; } = 20;
            public int MinStartHour { get; set; } = 8;
            public int MaxHoursPerWeek { get; set; } = 16;
            public Dictionary<string, HashSet<int>> ForbiddenSlots { get; set; } = new Dictionary<string, HashSet<int>>();
        }

        private string MapDayToShort(string fullDay)
        {
            if (string.IsNullOrEmpty(fullDay)) return fullDay;
            string lower = fullDay.ToLower().Trim();

            if (lower.Contains("mon") || lower.Contains("pon")) return "Mon";
            if (lower.Contains("tue") || lower.Contains("wto")) return "Tue";
            if (lower.Contains("wed") || lower.Contains("śro") || lower.Contains("sro")) return "Wed";
            if (lower.Contains("thu") || lower.Contains("czw")) return "Thu";
            if (lower.Contains("fri") || lower.Contains("pią") || lower.Contains("pia")) return "Fri";

            return fullDay;
        }

        private Dictionary<string, InstructorPref> LoadPreferences(List<Instructor> instructors)
        {
            var instructorPrefs = new Dictionary<string, InstructorPref>();
            string[] allDays = { "Mon", "Tue", "Wed", "Thu", "Fri" };

            foreach (var inst in instructors)
            {
                var pref = new InstructorPref();
                var type = inst.GetType();

                var prop = type.GetProperty("ParsedPreferences") ?? type.GetProperty("Preferences") ?? type.GetProperty("preferences");
                var field = type.GetField("ParsedPreferences") ?? type.GetField("Preferences") ?? type.GetField("preferences");

                object prefsObj = prop != null ? prop.GetValue(inst) : field?.GetValue(inst);

                if (prefsObj != null)
                {
                    int minStart = 8;
                    int maxEnd = 20;

                    if (prefsObj is System.Text.Json.JsonElement jsonElement && jsonElement.ValueKind == System.Text.Json.JsonValueKind.Object)
                    {
                        var fsProp = jsonElement.EnumerateObject().FirstOrDefault(p => p.Name.ToLower().Contains("forbidden"));
                        if (fsProp.Value.ValueKind == System.Text.Json.JsonValueKind.Object)
                        {
                            foreach (var dayProp in fsProp.Value.EnumerateObject())
                            {
                                string shortDay = MapDayToShort(dayProp.Name);
                                var hours = new HashSet<int>();
                                if (dayProp.Value.ValueKind == System.Text.Json.JsonValueKind.Array)
                                {
                                    foreach (var h in dayProp.Value.EnumerateArray())
                                    {
                                        if (h.TryGetInt32(out int hour)) hours.Add(hour);
                                    }
                                }
                                pref.ForbiddenSlots[shortDay] = hours;
                            }
                        }

                        if (jsonElement.TryGetProperty("min_start_hour", out var minP) && minP.ValueKind == System.Text.Json.JsonValueKind.Number)
                            minStart = minP.GetInt32();
                        if (jsonElement.TryGetProperty("max_end_hour", out var maxP) && maxP.ValueKind == System.Text.Json.JsonValueKind.Number)
                            maxEnd = maxP.GetInt32();
                    }
                    else
                    {
                        var prefType = prefsObj.GetType();
                        var fsProp = prefType.GetProperties().FirstOrDefault(p => p.Name.ToLower().Contains("forbidden"));
                        if (fsProp != null)
                        {
                            var rawValue = fsProp.GetValue(prefsObj);
                            if (rawValue is Dictionary<string, List<int>> fList)
                            {
                                foreach (var kvp in fList) pref.ForbiddenSlots[MapDayToShort(kvp.Key)] = new HashSet<int>(kvp.Value);
                            }
                            else if (rawValue is Dictionary<string, HashSet<int>> fHash)
                            {
                                foreach (var kvp in fHash) pref.ForbiddenSlots[MapDayToShort(kvp.Key)] = kvp.Value;
                            }
                        }

                        var minProp = prefType.GetProperty("MinStartHour") ?? prefType.GetProperty("min_start_hour");
                        if (minProp != null) minStart = (int)minProp.GetValue(prefsObj);

                        var maxProp = prefType.GetProperty("MaxEndHour") ?? prefType.GetProperty("max_end_hour");
                        if (maxProp != null) maxEnd = (int)maxProp.GetValue(prefsObj);
                    }

                    foreach (var day in allDays)
                    {
                        if (!pref.ForbiddenSlots.ContainsKey(day))
                        {
                            pref.ForbiddenSlots[day] = new HashSet<int>();
                        }

                        for (int h = 8; h < 20; h++)
                        {
                            if (h < minStart || h >= maxEnd)
                            {
                                pref.ForbiddenSlots[day].Add(h);
                            }
                        }
                    }
                }
                instructorPrefs[inst.Id] = pref;
            }

            var demborski = instructorPrefs.GetValueOrDefault("I09");
            if (demborski != null && demborski.ForbiddenSlots.Any())
            {
                Console.WriteLine($"[DEBUG SILNIKA]" + string.Join(" | ", demborski.ForbiddenSlots.Select(x => $"{x.Key}: [{string.Join(",", x.Value)}]")));
            }

            return instructorPrefs;
        }

        public List<ScheduledLesson> RunOptimization(List<Instructor> instructors, List<Room> rooms, List<Course> courses)
        {
            var finalPlan = new List<ScheduledLesson>();
            var instructorPrefs = LoadPreferences(instructors);

            var instructorTimeline = instructors.ToDictionary(i => i.Id, _ => new bool[Days.Length, TotalHoursPerDay]);
            var roomTimeline = rooms.ToDictionary(r => r.Id, _ => new bool[Days.Length, TotalHoursPerDay]);
            var groupTimeline = courses.Select(c => c.GroupId).Distinct().ToDictionary(id => id, _ => new bool[Days.Length, TotalHoursPerDay]);

            var sortedCourses = courses.OrderByDescending(c => c.Students).ToList();

            foreach (var course in sortedCourses)
            {
                bool assigned = false;
                var eligibleInstructors = instructors.Where(i => i.Subjects.Contains(course.SubjectId))
                                                     .OrderBy(x => Guid.NewGuid())
                                                     .ToList();
                var eligibleRooms = rooms.Where(r => r.Type == course.RequiredRoomType && r.Capacity >= course.Students).ToList();

                foreach (var instructor in eligibleInstructors)
                {
                    foreach (var room in eligibleRooms)
                    {
                        for (int d = 0; d < Days.Length; d++)
                        {
                            string dayName = Days[d];
                            int duration = course.HoursPerSemester / 15;
                            if (duration < 1) duration = 1;

                            for (int h = 0; h < TotalHoursPerDay - duration + 1; h++)
                            {
                                if (IsSlotFree(d, h, duration, instructor.Id, room.Id, course.GroupId, instructorTimeline, roomTimeline, groupTimeline, dayName, instructorPrefs))
                                {
                                    UpdateTimelines(d, h, duration, instructor.Id, room.Id, course.GroupId, instructorTimeline, roomTimeline, groupTimeline, true);

                                    finalPlan.Add(new ScheduledLesson
                                    {
                                        CourseId = course.Id,
                                        GroupId = course.GroupId,
                                        InstructorId = instructor.Id,
                                        RoomId = room.Id,
                                        Day = dayName,
                                        StartHour = StartDayHour + h,
                                        EndHour = StartDayHour + h + duration
                                    });

                                    assigned = true;
                                    break;
                                }
                            }
                            if (assigned) break;
                        }
                        if (assigned) break;
                    }
                    if (assigned) break;
                }
                if (!assigned)
                {
                    Console.WriteLine($"[ALERT HC] Nie można usunąć kolizji twardych dla: {course.Name}");
                }
            }
            return finalPlan;
        }

        private bool IsSlotFree(int dayIdx, int hourIdx, int duration, string instructorId,
        string roomId, string groupId, Dictionary<string, bool[,]> instTime, Dictionary<string, bool[,]>
        roomTime, Dictionary<string, bool[,]> groupTime, string dayName, Dictionary<string, InstructorPref> prefs)
        {
            if (dayIdx < 0 || dayIdx >= Days.Length) return false;
            if (hourIdx < 0 || hourIdx >= TotalHoursPerDay) return false;
            if (duration <= 0) return false;

            if (!instTime.ContainsKey(instructorId)) return false;
            if (!roomTime.ContainsKey(roomId)) return false;
            if (!groupTime.ContainsKey(groupId)) return false;

            for (int i = 0; i < duration; i++)
            {
                int currentHour = StartDayHour + hourIdx + i;
                if (hourIdx + i >= TotalHoursPerDay) return false;

                if (instTime[instructorId][dayIdx, hourIdx + i]) return false;
                if (roomTime[roomId][dayIdx, hourIdx + i]) return false;
                if (groupTime[groupId][dayIdx, hourIdx + i]) return false;

                if (prefs.TryGetValue(instructorId, out var p) && p.ForbiddenSlots.ContainsKey(dayName))
                {
                    if (p.ForbiddenSlots[dayName].Contains(currentHour)) return false;
                }
            }
            return true;
        }

        private void UpdateTimelines(int dayIdx, int hourIdx, int duration, string instructorId, string roomId,
        string groupId, Dictionary<string, bool[,]> instTime, Dictionary<string, bool[,]> roomTime, Dictionary<string,
        bool[,]> groupTime, bool state)
        {
            if (dayIdx < 0 || dayIdx >= Days.Length) return;
            if (hourIdx < 0 || hourIdx >= TotalHoursPerDay) return;
            if (duration <= 0) return;
            if (!instTime.ContainsKey(instructorId)) return;
            if (!roomTime.ContainsKey(roomId)) return;
            if (!groupTime.ContainsKey(groupId)) return;

            for (int i = 0; i < duration; i++)
            {
                if (hourIdx + i >= TotalHoursPerDay) break;
                instTime[instructorId][dayIdx, hourIdx + i] = state;
                roomTime[roomId][dayIdx, hourIdx + i] = state;
                groupTime[groupId][dayIdx, hourIdx + i] = state;
            }
        }

        public List<ScheduledLesson> OptimizeSoftConstraints(List<ScheduledLesson> initialPlan, List<Instructor> instructors, List<Room> rooms)
        {
            var bestPlan = initialPlan.Select(l => new ScheduledLesson
            {
                CourseId = l.CourseId,
                GroupId = l.GroupId,
                InstructorId = l.InstructorId,
                RoomId = l.RoomId,
                Day = l.Day,
                StartHour = l.StartHour,
                EndHour = l.EndHour
            }).ToList();

            var instructorPrefs = LoadPreferences(instructors);

            var instructorTotalHours = instructors.ToDictionary(i => i.Id, _ => 0);
            var instructorSchedule = instructors.ToDictionary(i => i.Id, _ => {
                var arr = new List<ScheduledLesson>[5];
                for (int d = 0; d < 5; d++) arr[d] = new List<ScheduledLesson>();
                return arr;
            });

            var uniqueCourseIds = bestPlan.Select(l => l.CourseId).Distinct().ToArray();
            var uniqueGroupIds = bestPlan.Select(l => l.GroupId).Distinct().ToArray();

            var courseDays = uniqueCourseIds.ToDictionary(id => id, _ => new HashSet<string>());
            var courseLessonCount = uniqueCourseIds.ToDictionary(id => id, _ => 0);

            var groupSchedule = uniqueGroupIds.ToDictionary(id => id, _ => {
                var arr = new List<ScheduledLesson>[5];
                for (int d = 0; d < 5; d++) arr[d] = new List<ScheduledLesson>();
                return arr;
            });

            var instructorIds = instructors.Select(i => i.Id).ToArray();
            int[] lessonsPerDay = new int[5];

            int bestScore = CalculatePenalty(bestPlan, instructorPrefs, instructorIds, uniqueCourseIds, uniqueGroupIds, instructorTotalHours, instructorSchedule, courseDays, courseLessonCount, groupSchedule, lessonsPerDay);

            Random rnd = new Random();
            int maxIterations = 50000;

            List<double> iterationHistory = new List<double>();
            List<double> scoreHistory = new List<double>();

            for (int i = 0; i < maxIterations; i++)
            {
                if (bestPlan.Count == 0) break;

                int randomLessonIndex = rnd.Next(bestPlan.Count);
                var lesson = bestPlan[randomLessonIndex];

                string oldDay = lesson.Day;
                int oldStartHour = lesson.StartHour;
                int oldEndHour = lesson.EndHour;

                string newDay = Days[rnd.Next(Days.Length)];
                int duration = oldEndHour - oldStartHour;
                int newStartHour = StartDayHour + rnd.Next(TotalHoursPerDay - duration + 1);

                if (oldDay == newDay && oldStartHour == newStartHour) continue;

                lesson.Day = newDay;
                lesson.StartHour = newStartHour;
                lesson.EndHour = newStartHour + duration;

                if (CausesHardConstraintViolation(bestPlan, lesson, newDay, instructorPrefs))
                {
                    lesson.Day = oldDay;
                    lesson.StartHour = oldStartHour;
                    lesson.EndHour = oldEndHour;
                    continue;
                }

                int currentScore = CalculatePenalty(bestPlan, instructorPrefs, instructorIds, uniqueCourseIds, uniqueGroupIds, instructorTotalHours, instructorSchedule, courseDays, courseLessonCount, groupSchedule, lessonsPerDay);

                if (currentScore <= bestScore)
                {
                    bestScore = currentScore;
                }
                else
                {
                    lesson.Day = oldDay;
                    lesson.StartHour = oldStartHour;
                    lesson.EndHour = oldEndHour;
                }

                if (i % 100 == 0)
                {
                    iterationHistory.Add(i);
                    scoreHistory.Add(bestScore);
                }
            }

            Console.WriteLine($"[Optymalizacja SC] Kara początkowa: {CalculatePenalty(initialPlan, instructorPrefs, instructorIds, uniqueCourseIds, uniqueGroupIds, instructorTotalHours, instructorSchedule, courseDays, courseLessonCount, groupSchedule, lessonsPerDay)} | Kara końcowa: {bestScore}");

            double daysSatisfaction = 0.0;
            double hoursSatisfaction = 0.0;

            try
            {
                iterationHistory.Add(maxIterations);
                scoreHistory.Add(bestScore);

                Plot plotZbieznosci = new Plot();
                var scatter = plotZbieznosci.Add.Scatter(iterationHistory.ToArray(), scoreHistory.ToArray());
                scatter.LineWidth = 2;

                plotZbieznosci.Title("Wykres zbieżności algorytmu optymalizacyjnego");
                plotZbieznosci.XLabel("Liczba iteracji");
                plotZbieznosci.YLabel("Wartość funkcji kary (Penalty)");
                plotZbieznosci.SavePng("wykres_zbieznosci.png", 800, 600);
                Console.WriteLine($"[WYKRES] Zapisano plik: wykres_zbieznosci.png");

                Console.WriteLine("[ANALIZA] Generowanie raportów statystycznych...");

                var instructorLoad = bestPlan.GroupBy(l => l.InstructorId)
                    .Select(g => new { Id = g.Key, Hours = g.Sum(l => (l.EndHour != 0 ? l.EndHour : l.StartHour + 1) - l.StartHour) })
                    .OrderByDescending(x => x.Hours).ToList();

                Plot barPlot = new Plot();
                double[] loadValues = instructorLoad.Select(x => (double)x.Hours).ToArray();
                barPlot.Add.Bars(loadValues);
                barPlot.Title("Obciążenie godzinowe prowadzących");
                barPlot.YLabel("Liczba przypisanych godzin");
                barPlot.SavePng("obciazenie_wykladowcow.png", 900, 400);

                var uniqueRooms = bestPlan.Select(l => l.RoomId).Distinct().OrderBy(r => r).ToList();
                int roomCount = uniqueRooms.Count;
                int hourCount = 12;
                double[,] heatMapData = new double[hourCount, roomCount > 0 ? roomCount : 1];

                foreach (var l in bestPlan)
                {
                    int roomIdx = uniqueRooms.IndexOf(l.RoomId);
                    int startIdx = l.StartHour - 8;
                    int duration = (l.EndHour != 0 ? l.EndHour : l.StartHour + 1) - l.StartHour;

                    for (int i = 0; i < duration; i++)
                    {
                        if (startIdx + i < hourCount && roomIdx >= 0)
                        {
                            heatMapData[startIdx + i, roomIdx]++;
                        }
                    }
                }

                Plot heatPlot = new Plot();
                heatPlot.Add.Heatmap(heatMapData);
                heatPlot.Title("Mapa Ciepła Wykorzystania Sal (Im jaśniej, tym częściej zajęta)");
                heatPlot.SavePng("mapa_ciepla.png", 800, 500);

                int totalLessons = bestPlan.Count;
                int badDaysCount = 0;
                int badHoursCount = 0;

                foreach (var l in bestPlan)
                {
                    if (instructorPrefs.TryGetValue(l.InstructorId, out var pref))
                    {
                        if (pref.PreferredDays.Count > 0 && !pref.PreferredDays.Contains(l.Day))
                            badDaysCount++;

                        int endH = l.EndHour != 0 ? l.EndHour : l.StartHour + 1;
                        if (l.StartHour < pref.PreferredHoursStart || endH > pref.PreferredHoursEnd)
                            badHoursCount++;
                    }
                }

                if (totalLessons > 0)
                {
                    daysSatisfaction = 100.0 - ((double)badDaysCount / totalLessons * 100.0);
                    hoursSatisfaction = 100.0 - ((double)badHoursCount / totalLessons * 100.0);
                }

                var jsonOptions = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };

                var exportData = bestPlan.Select(lesson => new
                {
                    DzienTygodnia = lesson.Day,
                    GodzinaRozpoczecia = lesson.StartHour,
                    GodzinaZakonczenia = lesson.EndHour != 0 ? lesson.EndHour : (lesson.StartHour + 1),
                    IdPrzedmiotu = lesson.CourseId,
                    GrupaStudencka = lesson.GroupId,
                    IdProwadzacego = lesson.InstructorId,
                    Sala = lesson.RoomId
                }).ToList();

                string jsonString = JsonSerializer.Serialize(exportData, jsonOptions);
                File.WriteAllText("raport_planu.json", jsonString);
                Console.WriteLine($"[JSON EXPORT] Zapisano raport_planu.json");

                Console.WriteLine("[RAPORT] Generowanie pełnego pliku HTML z analityką...");

                var sb = new System.Text.StringBuilder();
                sb.AppendLine("<!DOCTYPE html>\n<html lang='pl'>\n<head>\n<meta charset='utf-8'>");
                sb.AppendLine("<title>Raport: Plan Zajęć Uczelni</title>");
                sb.AppendLine("<style>");
                sb.AppendLine("body { font-family: 'Segoe UI', Arial, sans-serif; background-color: #f4f7f6; color: #333; margin: 0; padding: 40px; }");
                sb.AppendLine(".container { max-width: 1200px; margin: 0 auto; background: #fff; padding: 30px; border-radius: 10px; box-shadow: 0 4px 15px rgba(0,0,0,0.05); }");
                sb.AppendLine("h1 { color: #2c3e50; border-bottom: 2px solid #3498db; padding-bottom: 10px; margin-bottom: 30px; }");
                sb.AppendLine("h2 { color: #34495e; margin-top: 40px; }");
                sb.AppendLine(".chart-container { text-align: center; margin-bottom: 40px; padding: 20px; background: #fafafa; border-radius: 8px; border: 1px dashed #ccc; }");
                sb.AppendLine("img { max-width: 100%; height: auto; border-radius: 5px; box-shadow: 0 2px 10px rgba(0,0,0,0.1); }");
                sb.AppendLine("table { border-collapse: collapse; width: 100%; margin-top: 20px; font-size: 14px; background: #fff; }");
                sb.AppendLine("th, td { border: 1px solid #ddd; padding: 12px; text-align: left; }");
                sb.AppendLine("th { background-color: #3498db; color: white; font-weight: 600; }");
                sb.AppendLine("tr:nth-child(even) { background-color: #f9f9f9; }");
                sb.AppendLine(".stats-card { display: flex; justify-content: space-around; background: #fff; padding: 20px; border-radius: 8px; margin-bottom: 20px; border: 1px solid #e0e0e0; }");
                sb.AppendLine("</style>\n</head>\n<body>\n<div class='container'>");
                sb.AppendLine("<h1>Dashboard Analityczny: Plan Zajęć</h1>");

                sb.AppendLine("<h2>1. Zbieżność Algorytmu (Hill Climbing)</h2>");
                sb.AppendLine("<div class='chart-container'><img src='wykres_zbieznosci.png' alt='Wykres zbieżności algorytmu'></div>");

                sb.AppendLine("<h2>2. Statystyki i Zaspokojenie Ograniczeń (Soft Constraints)</h2>");
                sb.AppendLine("<div class='stats-card'>");
                sb.AppendLine($"<div style='text-align: center;'><h3 style='color: #27ae60;'>Zaspokojenie preferencji DNI</h3><p style='font-size: 28px; font-weight: bold; margin: 0;'>{daysSatisfaction:F2}%</p></div>");
                sb.AppendLine($"<div style='text-align: center;'><h3 style='color: #2980b9;'>Zaspokojenie preferencji GODZIN</h3><p style='font-size: 28px; font-weight: bold; margin: 0;'>{hoursSatisfaction:F2}%</p></div>");
                sb.AppendLine("</div>");

                sb.AppendLine("<h2>3. Analiza Obciążenia i Logistyki Sal</h2>");
                sb.AppendLine("<div class='chart-container'><h3>Obciążenie Prowadzących</h3><img src='obciazenie_wykladowcow.png' alt='Obciążenie'></div>");
                sb.AppendLine("<div class='chart-container'><h3>Mapa Ciepła Wykorzystania Sal</h3><img src='mapa_ciepla.png' alt='Mapa'></div>");

                sb.AppendLine("<h2>4. Szczegółowy Harmonogram Zajęć</h2>");
                sb.AppendLine("<table><thead><tr><th>Dzień</th><th>Godzina</th><th>Przedmiot</th><th>Grupa</th><th>Prowadzący</th><th>Sala</th></tr></thead><tbody>");

                var daysOrder = new List<string> { "Mon", "Tue", "Wed", "Thu", "Fri" };
                var sortedPlan = bestPlan.OrderBy(l => daysOrder.IndexOf(l.Day)).ThenBy(l => l.StartHour).ToList();

                foreach (var lesson in sortedPlan)
                {
                    string dayPl = lesson.Day switch { "Mon" => "Poniedziałek", "Tue" => "Wtorek", "Wed" => "Środa", "Thu" => "Czwartek", "Fri" => "Piątek", _ => lesson.Day };
                    int endH = lesson.EndHour != 0 ? lesson.EndHour : (lesson.StartHour + 1);

                    sb.AppendLine($"<tr><td>{dayPl}</td><td><b>{lesson.StartHour}:00 - {endH}:00</b></td><td>{lesson.CourseId}</td><td>{lesson.GroupId}</td><td>{lesson.InstructorId}</td><td>{lesson.RoomId}</td></tr>");
                }

                sb.AppendLine("</tbody></table></div></body></html>");
                File.WriteAllText("raport_planu.html", sb.ToString());
                Console.WriteLine($"[RAPORT] Zapisano plik: raport_planu.html");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[BŁĄD GENEROWANIA RAPORTÓW] {ex.Message}");
            }

            return bestPlan;
        }

        private bool CausesHardConstraintViolation(List<ScheduledLesson> plan, ScheduledLesson movedLesson, string targetDay, Dictionary<string, InstructorPref> instructorPrefs)
        {
            if (instructorPrefs.TryGetValue(movedLesson.InstructorId, out var pref) && pref.ForbiddenSlots != null)
            {
                if (pref.ForbiddenSlots.TryGetValue(targetDay, out var forbiddenHours))
                {
                    for (int hour = movedLesson.StartHour; hour < movedLesson.EndHour; hour++)
                    {
                        if (forbiddenHours.Contains(hour)) return true;
                    }
                }
            }

            foreach (var other in plan)
            {
                if (other == movedLesson) continue;
                if (other.Day != targetDay) continue;

                bool hoursOverlap = movedLesson.StartHour < other.EndHour && movedLesson.EndHour > other.StartHour;
                if (hoursOverlap)
                {
                    if (other.InstructorId == movedLesson.InstructorId) return true;
                    if (other.RoomId == movedLesson.RoomId) return true;
                    if (other.GroupId == movedLesson.GroupId) return true;
                }
            }
            return false;
        }

        private int CalculatePenalty(
            List<ScheduledLesson> plan,
            Dictionary<string, InstructorPref> instructorPrefs,
            string[] instructorIds,
            string[] courseIds,
            string[] groupIds,
            Dictionary<string, int> instructorTotalHours,
            Dictionary<string, List<ScheduledLesson>[]> instructorSchedule,
            Dictionary<string, HashSet<string>> courseDays,
            Dictionary<string, int> courseLessonCount,
            Dictionary<string, List<ScheduledLesson>[]> groupSchedule,
            int[] lessonsPerDay)
        {
            int penalty = 0;

            for (int i = 0; i < instructorIds.Length; i++)
            {
                instructorTotalHours[instructorIds[i]] = 0;
                var schedule = instructorSchedule[instructorIds[i]];
                for (int d = 0; d < 5; d++) schedule[d].Clear();
            }
            for (int i = 0; i < courseIds.Length; i++)
            {
                courseDays[courseIds[i]].Clear();
                courseLessonCount[courseIds[i]] = 0;
            }
            for (int i = 0; i < groupIds.Length; i++)
            {
                var gSched = groupSchedule[groupIds[i]];
                for (int d = 0; d < 5; d++) gSched[d].Clear();
            }
            Array.Clear(lessonsPerDay, 0, 5);

            foreach (var l in plan)
            {
                int dayIdx = GetDayIndex(l.Day);
                if (dayIdx == -1) continue;

                lessonsPerDay[dayIdx]++;
                instructorTotalHours[l.InstructorId] += (l.EndHour - l.StartHour);
                instructorSchedule[l.InstructorId][dayIdx].Add(l);

                courseDays[l.CourseId].Add(l.Day);
                courseLessonCount[l.CourseId]++;
                groupSchedule[l.GroupId][dayIdx].Add(l);

                if (instructorPrefs.TryGetValue(l.InstructorId, out var pref))
                {
                    if (pref.PreferredDays.Count > 0 && !pref.PreferredDays.Contains(l.Day))
                    {
                        penalty += 100;
                    }
                    if (l.StartHour < pref.PreferredHoursStart || l.EndHour > pref.PreferredHoursEnd)
                    {
                        penalty += 100;
                    }
                    if (l.StartHour < pref.MinStartHour)
                    {
                        penalty += 100;
                    }
                }
            }

            for (int i = 0; i < instructorIds.Length; i++)
            {
                string instId = instructorIds[i];
                int hours = instructorTotalHours[instId];
                if (instructorPrefs.TryGetValue(instId, out var pref) && hours > pref.MaxHoursPerWeek)
                {
                    penalty += (hours - pref.MaxHoursPerWeek) * 100;
                }
            }

            for (int i = 0; i < instructorIds.Length; i++)
            {
                var daysArray = instructorSchedule[instructorIds[i]];
                for (int d = 0; d < 5; d++)
                {
                    List<ScheduledLesson> dayLessons = daysArray[d];
                    if (dayLessons.Count < 2) continue;

                    dayLessons.Sort((x, y) => x.StartHour.CompareTo(y.StartHour));

                    for (int j = 0; j < dayLessons.Count - 1; j++)
                    {
                        int gap = dayLessons[j + 1].StartHour - dayLessons[j].EndHour;
                        if (gap > 0) penalty += gap * 30;
                    }
                }
            }

            for (int i = 0; i < courseIds.Length; i++)
            {
                string cId = courseIds[i];
                if (courseLessonCount[cId] > 1 && courseDays[cId].Count == 1)
                {
                    penalty += 30;
                }
            }

            for (int i = 0; i < groupIds.Length; i++)
            {
                var daysArray = groupSchedule[groupIds[i]];
                for (int d = 0; d < 5; d++)
                {
                    List<ScheduledLesson> dayLessons = daysArray[d];
                    if (dayLessons.Count < 2) continue;

                    dayLessons.Sort((x, y) => x.StartHour.CompareTo(y.StartHour));

                    for (int j = 0; j < dayLessons.Count - 1; j++)
                    {
                        if (dayLessons[j].EndHour == dayLessons[j + 1].StartHour)
                        {
                            if (dayLessons[j].RoomId != dayLessons[j + 1].RoomId)
                            {
                                penalty += 10;
                            }
                        }
                    }
                }
            }

            int maxLessons = 0;
            int minLessons = int.MaxValue;
            for (int d = 0; d < 5; d++)
            {
                if (lessonsPerDay[d] > maxLessons) maxLessons = lessonsPerDay[d];
                if (lessonsPerDay[d] < minLessons) minLessons = lessonsPerDay[d];
            }
            if (minLessons != int.MaxValue)
            {
                penalty += (maxLessons - minLessons) * 5;
            }

            return penalty;
        }

        private int GetDayIndex(string day)
        {
            switch (day)
            {
                case "Mon": return 0;
                case "Tue": return 1;
                case "Wed": return 2;
                case "Thu": return 3;
                case "Fri": return 4;
                default: return -1;
            }
        }
    }
}