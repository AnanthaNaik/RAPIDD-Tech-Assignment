using System.Net.Http;
using System.Text.Json;
using System.Net;
using System.Text;
using System.Globalization;
using System.Text.Json.Serialization;

using var client = new HttpClient();

// Keep your existing API URL here.
string url = "https://rc-vault-fap-live-1.azurewebsites.net/api/gettimeentries?code=vO17RnE8vuzXzPJo5eaLLjXjmRW07law99QTD90zat9FfOQJKKUcgQ==";
string json = await client.GetStringAsync(url);

var entries = JsonSerializer.Deserialize<List<TimeEntry>>(json)
              ?? new List<TimeEntry>();

var employees = entries
    .Where(entry =>
        entry.DeletedOn == null &&
        !string.IsNullOrWhiteSpace(entry.EmployeeName) &&
        entry.StartTime.HasValue &&
        entry.EndTime.HasValue &&
        entry.EndTime.Value >= entry.StartTime.Value)
    .GroupBy(entry => entry.EmployeeName!.Trim())
    .Select(group => new
    {
        Name = group.Key,
        TotalHours = group.Sum(entry =>
            (entry.EndTime!.Value - entry.StartTime!.Value).TotalHours)
    })
    .OrderByDescending(employee => employee.TotalHours)
    .ToList();

foreach (var employee in employees)
{
    Console.WriteLine($"{employee.Name}: {employee.TotalHours:F2} hours");
}

var html = new StringBuilder("""
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1">
    <title>Employee Working Hours</title>
    <style>
        body { font-family: Arial, sans-serif; margin: 40px; }
        table { border-collapse: collapse; width: 100%; max-width: 800px; }
        th, td { border: 1px solid #ccc; padding: 12px; text-align: left; }
        th { background: #243b53; color: white; }
        .below-target { background: #ffe0e0; }
    </style>
</head>
<body>
    <h1>Employee Working Hours</h1>
    <p>Employees with fewer than 100 hours are highlighted.</p>
    <table>
        <thead><tr><th scope="col">Name</th><th scope="col">Total time worked (hours)</th></tr></thead>
        <tbody>
""");

foreach (var employee in employees)
{
    string rowClass = employee.TotalHours < 100 ? " class=\"below-target\"" : "";
    string safeName = WebUtility.HtmlEncode(employee.Name);
    string hours = employee.TotalHours.ToString("F2", CultureInfo.InvariantCulture);
    html.AppendLine($"<tr{rowClass}><td>{safeName}</td><td>{hours}</td></tr>");
}

html.AppendLine("</tbody></table></body></html>");
string outputDirectory = Path.Combine(Directory.GetCurrentDirectory(), "output");
Directory.CreateDirectory(outputDirectory);
string htmlPath = Path.Combine(outputDirectory, "employees.html");
await File.WriteAllTextAsync(htmlPath, html.ToString());
Console.WriteLine($"HTML report saved to: {htmlPath}");

double totalHours = employees.Sum(employee => employee.TotalHours);
if (totalHours > 0)
{
    var plot = new ScottPlot.Plot();
    var pie = plot.Add.Pie(employees.Select(employee => employee.TotalHours).ToArray());
    for (int i = 0; i < employees.Count; i++)
    {
        double percentage = employees[i].TotalHours / totalHours * 100;
        pie.Slices[i].Label = $"{percentage:F1}%";
        pie.Slices[i].LegendText = $"{employees[i].Name} ({percentage:F1}%)";
    }
    plot.Title("Percentage of Total Time Worked by Employee");
    plot.ShowLegend(ScottPlot.Alignment.MiddleRight);
    plot.HideAxesAndGrid();
    string chartPath = Path.Combine(outputDirectory, "employees-pie.png");
    plot.SavePng(chartPath, 1200, 800);
    Console.WriteLine($"Pie chart saved to: {chartPath}");
}
else
{
    Console.WriteLine("No positive working hours available to create a pie chart.");
}

public class TimeEntry
{
    public string? EmployeeName { get; set; }

    // The API commonly spells this field "StarTimeUtc".
    // Check that this matches your actual JSON response.
    [JsonPropertyName("StarTimeUtc")]
    public DateTimeOffset? StartTime { get; set; }

    [JsonPropertyName("EndTimeUtc")]
    public DateTimeOffset? EndTime { get; set; }

    public DateTimeOffset? DeletedOn { get; set; }
}
