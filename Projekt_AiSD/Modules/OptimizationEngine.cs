using Projekt_AiSD.Models;
using ScottPlot;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text.Json;

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
            try
            {
                iterationHistory.Add(maxIterations);
                scoreHistory.Add(bestScore);

                Plot myPlot = new Plot();
                var scatter = myPlot.Add.Scatter(iterationHistory.ToArray(), scoreHistory.ToArray());
                scatter.LineWidth = 2;
                scatter.Color = Colors.Blue;

                myPlot.Title("Wykres zbieżności algorytmu optymalizacyjnego");
                myPlot.XLabel("Liczba iteracji");
                myPlot.YLabel("Wartość funkcji kary (Penalty)");

                string plotPath = "wykres_zbieznosci.png";
                myPlot.SavePng(plotPath, 800, 600);

                Console.WriteLine($"[WYKRES] Zapisano plik wykresu pod ścieżką: {plotPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WYKRES BŁĄD] Nie udało się wygenerować wykresu: {ex.Message}");
            }

            Console.WriteLine($"[Optymalizacja SC] Kara początkowa: {CalculatePenalty(initialPlan, instructorPrefs, instructorIds, uniqueCourseIds, uniqueGroupIds, instructorTotalHours, instructorSchedule, courseDays, courseLessonCount, groupSchedule, lessonsPerDay)} | Kara końcowa: {bestScore}");

            try
            {
        
                Plot myPlot = new Plot();

                var scatter = myPlot.Add.Scatter(iterationHistory.ToArray(), scoreHistory.ToArray());

                scatter.LineWidth = 2;

                myPlot.Title("Wykres zbieżności algorytmu optymalizacyjnego");
                myPlot.XLabel("Liczba iteracji");
                myPlot.YLabel("Wartość funkcji kary (Penalty)");

                string plotPath = "wykres_zbieznosci.png";
                myPlot.SavePng(plotPath, 800, 600);

                Console.WriteLine($"[WYKRES] Zapisano plik wykresu pod ścieżką: {plotPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WYKRES BŁĄD] Nie udało się wygenerować wykresu: {ex.Message}");
            }

            try
            {
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
                string jsonPath = "raport_planu.json";

                File.WriteAllText(jsonPath, jsonString);

                Console.WriteLine($"[JSON EXPORT] Sukces! Zapisano surowy plan zajęć pod ścieżką: {jsonPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[JSON ERROR] Krytyczny błąd podczas eksportu planu: {ex.Message}");
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