using System.Globalization;

namespace Ghost.Testing.Server.Fixtures;

/// <summary>
/// Provides sample job data for HTML fixtures.
/// </summary>
public static class SampleJobData
{
    /// <summary>
    /// Gets sample jobs for testing.
    /// </summary>
    public static IReadOnlyList<SampleJob> Jobs => new List<SampleJob>
    {
        new(
            Id: "job-001",
            Title: "Senior Software Engineer",
            Company: "TechCorp",
            Location: "San Francisco, CA",
            Salary: "$150,000 - $200,000",
            Description: "We are seeking a Senior Software Engineer to join our growing team...",
            PostedDaysAgo: 2,
            IsRemote: false,
            JobType: "Full-time",
            ExperienceLevel: "Senior"
        ),
        new(
            Id: "job-002",
            Title: "Full Stack Developer",
            Company: "StartupXYZ",
            Location: "Remote",
            Salary: "$100,000 - $140,000",
            Description: "Join our fast-paced startup as a Full Stack Developer...",
            PostedDaysAgo: 1,
            IsRemote: true,
            JobType: "Full-time",
            ExperienceLevel: "Mid-level"
        ),
        new(
            Id: "job-003",
            Title: "Backend Engineer - Python",
            Company: "DataSystems Inc",
            Location: "New York, NY",
            Salary: "$130,000 - $170,000",
            Description: "Looking for an experienced Python backend engineer...",
            PostedDaysAgo: 3,
            IsRemote: false,
            JobType: "Full-time",
            ExperienceLevel: "Senior"
        ),
        new(
            Id: "job-004",
            Title: "Frontend React Developer",
            Company: "WebSolutions",
            Location: "Austin, TX",
            Salary: "$90,000 - $120,000",
            Description: "Seeking a talented React developer to build beautiful user interfaces...",
            PostedDaysAgo: 5,
            IsRemote: true,
            JobType: "Full-time",
            ExperienceLevel: "Mid-level"
        ),
        new(
            Id: "job-005",
            Title: "DevOps Engineer",
            Company: "CloudFirst",
            Location: "Seattle, WA",
            Salary: "$140,000 - $180,000",
            Description: "Join our DevOps team to build and maintain cloud infrastructure...",
            PostedDaysAgo: 1,
            IsRemote: false,
            JobType: "Full-time",
            ExperienceLevel: "Senior"
        ),
        new(
            Id: "job-006",
            Title: "Machine Learning Engineer",
            Company: "AI Innovations",
            Location: "Boston, MA",
            Salary: "$160,000 - $220,000",
            Description: "Develop cutting-edge ML models for our AI platform...",
            PostedDaysAgo: 4,
            IsRemote: true,
            JobType: "Full-time",
            ExperienceLevel: "Senior"
        ),
        new(
            Id: "job-007",
            Title: "Junior Web Developer",
            Company: "Digital Agency",
            Location: "Los Angeles, CA",
            Salary: "$60,000 - $80,000",
            Description: "Great opportunity for a junior developer to grow their skills...",
            PostedDaysAgo: 7,
            IsRemote: false,
            JobType: "Full-time",
            ExperienceLevel: "Entry-level"
        ),
        new(
            Id: "job-008",
            Title: "Mobile App Developer",
            Company: "AppWorks",
            Location: "Chicago, IL",
            Salary: "$110,000 - $150,000",
            Description: "Build native mobile applications for iOS and Android...",
            PostedDaysAgo: 2,
            IsRemote: true,
            JobType: "Contract",
            ExperienceLevel: "Mid-level"
        ),
        new(
            Id: "job-009",
            Title: "Data Engineer",
            Company: "BigData Corp",
            Location: "Denver, CO",
            Salary: "$125,000 - $165,000",
            Description: "Design and implement data pipelines and ETL processes...",
            PostedDaysAgo: 3,
            IsRemote: true,
            JobType: "Full-time",
            ExperienceLevel: "Senior"
        ),
        new(
            Id: "job-010",
            Title: "Security Engineer",
            Company: "SecureNet",
            Location: "Washington, DC",
            Salary: "$145,000 - $190,000",
            Description: "Protect our systems and data from cyber threats...",
            PostedDaysAgo: 1,
            IsRemote: false,
            JobType: "Full-time",
            ExperienceLevel: "Senior"
        )
    }.AsReadOnly();

    /// <summary>
    /// Gets a paginated subset of jobs.
    /// </summary>
    public static IReadOnlyList<SampleJob> GetJobs(int page, int count)
    {
        return Jobs
            .Skip((page - 1) * count)
            .Take(count)
            .ToList()
            .AsReadOnly();
    }

    /// <summary>
    /// Gets a job by ID.
    /// </summary>
    public static SampleJob? GetJobById(string id)
    {
        return Jobs.FirstOrDefault(j => j.Id == id);
    }

    /// <summary>
    /// Gets filtered jobs by search term.
    /// </summary>
    public static IReadOnlyList<SampleJob> SearchJobs(string? searchTerm, int page, int count)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return GetJobs(page, count);
        }

        return Jobs
            .Where(j =>
                j.Title.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                j.Company.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                j.Location.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
            .Skip((page - 1) * count)
            .Take(count)
            .ToList()
            .AsReadOnly();
    }
}

/// <summary>
/// Represents a sample job for testing.
/// </summary>
public sealed record SampleJob(
    string Id,
    string Title,
    string Company,
    string Location,
    string? Salary,
    string Description,
    int PostedDaysAgo,
    bool IsRemote,
    string JobType,
    string ExperienceLevel
);
