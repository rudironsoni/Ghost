using System.Text.Json;

var json = File.ReadAllText("logs/glassdoor_search.json");
Console.WriteLine($"JSON length: {json.Length}");
Console.WriteLine($"First 200 chars: {json.Substring(0, Math.Min(200, json.Length))}");

using var doc = JsonDocument.Parse(json);
Console.WriteLine($"Root kind: {doc.RootElement.ValueKind}");

if (doc.RootElement.ValueKind == JsonValueKind.Array)
{
    Console.WriteLine($"Root is array with {doc.RootElement.GetArrayLength()} elements");
    var first = doc.RootElement[0];
    Console.WriteLine($"First element kind: {first.ValueKind}");
    
    if (first.TryGetProperty("data", out var data))
    {
        Console.WriteLine("Found data property");
        if (data.TryGetProperty("jobListings", out var jobListings))
        {
            Console.WriteLine("Found jobListings property");
            if (jobListings.TryGetProperty("jobListings", out var jobs))
            {
                Console.WriteLine($"Found jobs array with {jobs.GetArrayLength()} jobs");
            }
        }
    }
}
