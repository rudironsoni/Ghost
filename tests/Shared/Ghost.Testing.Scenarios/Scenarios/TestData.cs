using Ghost.Testing.Scenarios.Models;

namespace Ghost.Testing.Scenarios.Scenarios;

/// <summary>
/// Deterministic test data for scenarios.
/// </summary>
public static class TestData
{
    private static readonly List<SyntheticJobPosting> _allJobs = GenerateJobs();

    public const int TotalJobCount = 500;

    /// <summary>
    /// Gets a page of job postings with deterministic data.
    /// </summary>
    public static List<SyntheticJobPosting> GetJobPostings(int offset, int limit)
    {
        return _allJobs.Skip(offset).Take(limit).ToList();
    }

    private static List<SyntheticJobPosting> GenerateJobs()
    {
        List<SyntheticJobPosting> jobs = [];
        var random = new Random(42); // Deterministic seed

        string[] titles = new[]
        {
            "Senior Software Engineer",
            "Frontend Developer",
            "Backend Developer",
            "Full Stack Engineer",
            "DevOps Engineer",
            "Data Scientist",
            "Machine Learning Engineer",
            "Product Manager",
            "UX Designer",
            "QA Engineer",
            "System Administrator",
            "Cloud Architect",
            "Security Engineer",
            "Mobile Developer",
            "Engineering Manager"
        };

        string[] companies = new[]
        {
            "TechCorp", "InnovateLabs", "DataSystems", "CloudNine",
            "DevTools Inc", "SecureNet", "MobileTech", "AIventures",
            "WebScale", "CodeCraft", "SystemsFirst", "AppBuilder",
            "DataFlow", "CloudFirst", "TechVision"
        };

        string[] locations = new[]
        {
            "San Francisco, CA", "New York, NY", "Seattle, WA",
            "Austin, TX", "Boston, MA", "Denver, CO",
            "Portland, OR", "Chicago, IL", "Remote",
            "London, UK", "Berlin, Germany", "Amsterdam, NL"
        };

        for (int i = 0; i < TotalJobCount; i++)
        {
            string title = titles[i % titles.Length];
            string company = companies[random.Next(companies.Length)];
            string location = locations[random.Next(locations.Length)];

            jobs.Add(new SyntheticJobPosting
            {
                Id = $"job-{i:D4}",
                Title = $"{title} #{i}",
                Company = company,
                Location = location,
                Description = $"Exciting opportunity for a {title} at {company}. We are looking for talented individuals to join our team. This is a synthetic test job posting with ID {i}.",
                PostedDate = DateTime.UtcNow.AddDays(-random.Next(1, 30)),
                Salary = random.Next(2) == 0 ? $"${random.Next(80, 200)}k - ${random.Next(200, 300)}k" : null,
                Requirements = new List<string>
                {
                    $"{random.Next(3, 8)}+ years experience",
                    "Strong problem-solving skills",
                    "Team player"
                },
                ApplyUrl = $"/apply/{i}"
            });
        }

        return jobs;
    }
}
