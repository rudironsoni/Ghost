using System.Globalization;
using System.Text;

namespace Ghost.Testing.Server.Fixtures;

#pragma warning disable CA1305 // Culture-specific formatting in test fixtures
#pragma warning disable CA1822 // Member can be static in test fixtures

/// <summary>
/// Generates realistic LinkedIn-style HTML for E2E testing.
/// </summary>
public sealed class LinkedInHtmlFixture
{
    private readonly string _baseUrl;

    /// <summary>
    /// Creates a new LinkedIn HTML fixture.
    /// </summary>
    /// <param name="baseUrl">The base URL for generating links.</param>
    public LinkedInHtmlFixture(string baseUrl)
    {
        _baseUrl = baseUrl;
    }

    /// <summary>
    /// Generates a LinkedIn-style search results page.
    /// </summary>
    public string GenerateSearchResultsPage(string? searchTerm, string? location, int page, int count)
    {
        IReadOnlyList<SampleJob> jobs = SampleJobData.SearchJobs(searchTerm, page, count);
        int totalResults = SampleJobData.Jobs.Count;
        int totalPages = (int)Math.Ceiling((double)totalResults / count);

        StringBuilder sb = new();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"en\">");
        sb.AppendLine("<head>");
        sb.AppendLine("  <meta charset=\"UTF-8\">");
        sb.AppendLine("  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
        sb.AppendLine("  <title>Job Search | LinkedIn</title>");
        sb.AppendLine("  <style>");
        sb.AppendLine(GetLinkedInStyles());
        sb.AppendLine("  </style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine("  <header class=\"global-nav\">");
        sb.AppendLine("    <div class=\"nav-container\">");
        sb.AppendLine("      <div class=\"logo\">LinkedIn</div>");
        sb.AppendLine("      <div class=\"search-bar\">");
        sb.AppendLine(CultureInfo.InvariantCulture, $"        <input type=\"text\" class=\"search-input\" placeholder=\"Search\" value=\"{EscapeHtml(searchTerm)}\" />");
        sb.AppendLine("      </div>");
        sb.AppendLine("    </div>");
        sb.AppendLine("  </header>");
        sb.AppendLine("  <main class=\"jobs-search\">");
        sb.AppendLine("    <div class=\"jobs-search__container\">");
        sb.AppendLine("      <div class=\"jobs-search__left-rail\">");
        sb.AppendLine(CultureInfo.InvariantCulture, $"        <h1 class=\"search-results-count\">{totalResults} results</h1>");
        sb.AppendLine(CultureInfo.InvariantCulture, $"        <p class=\"search-term-echo\">Search: {EscapeHtml(searchTerm)} | Location: {EscapeHtml(location)}</p>");
        sb.AppendLine("        <ul class=\"jobs-search__results-list\">");

        foreach (SampleJob job in jobs)
        {
            sb.AppendLine(GenerateJobCard(job));
        }

        sb.AppendLine("        </ul>");
        sb.AppendLine(GenerateLinkedInPagination(page, totalPages, searchTerm, location));
        sb.AppendLine("      </div>");
        sb.AppendLine("      <div class=\"jobs-search__right-rail\">");
        sb.AppendLine("        <div class=\"job-details\">Select a job to view details</div>");
        sb.AppendLine("      </div>");
        sb.AppendLine("    </div>");
        sb.AppendLine("  </main>");
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return sb.ToString();
    }

    /// <summary>
    /// Generates a LinkedIn-style job detail page.
    /// </summary>
    public string GenerateJobDetailPage(string jobId)
    {
        SampleJob? job = SampleJobData.GetJobById(jobId);
        if (job is null)
        {
            job = SampleJobData.Jobs[0];
        }

        StringBuilder sb = new();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"en\">");
        sb.AppendLine("<head>");
        sb.AppendLine("  <meta charset=\"UTF-8\">");
        sb.AppendLine("  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
        sb.AppendLine(CultureInfo.InvariantCulture, $"  <title>{EscapeHtml(job.Title)} | {EscapeHtml(job.Company)} | LinkedIn</title>");
        sb.AppendLine("  <style>");
        sb.AppendLine(GetLinkedInStyles());
        sb.AppendLine("  </style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine("  <header class=\"global-nav\">");
        sb.AppendLine("    <div class=\"nav-container\">");
        sb.AppendLine("      <div class=\"logo\">LinkedIn</div>");
        sb.AppendLine("    </div>");
        sb.AppendLine("  </header>");
        sb.AppendLine("  <main class=\"job-details-page\">");
        sb.AppendLine("    <div class=\"job-details-container\">");
        sb.AppendLine("      <div class=\"job-header\">");
        sb.AppendLine(CultureInfo.InvariantCulture, $"        <h1 class=\"job-title\" data-test-id=\"job-title\">{EscapeHtml(job.Title)}</h1>");
        sb.AppendLine(CultureInfo.InvariantCulture, $"        <h2 class=\"company-name\" data-test-id=\"company-name\">{EscapeHtml(job.Company)}</h2>");
        sb.AppendLine(CultureInfo.InvariantCulture, $"        <div class=\"location\" data-test-id=\"location\">{EscapeHtml(job.Location)}</div>");
        if (!string.IsNullOrEmpty(job.Salary))
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"        <div class=\"salary\" data-test-id=\"salary\">{EscapeHtml(job.Salary)}</div>");
        }
        sb.AppendLine(CultureInfo.InvariantCulture, $"        <div class=\"post-date\" data-test-id=\"post-date\">Posted {job.PostedDaysAgo} days ago</div>");
        sb.AppendLine("        <div class=\"easy-apply\">");
        sb.AppendLine("          <button class=\"apply-button\" data-test-id=\"easy-apply\">Easy Apply</button>");
        sb.AppendLine("        </div>");
        sb.AppendLine("      </div>");
        sb.AppendLine("      <div class=\"job-description\">");
        sb.AppendLine("        <h3>About the job</h3>");
        sb.AppendLine(CultureInfo.InvariantCulture, $"        <p data-test-id=\"job-description\">{EscapeHtml(job.Description)}</p>");
        sb.AppendLine("        <div class=\"job-metadata\">");
        sb.AppendLine(CultureInfo.InvariantCulture, $"          <div class=\"metadata-item\"><span class=\"label\">Job Type:</span> {job.JobType}</div>");
        sb.AppendLine(CultureInfo.InvariantCulture, $"          <div class=\"metadata-item\"><span class=\"label\">Experience Level:</span> {job.ExperienceLevel}</div>");
        sb.AppendLine(CultureInfo.InvariantCulture, $"          <div class=\"metadata-item\"><span class=\"label\">Remote:</span> {(job.IsRemote ? "Yes" : "No")}</div>");
        sb.AppendLine("        </div>");
        sb.AppendLine("      </div>");
        sb.AppendLine("    </div>");
        sb.AppendLine("  </main>");
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return sb.ToString();
    }

    private string GenerateJobCard(SampleJob job)
    {
        StringBuilder sb = new();
        sb.AppendLine(CultureInfo.InvariantCulture, $"          <li class=\"job-card\" data-job-id=\"{job.Id}\">");
        sb.AppendLine("            <div class=\"job-card__content\">");
        sb.AppendLine("              <h3 class=\"job-card__title\">");
        sb.AppendLine(CultureInfo.InvariantCulture, $"                <a href=\"{_baseUrl}/linkedin/jobs/{job.Id}\" class=\"job-card__title-link\">{EscapeHtml(job.Title)}</a>");
        sb.AppendLine("              </h3>");
        sb.AppendLine(CultureInfo.InvariantCulture, $"              <div class=\"job-card__company-name\">{EscapeHtml(job.Company)}</div>");
        sb.AppendLine(CultureInfo.InvariantCulture, $"              <div class=\"job-card__location\">{EscapeHtml(job.Location)}</div>");
        if (!string.IsNullOrEmpty(job.Salary))
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"              <div class=\"job-card__salary\">{EscapeHtml(job.Salary)}</div>");
        }
        sb.AppendLine(CultureInfo.InvariantCulture, $"              <div class=\"job-card__post-date\">{job.PostedDaysAgo} days ago</div>");
        sb.AppendLine("            </div>");
        sb.AppendLine("          </li>");
        return sb.ToString();
    }

    private string GenerateLinkedInPagination(int currentPage, int totalPages, string? searchTerm, string? location)
    {
        StringBuilder sb = new();
        sb.AppendLine("        <nav class=\"pagination\">");
        sb.AppendLine("          <ul class=\"pagination__list\">");

        if (currentPage > 1)
        {
            string prevUrl = $"{_baseUrl}/linkedin/jobs?page={currentPage - 1}&keywords={Uri.EscapeDataString(searchTerm ?? "")}&location={Uri.EscapeDataString(location ?? "")}";
            sb.AppendLine(CultureInfo.InvariantCulture, $"            <li><a href=\"{prevUrl}\" class=\"pagination__link\">Previous</a></li>");
        }

        for (int i = 1; i <= Math.Min(totalPages, 5); i++)
        {
            string pageUrl = $"{_baseUrl}/linkedin/jobs?page={i}&keywords={Uri.EscapeDataString(searchTerm ?? "")}&location={Uri.EscapeDataString(location ?? "")}";
            string activeClass = i == currentPage ? "pagination__link--active" : "";
            sb.AppendLine(CultureInfo.InvariantCulture, $"            <li><a href=\"{pageUrl}\" class=\"pagination__link {activeClass}\">{i}</a></li>");
        }

        if (currentPage < totalPages)
        {
            string nextUrl = $"{_baseUrl}/linkedin/jobs?page={currentPage + 1}&keywords={Uri.EscapeDataString(searchTerm ?? "")}&location={Uri.EscapeDataString(location ?? "")}";
            sb.AppendLine(CultureInfo.InvariantCulture, $"            <li><a href=\"{nextUrl}\" class=\"pagination__link\">Next</a></li>");
        }

        sb.AppendLine("          </ul>");
        sb.AppendLine("        </nav>");
        return sb.ToString();
    }

    private static string GetLinkedInStyles()
    {
        return """
            body { font-family: -apple-system, system-ui, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; margin: 0; background: #f3f2ef; }
            .global-nav { background: #fff; border-bottom: 1px solid #e0e0e0; padding: 12px 24px; }
            .nav-container { max-width: 1128px; margin: 0 auto; display: flex; align-items: center; gap: 20px; }
            .logo { font-size: 24px; font-weight: bold; color: #0a66c2; }
            .search-input { border: 1px solid #e0e0e0; border-radius: 4px; padding: 8px 12px; width: 300px; }
            .jobs-search { max-width: 1128px; margin: 24px auto; }
            .jobs-search__container { display: flex; gap: 24px; }
            .jobs-search__left-rail { flex: 0 0 400px; }
            .jobs-search__right-rail { flex: 1; }
            .search-results-count { font-size: 18px; margin-bottom: 8px; }
            .search-term-echo { color: #666; margin-bottom: 16px; font-size: 14px; }
            .jobs-search__results-list { list-style: none; padding: 0; margin: 0; }
            .job-card { background: #fff; border-radius: 8px; padding: 16px; margin-bottom: 8px; border: 1px solid #e0e0e0; }
            .job-card__title { font-size: 16px; margin: 0 0 8px 0; }
            .job-card__title-link { color: #0a66c2; text-decoration: none; }
            .job-card__company-name { font-size: 14px; color: #333; margin-bottom: 4px; }
            .job-card__location { font-size: 14px; color: #666; margin-bottom: 4px; }
            .job-card__salary { font-size: 14px; color: #057642; margin-bottom: 4px; }
            .job-card__post-date { font-size: 12px; color: #999; }
            .pagination { margin-top: 24px; }
            .pagination__list { display: flex; gap: 8px; list-style: none; padding: 0; }
            .pagination__link { padding: 8px 12px; border: 1px solid #e0e0e0; border-radius: 4px; text-decoration: none; color: #0a66c2; }
            .pagination__link--active { background: #0a66c2; color: #fff; }
            .job-details-page { max-width: 800px; margin: 24px auto; }
            .job-details-container { background: #fff; border-radius: 8px; padding: 24px; }
            .job-title { font-size: 24px; margin-bottom: 8px; }
            .company-name { font-size: 18px; color: #666; margin-bottom: 8px; font-weight: normal; }
            .location { color: #666; margin-bottom: 8px; }
            .salary { color: #057642; margin-bottom: 8px; }
            .post-date { color: #999; margin-bottom: 16px; }
            .easy-apply { margin-bottom: 24px; }
            .apply-button { background: #0a66c2; color: #fff; border: none; padding: 12px 24px; border-radius: 24px; cursor: pointer; font-size: 16px; }
            .job-description { line-height: 1.5; }
            .job-metadata { margin-top: 24px; padding-top: 24px; border-top: 1px solid #e0e0e0; }
            .metadata-item { margin-bottom: 8px; }
            .label { font-weight: 600; }
            """;
    }

    private static string EscapeHtml(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        return text
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&#x27;");
    }
}
