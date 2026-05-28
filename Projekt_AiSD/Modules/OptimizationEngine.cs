using Projekt_AiSD.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Projekt_AiSD.Modules
{
    internal class OptimizationEngine
    {
    }


    public class OptimizationEngine
    {
        private static readonly string[] Days = { "Mon", "Tue", "Wed", "Thu", "Fri" };
        private const int StartDayHour = 8;
        private const int EndDayHour = 20;
        private const int TotalHoursPerDay = EndDayHour - StartDayHour;


        /// <summary>
        /// plan spełniający ograniczenia twarde.
        /// </summary>

        public List<ScheduledLesson> RunOptimization(List<Instructor> instructors, List<Room> rooms, List<Course> courses)
        {
            var finalPlan = new List<ScheduledLesson>();

            //szybkie macierze
            var instructorTimeline = instructors.ToDictionary(i => i.Id, _ => new bool[Days.Length, TotalHoursPerDay]);
            var roomTimeline = rooms.ToDictionary(r => r.Id, _ => new bool[Days.Length, TotalHoursPerDay]);
            var groupTimeline = courses.ToDictionary(c => c.Id, _ => new bool[Days.Length, TotalHoursPerDay]);

            //sortowanie od najwiekszej liczby studentow (najtrudniej dopasowac sale)
            var sortedCourses = courses.OrderByDescending(c => c.Students).ToList();


            foreach (var course in sortedCourses)
            {
                bool assigned = false;

                //HC-8: filtrowanie prowadzacych z odpowiednimi kompetencjami
                var eligibleInstructors = instructors.Where(i => i.Subjects.Contains(course.Id)).ToList();

                //HC-6 i HC-7: filtrowanie sal (zgodnosc typu i pojemnosci)
                var eligibleRooms = rooms.Where(r => r.Type == course.RequiredRoomType && r.Capacity >= course.Students).ToList();

                foreach (var instructor in eligibleInstructors)
                {
                    foreach (var room in eligibleRooms)
                    {
                        for (int d = 0; d < Days.Length; d++)
                        {
                            string dayName = Days[d];
                            int duration = course.HoursPerWeek;

                            for (int h = 0; h <= TotalHoursPerDay - duration; h++)
                            {
                                // sprawdzenie weryfikatora ograniczen twardych
                                if (IsSlotFree(d, h, duration, instructor.Id, room.Id, course.Id, instructorTimeline, roomTimeline, groupTimeline, room, dayName))
                                {
                                    // rezerwacja miejsca w pamieci ( zaznaczamy jako zajete)
                                    UpdateTimelines(d, h, duration, instructor.Id, room.Id, course.Id, instructorTimeline, roomTimeline, groupTimeline, true);


                                    finalPlan.Add(new ScheduledLesson
                                    {
                                        CourseId = course.Id,
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


        // WERYFIKATOR OGRANICZEN TWARDYCH

        private bool IsSlotFree(int dayIdx, int hourIdx, int duration, string instructorId,
        string roomId, string courseId, Dictionary<string, bool[,]> instTime, Dictionary<string, bool[,]>
        roomTime, Dictionary<string, bool[,]> groupTime, Room room, string dayName)
        {
            for (int i = 0; i < duration; i++)
            {
                int currentHour = StartDayHour + hourIdx + i;

                if (instTime[instructorId][dayIdx, hourIdx + i]) return false; //HC-1 brak kolizji prowadzacego
                if (roomTime[roomId][dayIdx, hourIdx + i]) return false; //HC-2 brak kolizji sali
                if (groupTime[courseId][dayIdx, hourIdx + i]) return false; //HC-3 brak kolizji grupy

                //HC-5: dostepnosc godzinowa sali z pliku JSON
                if (!room.Availability.ContainsKey(dayName) || !room.Availability[dayName].Contains(currentHour))
                    return false;

                //HC-4: dostepnosc prowadzacego ( z modulu LLM - "forbidden_slots")


                //^^^wyzej wkleic strukture od danych/llm
            }
            return true;
        }

        private void UpdateTimelines(int dayIdx, int hourIdx, int duration, string instructorId, string roomId,
        string courseId, Dictionary<string, bool[,]> instTime, Dictionary<string, bool[,]> roomTime, Dictionary<string,
        bool[,]> groupTime, bool state)
        {
            for (int i = 0; i < duration; i++)
            {
                instTime[instructorId][dayIdx, hourIdx + i] = state;
                roomTime[roomId][dayIdx, hourIdx + i] = state;
                groupTime[courseId][dayIdx, hourIdx + i] = state;
            }
        }

        //ograniczenia miekkie

        // Pomocnicza klasa cache'ująca strukturę prowadzących bez ciągłej refleksji
        private class InstructorPref
        {
            public HashSet<string> PreferredDays { get; set; } = new HashSet<string>();
            public int PreferredHoursStart { get; set; } = 8;
            public int PreferredHoursEnd { get; set; } = 20;
            public int MinStartHour { get; set; } = 8;
            public int MaxHoursPerWeek { get; set; } = 16;
            public Dictionary<string, HashSet<int>> ForbiddenSlots { get; set; } = new Dictionary<string, HashSet<int>>();
        }

        /// <summary>
        /// Optymalizuje ograniczenia miękkie bez naruszania ograniczeń twardych Marysi.
        /// </summary>
        public List<ScheduledLesson> OptimizeSoftConstraints(List<ScheduledLesson> initialPlan, List<Instructor> instructors, List<Room> rooms)
        {
            var bestPlan = initialPlan.Select(l => new ScheduledLesson
            {
                CourseId = l.CourseId,
                InstructorId = l.InstructorId,
                RoomId = l.RoomId,
                Day = l.Day,
                StartHour = l.StartHour,
                EndHour = l.EndHour
            }).ToList();

            // 1. CACHOWANIE REFLEKSJI (Wchodzimy do klasy Preferences i obsługujemy literówki)
            var instructorPrefs = new Dictionary<string, InstructorPref>();
            var firstInst = instructors.FirstOrDefault();

            if (firstInst != null)
            {
                var type = firstInst.GetType();
                var preferencesProp = type.GetProperty("Preferences") ?? type.GetProperty("preferences");

                foreach (var inst in instructors)
                {
                    var pref = new InstructorPref();
                    object prefsObj = preferencesProp?.GetValue(inst);

                    if (prefsObj != null)
                    {
                        var prefType = prefsObj.GetType();

                        // Obsługa potencjalnych literówek z "Preffered" (podwójne f)
                        var prefDaysProp = prefType.GetProperty("PreferredDays") ?? prefType.GetProperty("PrefferedDays");
                        var prefHoursStartProp = prefType.GetProperty("PreferredHoursStart") ?? prefType.GetProperty("PrefferedHoursStart");
                        var prefHoursEndProp = prefType.GetProperty("PreferredHoursEnd") ?? prefType.GetProperty("PrefferedHoursEnd");
                        var minStartHourProp = prefType.GetProperty("MinStartHour") ?? prefType.GetProperty("min_start_hour");
                        var maxHoursProp = prefType.GetProperty("MaxHoursPerWeek") ?? prefType.GetProperty("max_hours_per_week");

                        var forbiddenSlotsProp = prefType.GetProperty("ForbiddenSlots") ?? type.GetProperty("ForbiddenSlots");

                        if (prefDaysProp != null && prefDaysProp.GetValue(prefsObj) is IEnumerable<string> days)
                            pref.PreferredDays = new HashSet<string>(days);

                        pref.PreferredHoursStart = (prefHoursStartProp?.GetValue(prefsObj) as int?) ?? 8;
                        pref.PreferredHoursEnd = (prefHoursEndProp?.GetValue(prefsObj) as int?) ?? 20;
                        pref.MinStartHour = (minStartHourProp?.GetValue(prefsObj) as int?) ?? 8;
                        pref.MaxHoursPerWeek = (maxHoursProp?.GetValue(prefsObj) as int?) ?? 16;

                        if (forbiddenSlotsProp != null)
                        {
                            object fSlotsSource = prefType.GetProperty("ForbiddenSlots") != null ? prefsObj : inst;

                            if (forbiddenSlotsProp.GetValue(fSlotsSource) is Dictionary<string, List<int>> fList)
                                pref.ForbiddenSlots = fList.ToDictionary(p => p.Key, p => new HashSet<int>(p.Value));
                            else if (forbiddenSlotsProp.GetValue(fSlotsSource) is Dictionary<string, HashSet<int>> fHash)
                                pref.ForbiddenSlots = fHash;
                        }
                    }

                    instructorPrefs[inst.Id] = pref;
                }
            }

            // 2. PREALOKACJA BUFORÓW (Zapobiega obciążeniu Garbage Collectora w pętli)
            var instructorTotalHours = instructors.ToDictionary(i => i.Id, _ => 0);
            var instructorSchedule = instructors.ToDictionary(i => i.Id, _ => {
                var arr = new List<ScheduledLesson>[5];
                for (int d = 0; d < 5; d++) arr[d] = new List<ScheduledLesson>();
                return arr;
            });

            var uniqueCourseIds = bestPlan.Select(l => l.CourseId).Distinct().ToArray();
            var courseDays = uniqueCourseIds.ToDictionary(id => id, _ => new HashSet<string>());
            var courseLessonCount = uniqueCourseIds.ToDictionary(id => id, _ => 0);
            var groupSchedule = uniqueCourseIds.ToDictionary(id => id, _ => {
                var arr = new List<ScheduledLesson>[5];
                for (int d = 0; d < 5; d++) arr[d] = new List<ScheduledLesson>();
                return arr;
            });

            var instructorIds = instructors.Select(i => i.Id).ToArray();
            int[] lessonsPerDay = new int[5];

            // Obliczenie kary początkowej
            int bestScore = CalculatePenalty(bestPlan, instructorPrefs, instructorIds, uniqueCourseIds, instructorTotalHours, instructorSchedule, courseDays, courseLessonCount, groupSchedule, lessonsPerDay);

            Random rnd = new Random();
            int maxIterations = 3000;

            for (int i = 0; i < maxIterations; i++)
            {
                if (bestPlan.Count == 0) break;

                int randomLessonIndex = rnd.Next(bestPlan.Count);
                var lesson = bestPlan[randomLessonIndex];

                string oldDay = lesson.Day;
                string newDay = Days[rnd.Next(Days.Length)];

                if (oldDay == newDay) continue;

                // Weryfikacja twardych ograniczeń
                if (CausesHardConstraintViolation(bestPlan, lesson, newDay, rooms, instructorPrefs))
                {
                    continue;
                }

                lesson.Day = newDay;

                int currentScore = CalculatePenalty(bestPlan, instructorPrefs, instructorIds, uniqueCourseIds, instructorTotalHours, instructorSchedule, courseDays, courseLessonCount, groupSchedule, lessonsPerDay);

                if (currentScore < bestScore)
                {
                    bestScore = currentScore; // Zmiana na plus, akceptujemy
                }
                else
                {
                    lesson.Day = oldDay; // Zmiana gorsza, cofamy (Rollback)
                }
            }

            Console.WriteLine($"[Optymalizacja SC] Kara początkowa: {CalculatePenalty(initialPlan, instructorPrefs, instructorIds, uniqueCourseIds, instructorTotalHours, instructorSchedule, courseDays, courseLessonCount, groupSchedule, lessonsPerDay)} | Kara końcowa: {bestScore}");
            return bestPlan;
        }

        /// <summary>
        /// Szybka weryfikacja O(N) uwzględniająca zarówno nakładanie się, jak i zakazy godzinowe LLM (HC-4).
        /// </summary>
        private bool CausesHardConstraintViolation(List<ScheduledLesson> plan, ScheduledLesson movedLesson, string targetDay, List<Room> rooms, Dictionary<string, InstructorPref> instructorPrefs)
        {
            // HC-4: Ochrona dostępności godzinowej prowadzącego przekazanej z modułu LLM
            if (instructorPrefs.TryGetValue(movedLesson.InstructorId, out var pref) && pref.ForbiddenSlots != null)
            {
                if (pref.ForbiddenSlots.TryGetValue(targetDay, out var forbiddenHours))
                {
                    for (int hour = movedLesson.StartHour; hour < movedLesson.EndHour; hour++)
                    {
                        if (forbiddenHours.Contains(hour)) return true; // Trafiono w zabroniony slot z LLM!
                    }
                }
            }

            // HC-5: Ochrona dostępności sal
            var targetRoom = rooms.FirstOrDefault(r => r.Id == movedLesson.RoomId);
            if (targetRoom != null)
            {
                for (int hour = movedLesson.StartHour; hour < movedLesson.EndHour; hour++)
                {
                    if (!targetRoom.Availability.ContainsKey(targetDay) || !targetRoom.Availability[targetDay].Contains(hour))
                    {
                        return true;
                    }
                }
            }

            // HC-1, HC-2, HC-3: Ochrona przed nakładaniem się zajęć
            foreach (var other in plan)
            {
                if (other == movedLesson) continue;
                if (other.Day != targetDay) continue;

                bool hoursOverlap = movedLesson.StartHour < other.EndHour && movedLesson.EndHour > other.StartHour;
                if (hoursOverlap)
                {
                    if (other.InstructorId == movedLesson.InstructorId) return true;
                    if (other.RoomId == movedLesson.RoomId) return true;
                    if (other.CourseId == movedLesson.CourseId) return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Funkcja celu czyszcząca i ponownie wykorzystująca gotowe struktury (Zero-Allocation).
        /// </summary>
        private int CalculatePenalty(
            List<ScheduledLesson> plan,
            Dictionary<string, InstructorPref> instructorPrefs,
            string[] instructorIds,
            string[] courseIds,
            Dictionary<string, int> instructorTotalHours,
            Dictionary<string, List<ScheduledLesson>[]> instructorSchedule,
            Dictionary<string, HashSet<string>> courseDays,
            Dictionary<string, int> courseLessonCount,
            Dictionary<string, List<ScheduledLesson>[]> groupSchedule,
            int[] lessonsPerDay)
        {
            int penalty = 0;

            // Szybkie czyszczenie prealokowanych kontenerów (Zamiast "new" w pętli)
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
                var gSched = groupSchedule[courseIds[i]];
                for (int d = 0; d < 5; d++) gSched[d].Clear();
            }
            Array.Clear(lessonsPerDay, 0, 5);

            // Jeden liniowy przebieg agregujący dane z aktualnego stanu planu
            foreach (var l in plan)
            {
                int dayIdx = GetDayIndex(l.Day);
                if (dayIdx == -1) continue;

                lessonsPerDay[dayIdx]++;
                instructorTotalHours[l.InstructorId] += (l.EndHour - l.StartHour);
                instructorSchedule[l.InstructorId][dayIdx].Add(l);

                courseDays[l.CourseId].Add(l.Day);
                courseLessonCount[l.CourseId]++;
                groupSchedule[l.CourseId][dayIdx].Add(l);

                // SC-1: Preferencje prowadzących (WAGA: 100)
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

            // SC-6: Maksymalne obciążenie tygodniowe prowadzącego (WAGA: 100 za każdą nadgodzinę)
            for (int i = 0; i < instructorIds.Length; i++)
            {
                string instId = instructorIds[i];
                int hours = instructorTotalHours[instId];
                if (instructorPrefs.TryGetValue(instId, out var pref) && hours > pref.MaxHoursPerWeek)
                {
                    penalty += (hours - pref.MaxHoursPerWeek) * 100;
                }
            }

            // SC-2: Minimalizacja okienek dla prowadzących (WAGA: 30 za godzinę przerwy)
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

            // SC-3: Wykład i lab tego samego przedmiotu w różne dni (WAGA: 30)
            for (int i = 0; i < courseIds.Length; i++)
            {
                string cId = courseIds[i];
                if (courseLessonCount[cId] > 1 && courseDays[cId].Count == 1)
                {
                    penalty += 30;
                }
            }

            // SC-5: Przemieszczanie się studentów - zajęcia POD RZĄD w różnych salach (WAGA: 10)
            for (int i = 0; i < courseIds.Length; i++)
            {
                var daysArray = groupSchedule[courseIds[i]];
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

            // SC-4: Równomierne rozłożenie obciążenia na dni tygodnia (WAGA: 5)
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

        /// <summary>
        /// Mapuje skrót nazwy dnia tygodnia na indeks tablicy (0-4).
        /// </summary>
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
    } // Zamyka klasę OptimizationEngine
} // Zamyka namespace Projekt_AiSD.Modules
