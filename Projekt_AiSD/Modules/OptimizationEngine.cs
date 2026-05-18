using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Projekt_AiSD.Models;
namespace Projekt_AiSD.Modules
{
    public class ScheduledLesson
    {
        public string CourseId { get; set; }
        public string InstructorId { get; set; }
        public string RoomId { get; set; }
        public string Day { get; set; }
        public int StartHour { get; set; }
        public int EndHour { get; set; } // StartHour + HoursPerWeek
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

        public List<ScheduledLesson> OptimizeSoftConstraints(List<ScheduledLesson> initialPlan, List<Instructor> instructors)
        {
            // Robimy bezpieczną kopię planu początkowego, żeby go nie nadpisać
            var bestPlan = new List<ScheduledLesson>(initialPlan);
            int bestScore = CalculatePenalty(bestPlan, instructors);

            Random rnd = new Random();
            int maxIterations = 3000; // Liczba prób optymalizacji

            for (int i = 0; i < maxIterations; i++)
            {
                // Tworzymy roboczą kopię planu do przetestowania zmiany
                var currentPlan = new List<ScheduledLesson>(bestPlan);

                if (currentPlan.Count == 0) break;

                // Losujemy jedne zajęcia i próbujemy zmienić im dzień tygodnia
                int randomLessonIndex = rnd.Next(currentPlan.Count);
                var lessonToMove = currentPlan[randomLessonIndex];

                string oldDay = lessonToMove.Day;
                string newDay = Days[rnd.Next(Days.Length)];

                if (oldDay == newDay) continue;

                lessonToMove.Day = newDay;

                // Liczymy punkty karne dla nowego układu
                int currentScore = CalculatePenalty(currentPlan, instructors);

                // Jeśli nowy układ jest LEPSZY (ma mniej punktów karnych), zapisujemy go
                if (currentScore < bestScore)
                {
                    bestPlan = currentPlan;
                    bestScore = currentScore;
                }
            }

            Console.WriteLine($"[Optymalizacja] Wynik startowy: {CalculatePenalty(initialPlan, instructors)} | Wynik końcowy: {bestScore}");
            return bestPlan;
        }

        /// <summary>
        /// Funkcja Celu (Fitting Function) - zlicza naruszenia ograniczeń miękkich zgodnie z instrukcją projektu.
        /// </summary>
        private int CalculatePenalty(List<ScheduledLesson> plan, List<Instructor> instructors)
        {
            int penalty = 0;

            // SC-6: Maksymalne obciążenie tygodniowe prowadzącego (Waga: Wysoka)
            var instructorHours = plan.GroupBy(p => p.InstructorId)
                                      .ToDictionary(g => g.Key, g => g.Sum(l => l.EndHour - l.StartHour));

            foreach (var instructor in instructors)
            {
                if (instructorHours.ContainsKey(instructor.Id) && instructorHours[instructor.Id] > instructor.MaxHoursPerWeek)
                {
                    penalty += 100; // Duża kara za przeciążenie pracownika [cite: 93]
                }
            }

            // SC-2: Minimalizacja okienek dla prowadzących w ciągu dnia (Waga: Średnia)
            var scheduleByInstructorAndDay = plan.GroupBy(p => new { p.InstructorId, p.Day });
            foreach (var dailySchedule in scheduleByInstructorAndDay)
            {
                var sortedLessons = dailySchedule.OrderBy(l => l.StartHour).ToList();
                for (int i = 0; i < sortedLessons.Count - 1; i++)
                {
                    int gap = sortedLessons[i + 1].StartHour - sortedLessons[i].EndHour;
                    if (gap > 0)
                    {
                        penalty += gap * 20; // Punkty karne za każdą godzinę "okienka" [cite: 87]
                    }
                }
            }

            // SC-3: Grupowanie zajęć - wykład i lab z tego samego przedmiotu w RÓŻNE dni (Waga: Średnia)
            var courseGroups = plan.GroupBy(p => p.CourseId);
            foreach (var group in courseGroups)
            {
                var daysUsed = group.Select(l => l.Day).Distinct().Count();
                if (group.Count() > 1 && daysUsed == 1)
                {
                    penalty += 30; // Kara za upchnięcie wszystkiego z jednego przedmiotu w jeden dzień [cite: 89]
                }
            }

            // SC-4: Równomierne rozłożenie zajęć na cały tydzień (Waga: Niska)
            var lessonsPerDay = plan.GroupBy(p => p.Day).Select(g => g.Count()).ToList();
            if (lessonsPerDay.Count > 0)
            {
                int maxLessons = lessonsPerDay.Max();
                int minLessons = lessonsPerDay.Min();
                penalty += (maxLessons - minLessons) * 5; // Mała kara za dysproporcję między dniami [cite: 90]
            }

            return penalty;
        }

    }
}
