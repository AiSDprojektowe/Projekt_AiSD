using Projekt_AiSD.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Projekt_AiSD.Modules
{
    public class ScheduledLesson
    {
        public string CourseId { get; set; }
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

        // Pomocnicza klasa cache'ująca strukturę
        private class InstructorPref
        {
            public HashSet<string> PreferredDays { get; set; } = new HashSet<string>();
            public int PreferredHoursStart { get; set; } = 8;
            public int PreferredHoursEnd { get; set; } = 20;
            public int MinStartHour { get; set; } = 8;
            public int MaxHoursPerWeek { get; set; } = 16;
            public Dictionary<string, HashSet<int>> ForbiddenSlots { get; set; } = new Dictionary<string, HashSet<int>>();
        }


        private Dictionary<string, InstructorPref> LoadPreferences(List<Instructor> instructors)
        {
            var instructorPrefs = new Dictionary<string, InstructorPref>();
            var firstInst = instructors.FirstOrDefault();

            if (firstInst != null)
            {
                var type = firstInst.GetType();

                var preferencesProp = type.GetProperty("ParsedPreferences") ?? type.GetProperty("Preferences") ?? type.GetProperty("preferences");

                foreach (var inst in instructors)
                {
                    var pref = new InstructorPref();

                    if (preferencesProp != null)
                    {
                        object prefsObj = preferencesProp.GetValue(inst);
                        if (prefsObj != null)
                        {
                            var prefType = prefsObj.GetType();

                            var prefDaysProp = prefType.GetProperty("PreferredDays") ?? prefType.GetProperty("PrefferedDays");
                            var prefHoursStartProp = prefType.GetProperty("PreferredHoursStart") ?? prefType.GetProperty("PreferredHoursStart");
                            var prefHoursEndProp = prefType.GetProperty("PreferredHoursEnd") ?? prefType.GetProperty("PreferredHoursEnd");
                            var minStartHourProp = prefType.GetProperty("MinStartHour") ?? prefType.GetProperty("min_start_hour");
                            var maxHoursProp = prefType.GetProperty("MaxHoursPerWeek") ?? prefType.GetProperty("max_hours_per_week");
                            var forbiddenSlotsProp = prefType.GetProperty("ForbiddenSlots") ?? type.GetProperty("ForbiddenSlots");

                            if (prefDaysProp != null && prefDaysProp.GetValue(prefsObj) is IEnumerable<string> days)
                                pref.PreferredDays = new HashSet<string>(days.Select(MapDayToShort));

                            pref.PreferredHoursStart = (prefHoursStartProp?.GetValue(prefsObj) as int?) ?? 8;
                            pref.PreferredHoursEnd = (prefHoursEndProp?.GetValue(prefsObj) as int?) ?? 20;
                            pref.MinStartHour = (minStartHourProp?.GetValue(prefsObj) as int?) ?? 8;
                            pref.MaxHoursPerWeek = (maxHoursProp?.GetValue(prefsObj) as int?) ?? 16;

                            if (forbiddenSlotsProp != null)
                            {
                                object fSlotsSource = prefType.GetProperty("ForbiddenSlots") != null ? prefsObj : inst;

                                if (forbiddenSlotsProp.GetValue(fSlotsSource) is Dictionary<string, List<int>> fList)
                                    pref.ForbiddenSlots = fList.ToDictionary(p => MapDayToShort(p.Key), p => new HashSet<int>(p.Value));
                                else if (forbiddenSlotsProp.GetValue(fSlotsSource) is Dictionary<string, HashSet<int>> fHash)
                                    pref.ForbiddenSlots = fHash.ToDictionary(p => MapDayToShort(p.Key), p => p.Value);
                            }
                        }
                    }
                    instructorPrefs[inst.Id] = pref;
                }
            }
            return instructorPrefs;
        }

        public List<ScheduledLesson> RunOptimization(List<Instructor> instructors, List<Room> rooms, List<Course> courses)
        {
            var finalPlan = new List<ScheduledLesson>();


            var instructorPrefs = LoadPreferences(instructors);

            var instructorTimeline = instructors.ToDictionary(i => i.Id, _ => new bool[Days.Length, TotalHoursPerDay]);
            var roomTimeline = rooms.ToDictionary(r => r.Id, _ => new bool[Days.Length, TotalHoursPerDay]);
            var groupTimeline = courses.ToDictionary(c => c.Id, _ => new bool[Days.Length, TotalHoursPerDay]);

            var sortedCourses = courses.OrderByDescending(c => c.Students).ToList();

            foreach (var course in sortedCourses)
            {
                bool assigned = false;
                var eligibleInstructors = instructors.Where(i => i.Subjects.Contains(course.SubjectId)).ToList();
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

                                if (IsSlotFree(d, h, duration, instructor.Id, room.Id, course.Id, instructorTimeline, roomTimeline, groupTimeline, dayName, instructorPrefs))
                                {
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

        private bool IsSlotFree(int dayIdx, int hourIdx, int duration, string instructorId,
        string roomId, string courseId, Dictionary<string, bool[,]> instTime, Dictionary<string, bool[,]>
        roomTime, Dictionary<string, bool[,]> groupTime, string dayName, Dictionary<string, InstructorPref> prefs)
        {
            if (dayIdx < 0 || dayIdx >= Days.Length) return false;
            if (hourIdx < 0 || hourIdx >= TotalHoursPerDay) return false;
            if (duration <= 0) return false;

            if (!instTime.ContainsKey(instructorId)) return false;
            if (!roomTime.ContainsKey(roomId)) return false;
            if (!groupTime.ContainsKey(courseId)) return false;

            for (int i = 0; i < duration; i++)
            {
                int currentHour = StartDayHour + hourIdx + i;
                if (hourIdx + i >= TotalHoursPerDay) return false;

                if (instTime[instructorId][dayIdx, hourIdx + i]) return false;
                if (roomTime[roomId][dayIdx, hourIdx + i]) return false;
                if (groupTime[courseId][dayIdx, hourIdx + i]) return false;


                if (prefs.TryGetValue(instructorId, out var p) && p.ForbiddenSlots.ContainsKey(dayName))
                {
                    if (p.ForbiddenSlots[dayName].Contains(currentHour)) return false;
                }
            }
            return true;
        }

        private void UpdateTimelines(int dayIdx, int hourIdx, int duration, string instructorId, string roomId,
        string courseId, Dictionary<string, bool[,]> instTime, Dictionary<string, bool[,]> roomTime, Dictionary<string,
        bool[,]> groupTime, bool state)
        {
            if (dayIdx < 0 || dayIdx >= Days.Length) return;
            if (hourIdx < 0 || hourIdx >= TotalHoursPerDay) return;
            if (duration <= 0) return;
            if (!instTime.ContainsKey(instructorId)) return;
            if (!roomTime.ContainsKey(roomId)) return;
            if (!groupTime.ContainsKey(courseId)) return;

            for (int i = 0; i < duration; i++)
            {
                if (hourIdx + i >= TotalHoursPerDay) break;
                instTime[instructorId][dayIdx, hourIdx + i] = state;
                roomTime[roomId][dayIdx, hourIdx + i] = state;
                groupTime[courseId][dayIdx, hourIdx + i] = state;
            }
        }

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


            var instructorPrefs = LoadPreferences(instructors);

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

            int bestScore = CalculatePenalty(bestPlan, instructorPrefs, instructorIds, uniqueCourseIds, instructorTotalHours, instructorSchedule, courseDays, courseLessonCount, groupSchedule, lessonsPerDay);

            Random rnd = new Random();
            int maxIterations = 50000;

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

                int currentScore = CalculatePenalty(bestPlan, instructorPrefs, instructorIds, uniqueCourseIds, instructorTotalHours, instructorSchedule, courseDays, courseLessonCount, groupSchedule, lessonsPerDay);

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
            }

            Console.WriteLine($"[Optymalizacja SC] Kara początkowa: {CalculatePenalty(initialPlan, instructorPrefs, instructorIds, uniqueCourseIds, instructorTotalHours, instructorSchedule, courseDays, courseLessonCount, groupSchedule, lessonsPerDay)} | Kara końcowa: {bestScore}");
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
                    if (other.CourseId == movedLesson.CourseId) return true;
                }
            }
            return false;
        }

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

        private string MapDayToShort(string fullDay)
        {
            if (string.IsNullOrEmpty(fullDay)) return fullDay;
            switch (fullDay.ToLower().Trim())
            {
                case "monday": return "Mon";
                case "tuesday": return "Tue";
                case "wednesday": return "Wed";
                case "thursday": return "Thu";
                case "friday": return "Fri";
                default: return fullDay;
            }
        }
    }
}