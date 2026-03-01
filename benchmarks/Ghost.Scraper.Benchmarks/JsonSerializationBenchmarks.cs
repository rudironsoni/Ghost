using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Ghost.Contracts.Jobs;
using Ghost.Contracts.Jobs.Serialization;
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Ghost.Scraper.Benchmarks;

/// <summary>
/// Performance benchmarks comparing reflection-based vs source generator JSON serialization.
/// Demonstrates AOT compatibility and performance improvements.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net90, launchCount: 3, warmupCount: 3, iterationCount: 5)]
public class JsonSerializationBenchmarks
{
    private List<JobListing> _jobListings = [];
    private string _serializedJobs = string.Empty;
    private JsonSerializerOptions _reflectionOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    [GlobalSetup]
    public void Setup()
    {
        _jobListings = CreateSampleJobListings(100);
        _serializedJobs = JsonSerializer.Serialize(_jobListings, _reflectionOptions);
    }

    /// <summary>
    /// Benchmark: Serialize job listings using reflection-based JsonSerializer.
    /// This is the baseline approach that uses runtime reflection.
    /// </summary>
    [Benchmark(Baseline = true)]
    public string SerializeWithReflection()
    {
        return JsonSerializer.Serialize(_jobListings, _reflectionOptions);
    }

    /// <summary>
    /// Benchmark: Serialize job listings using source generator.
    /// Uses compile-time generated serialization logic for better performance and AOT compatibility.
    /// </summary>
    [Benchmark]
    public string SerializeWithSourceGenerator()
    {
        return JsonSerializer.Serialize(_jobListings, JobsSerializerContext.Default.ListJobListing);
    }

    /// <summary>
    /// Benchmark: Deserialize job listings using reflection-based JsonSerializer.
    /// This is the baseline approach that uses runtime reflection.
    /// </summary>
    [Benchmark]
    public List<JobListing>? DeserializeWithReflection()
    {
        return JsonSerializer.Deserialize<List<JobListing>>(_serializedJobs, _reflectionOptions);
    }

    /// <summary>
    /// Benchmark: Deserialize job listings using source generator.
    /// Uses compile-time generated deserialization logic for better performance and AOT compatibility.
    /// </summary>
    [Benchmark]
    public List<JobListing>? DeserializeWithSourceGenerator()
    {
        return JsonSerializer.Deserialize(_serializedJobs, JobsSerializerContext.Default.ListJobListing);
    }

    /// <summary>
    /// Benchmark: Serialize single job using reflection.
    /// </summary>
    [Benchmark]
    public string SerializeSingleJobWithReflection()
    {
        return JsonSerializer.Serialize(_jobListings[0], _reflectionOptions);
    }

    /// <summary>
    /// Benchmark: Serialize single job using source generator.
    /// </summary>
    [Benchmark]
    public string SerializeSingleJobWithSourceGenerator()
    {
        return JsonSerializer.Serialize(_jobListings[0], JobsSerializerContext.Default.JobListing);
    }

    /// <summary>
    /// Creates sample job listings for benchmarking.
    /// </summary>
    private static List<JobListing> CreateSampleJobListings(int count)
    {
        List<JobListing> jobs = new List<JobListing>(count);
        string[] companies = new[] { "TechCorp", "StartupXYZ", "DataDriven", "CloudNative", "AI Systems" };
        string[] locations = new[] { "San Francisco, CA", "New York, NY", "Austin, TX", "Seattle, WA", "Remote" };
        string[] titles = new[] { "Software Engineer", "Product Manager", "Data Scientist", "DevOps Engineer", "UX Designer" };

        for (int i = 0; i < count; i++)
        {
            jobs.Add(new JobListing
            {
                Id = $"job_{i}",
                Title = titles[i % titles.Length],
                Company = companies[i % companies.Length],
                Location = locations[i % locations.Length],
                Description = $"We are looking for a talented {titles[i % titles.Length]} to join our team...",
                Salary = $"${100000 + (i * 1000)} - ${150000 + (i * 1000)} per year",
                JobType = (JobType)(i % 5),
                ExperienceLevel = (ExperienceLevel)(i % 5),
                PostedAt = DateTimeOffset.UtcNow.AddDays(-(i % 30)),
                Remote = i % 3 == 0,
                Url = $"https://example.com/jobs/{i}",
                Source = "Indeed",
                IsEasyApply = i % 2 == 0
            });
        }

        return jobs;
    }
}
