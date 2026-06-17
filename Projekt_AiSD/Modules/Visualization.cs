using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Projekt_AiSD.Models; 

namespace Projekt_AiSD.Modules
{
    public static class Visualization
    {
        public static void GenerateHtmlReport(
            List<ScheduledLesson> plan,
            List<Instructor> instructors,
            List<Room> rooms,
            List<Course> courses,
            string chartPath = "wykres_zbieznosci.png")
        {
            StringBuilder html = new StringBuilder();

            html.AppendLine("<!DOCTYPE html>");
            html.AppendLine("<html lang='pl'>");
            html.AppendLine("<head>");
            html.AppendLine("<meta charset='utf-8'>");
            html.AppendLine("<meta name='viewport' content='width=device-width, initial-scale=1.0'>");
            html.AppendLine("<title>Raport: Plan Zajęć Uczelni</title>");
            html.AppendLine("<style>");

            
            html.AppendLine("body { font-family: 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; background-color: #f4f7f6; color: #333; margin: 0; padding: 40px; }");
            html.AppendLine(".container { max-width: 1200px; margin: 0 auto; background: #fff; padding: 30px; border-radius: 10px; box-shadow: 0 4px 15px rgba(0,0,0,0.05); }");
            html.AppendLine("h1 { color: #2c3e50; border-bottom: 2px solid #3498db; padding-bottom: 10px; margin-bottom: 30px; }");
            html.AppendLine("h2 { color: #34495e; margin-top: 40px; }");
            html.AppendLine(".chart-container { text-align: center; margin-bottom: 40px; padding: 20px; background: #fafafa; border-radius: 8px; border: 1px dashed #ccc; }");
            html.AppendLine("img { max-width: 100%; height: auto; border-radius: 5px; }");

            html.AppendLine("table { border-collapse: collapse; width: 100%; margin-top: 20px; font-size: 14px; }");
            html.AppendLine("th, td { padding: 12px 15px; text-align: left; border-bottom: 1px solid #ddd; }");
            html.AppendLine("th { background-color: #3498db; color: white; text-transform: uppercase; letter-spacing: 0.5px; }");
            html.AppendLine("tr:nth-child(even) { background-color: #f8f9fa; }");
            html.AppendLine("tr:hover { background-color: #e8f4fd; transition: background-color 0.2s ease; }");

            
            html.AppendLine(".day-row { font-weight: bold; background-color: #e2e8f0 !important; color: #2d3748; }");
            html.AppendLine("</style>");
            html.AppendLine("</head>");
            html.AppendLine("<body>");
            html.AppendLine("<div class='container'>");

            html.AppendLine("<h1>Zoptymalizowany Plan Zajęć</h1>");

            html.AppendLine("<h2>1. Wykres zbieżności algorytmu</h2>");
            html.AppendLine("<div class='chart-container'>");
            html.AppendLine($"<img src='{chartPath}' alt='Wykres zbieżności algorytmu'>");
            html.AppendLine("</div>");

            html.AppendLine("<h2>2. Szczegółowa siatka planu</h2>");
            html.AppendLine("<table>");
            html.AppendLine("<thead>");
            html.AppendLine("<tr>");
            html.AppendLine("<th>Dzień</th>");
            html.AppendLine("<th>Godziny</th>");
            html.AppendLine("<th>Przedmiot</th>");
            html.AppendLine("<th>Grupa</th>");
            html.AppendLine("<th>Prowadzący</th>");
            html.AppendLine("<th>Sala</th>");
            html.AppendLine("</tr>");
            html.AppendLine("</thead>");
            html.AppendLine("<tbody>");

            
            var displayPlan = plan.Select(lesson => {
                var course = courses.FirstOrDefault(c => c.Id == lesson.CourseId);
                var inst = instructors.FirstOrDefault(i => i.Id == lesson.InstructorId);
                var room = rooms.FirstOrDefault(r => r.Id == lesson.RoomId);

                string polishDay = lesson.Day switch
                {
                    "Mon" => "1. Poniedziałek",
                    "Tue" => "2. Wtorek",
                    "Wed" => "3. Środa",
                    "Thu" => "4. Czwartek",
                    "Fri" => "5. Piątek",
                    _ => lesson.Day
                };

                return new
                {
                    Day = polishDay,
                    Start = lesson.StartHour,
                    End = lesson.EndHour,
                    CourseName = course != null ? course.Name : lesson.CourseId,
                    GroupName = course != null ? course.GroupId : "-",
                    InstructorName = inst != null ? inst.Name : lesson.InstructorId,
                    RoomName = room != null ? $"{room.Name} ({room.Id})" : lesson.RoomId
                };
            })
            .OrderBy(l => l.Day)
            .ThenBy(l => l.Start)
            .ToList();

            string currentDay = "";
            foreach (var lesson in displayPlan)
            {
                
                if (currentDay != lesson.Day)
                {
                    currentDay = lesson.Day;
                    html.AppendLine($"<tr class='day-row'><td colspan='6'>{currentDay.Substring(3)}</td></tr>"); // Usuwamy "1. " z nazwy
                }

                html.AppendLine("<tr>");
                html.AppendLine($"<td>{lesson.Day.Substring(3)}</td>");
                html.AppendLine($"<td><b>{lesson.Start}:00 - {lesson.End}:00</b></td>");
                html.AppendLine($"<td>{lesson.CourseName}</td>");
                html.AppendLine($"<td>{lesson.GroupName}</td>");
                html.AppendLine($"<td>{lesson.InstructorName}</td>");
                html.AppendLine($"<td>{lesson.RoomName}</td>");
                html.AppendLine("</tr>");
            }

            html.AppendLine("</tbody>");
            html.AppendLine("</table>");

            html.AppendLine("</div>");
            html.AppendLine("</body>");
            html.AppendLine("</html>");

            File.WriteAllText("raport_planu.html", html.ToString());
        }
    }
}