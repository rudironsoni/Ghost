using System.Globalization;
using System.Text;

namespace Ghost.Testing.Server.Fixtures;

#pragma warning disable CA1305 // Culture-specific formatting in test fixtures
#pragma warning disable CA1822 // Member can be static in test fixtures

/// <summary>
/// Generates realistic Glassdoor-style HTML for E2E testing.
/// </summary>
public sealed class GlassdoorHtmlFixture
{
    private readonly string _baseUrl;

    /// <summary>
    /// Creates a new Glassdoor HTML fixture.
    /// </summary>
    /// <param name="baseUrl">The base URL for generating links.</param>
    public GlassdoorHtmlFixture(string baseUrl)
    {
        _baseUrl = baseUrl;
    }

    /// <summary>
    /// Generates a Glassdoor-style search results page.
    /// </summary>
    public string GenerateSearchResultsPage(string? searchTerm, string? location, int page)
    {
        int count = 10;
        IReadOnlyList<SampleJob> jobs = SampleJobData.SearchJobs(searchTerm, page, count);
        int totalResults = SampleJobData.Jobs.Count;
        int totalPages = (int)Math.Ceiling((double)totalResults / count);

        StringBuilder sb = new();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"en\">");
        sb.AppendLine("<head>");
        sb.AppendLine("  <meta charset=\"UTF-8\">");
        sb.AppendLine("  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
        sb.AppendLine(CultureInfo.InvariantCulture, $"  <title>{EscapeHtml(searchTerm)} Jobs | Glassdoor</title>");
        sb.AppendLine("  <style>");
        sb.AppendLine(GetGlassdoorStyles());
        sb.AppendLine("  </style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine("  <div id=\"PageHead\">");
        sb.AppendLine("    <header class=\"header\">");
        sb.AppendLine("      <div class=\"headerContent\">");
        sb.AppendLine("        <div class=\"logo\">Glassdoor</div>");
        sb.AppendLine("        <nav class=\"navigation\">");
        sb.AppendLine("          <a href=\"#\" class=\"navItem active\">Jobs</a>");
        sb.AppendLine("          <a href=\"#\" class=\"navItem\">Companies</a>");
        sb.AppendLine("          <a href=\"#\" class=\"navItem\">Salaries</a>");
        sb.AppendLine("        </nav>");
        sb.AppendLine("      </div>");
        sb.AppendLine("    </header>");
        sb.AppendLine("  </div>");
        sb.AppendLine("  <div class=\"searchHeader\">");
        sb.AppendLine("    <div class=\"searchContainer\">");
        sb.AppendLine("      <form class=\"searchForm\">");
        sb.AppendLine(CultureInfo.InvariantCulture, $"        <input type=\"text\" name=\"keyword\" class=\"searchInput\" placeholder=\"Job Title, Keywords, or Company\" value=\"{EscapeHtml(searchTerm)}\" />");
        sb.AppendLine(CultureInfo.InvariantCulture, $"        <input type=\"text\" name=\"location\" class=\"searchInput\" placeholder=\"Location\" value=\"{EscapeHtml(location)}\" />");
        sb.AppendLine("        <button type=\"submit\" class=\"searchButton\">Search</button>");
        sb.AppendLine("      </form>");
        sb.AppendLine("    </div>");
        sb.AppendLine("  </div>");
        sb.AppendLine("  <main class=\"mainContent\">");
        sb.AppendLine("    <div class=\"resultsContainer\">");
        sb.AppendLine("      <div class=\"leftColumn\">");
        sb.AppendLine(CultureInfo.InvariantCulture, $"        <h1 class=\"resultsHeading\">{totalResults} Jobs</h1>");
        sb.AppendLine(CultureInfo.InvariantCulture, $"        <p class=\"searchEcho\">Search: \"{EscapeHtml(searchTerm)}\" in \"{EscapeHtml(location)}\"</p>");
        sb.AppendLine("        <ul class=\"jobListings\">");

        foreach (SampleJob job in jobs)
        {
            sb.AppendLine(GenerateGlassdoorJobCard(job));
        }

        sb.AppendLine("        </ul>");
        sb.AppendLine(GenerateGlassdoorPagination(page, totalPages, searchTerm, location));
        sb.AppendLine("      </div>");
        sb.AppendLine("      <div class=\"rightColumn\">");
        sb.AppendLine("        <div class=\"jobDetailsPlaceholder\">Select a job to view details</div>");
        sb.AppendLine("      </div>");
        sb.AppendLine("    </div>");
        sb.AppendLine("  </main>");
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return sb.ToString();
    }

    /// <summary>
    /// Generates a Glassdoor-style job detail page.
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
        sb.AppendLine(CultureInfo.InvariantCulture, $"  <title>{EscapeHtml(job.Title)} - {EscapeHtml(job.Company)} | Glassdoor</title>");
        sb.AppendLine("  <style>");
        sb.AppendLine(GetGlassdoorStyles());
        sb.AppendLine("  </style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine("  <div id=\"PageHead\">");
        sb.AppendLine("    <header class=\"header\">");
        sb.AppendLine("      <div class=\"headerContent\">");
        sb.AppendLine("        <div class=\"logo\">Glassdoor</div>");
        sb.AppendLine("      </div>");
        sb.AppendLine("    </header>");
        sb.AppendLine("  </div>");
        sb.AppendLine("  <main class=\"jobDetailPage\">");
        sb.AppendLine("    <div class=\"jobHeader\">");
        sb.AppendLine("      <div class=\"jobHeaderContent\">");
        sb.AppendLine(CultureInfo.InvariantCulture, $"        <h1 class=\"jobTitle\" data-test=\"job-title\">{EscapeHtml(job.Title)}</h1>");
        sb.AppendLine(CultureInfo.InvariantCulture, $"        <div class=\"employerName\" data-test=\"employer-name\">{EscapeHtml(job.Company)}</div>");
        sb.AppendLine(CultureInfo.InvariantCulture, $"        <div class=\"location\" data-test=\"location\">{EscapeHtml(job.Location)}</div>");
        if (!string.IsNullOrEmpty(job.Salary))
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"        <div class=\"salary\" data-test=\"salary\">{EscapeHtml(job.Salary)}</div>");
        }
        sb.AppendLine("        <div class=\"jobActions\">");
        sb.AppendLine("          <button class=\"applyButton\" data-test=\"apply-button\">Apply Now</button>");
        sb.AppendLine("          <button class=\"saveButton\">Save</button>");
        sb.AppendLine("        </div>");
        sb.AppendLine("      </div>");
        sb.AppendLine("    </div>");
        sb.AppendLine("    <div class=\"jobContent\">");
        sb.AppendLine("      <div class=\"jobDescription\">");
        sb.AppendLine("        <h2>Job Description</h2>");
        sb.AppendLine(CultureInfo.InvariantCulture, $"        <div data-test=\"job-description\">{EscapeHtml(job.Description)}</div>");
        sb.AppendLine("      </div>");
        sb.AppendLine("      <div class=\"jobInfo\">");
        sb.AppendLine("        <h3>Job Information</h3>");
        sb.AppendLine("        <div class=\"infoGrid\">");
        sb.AppendLine("          <div class=\"infoRow\">");
        sb.AppendLine("            <span class=\"infoLabel\">Job Type:</span>");
        sb.AppendLine(CultureInfo.InvariantCulture, $"            <span class=\"infoValue\">{job.JobType}</span>");
        sb.AppendLine("          </div>");
        sb.AppendLine("          <div class=\"infoRow\">");
        sb.AppendLine("            <span class=\"infoLabel\">Experience Level:</span>");
        sb.AppendLine(CultureInfo.InvariantCulture, $"            <span class=\"infoValue\">{job.ExperienceLevel}</span>");
        sb.AppendLine("          </div>");
        sb.AppendLine("          <div class=\"infoRow\">");
        sb.AppendLine("            <span class=\"infoLabel\">Remote:</span>");
        sb.AppendLine(CultureInfo.InvariantCulture, $"            <span class=\"infoValue\">{(job.IsRemote ? "Yes" : "No")}</span>");
        sb.AppendLine("          </div>");
        sb.AppendLine("          <div class=\"infoRow\">");
        sb.AppendLine("            <span class=\"infoLabel\">Posted:</span>");
        sb.AppendLine(CultureInfo.InvariantCulture, $"            <span class=\"infoValue\">{job.PostedDaysAgo} days ago</span>");
        sb.AppendLine("          </div>");
        sb.AppendLine("        </div>");
        sb.AppendLine("      </div>");
        sb.AppendLine("    </div>");
        sb.AppendLine("  </main>");
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return sb.ToString();
    }

    private string GenerateGlassdoorJobCard(SampleJob job)
    {
        StringBuilder sb = new();
        sb.AppendLine(CultureInfo.InvariantCulture, $"          <li class=\"jobListing\" data-job-id=\"{job.Id}\">");
        sb.AppendLine("            <div class=\"jobCard\">");
        sb.AppendLine("              <div class=\"jobCardContent\">");
        sb.AppendLine("                <div class=\"jobHeaderRow\">");
        sb.AppendLine("                  <div class=\"jobTitleArea\">");
        sb.AppendLine(CultureInfo.InvariantCulture, $"                    <h3 class=\"jobTitle\">");
        sb.AppendLine(CultureInfo.InvariantCulture, $"                      <a href=\"{_baseUrl}/glassdoor/job/{job.Id}\" class=\"jobTitleLink\" data-test=\"job-link\">{EscapeHtml(job.Title)}</a>");
        sb.AppendLine("                    </h3>");
        sb.AppendLine(CultureInfo.InvariantCulture, $"                    <div class=\"companyName\" data-test=\"employer-name\">{EscapeHtml(job.Company)}</div>");
        sb.AppendLine("                  </div>");
        sb.AppendLine("                </div>");
        sb.AppendLine("                <div class=\"jobDetails\">");
        sb.AppendLine(CultureInfo.InvariantCulture, $"                  <div class=\"location\" data-test=\"location\">{EscapeHtml(job.Location)}</div>");
        if (!string.IsNullOrEmpty(job.Salary))
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"                  <div class=\"salary\" data-test=\"salary\">{EscapeHtml(job.Salary)}</div>");
        }
        sb.AppendLine("                </div>");
        sb.AppendLine("                <div class=\"jobFooter\">");
        sb.AppendLine(CultureInfo.InvariantCulture, $"                  <span class=\"postDate\">{job.PostedDaysAgo}d ago</span>");
        sb.AppendLine(CultureInfo.InvariantCulture, $"                  <span class=\"jobType\">{job.JobType}</span>");
        sb.AppendLine("                </div>");
        sb.AppendLine("              </div>");
        sb.AppendLine("            </div>");
        sb.AppendLine("          </li>");
        return sb.ToString();
    }

    private string GenerateGlassdoorPagination(int currentPage, int totalPages, string? searchTerm, string? location)
    {
        StringBuilder sb = new();
        sb.AppendLine("        <nav class=\"pagination\">");
        sb.AppendLine("          <ul class=\"paginationList\">");

        if (currentPage > 1)
        {
            string prevUrl = $"{_baseUrl}/glassdoor/jobs?p={currentPage - 1}&keyword={Uri.EscapeDataString(searchTerm ?? "")}";
            sb.AppendLine(CultureInfo.InvariantCulture, $"            <li class=\"paginationItem\"><a href=\"{prevUrl}\" class=\"paginationLink prev\">Prev</a></li>");
        }

        for (int i = 1; i <= Math.Min(totalPages, 5); i++)
        {
            string pageUrl = $"{_baseUrl}/glassdoor/jobs?p={i}&keyword={Uri.EscapeDataString(searchTerm ?? "")}";
            string activeClass = i == currentPage ? "active" : "";
            sb.AppendLine(CultureInfo.InvariantCulture, $"            <li class=\"paginationItem\"><a href=\"{pageUrl}\" class=\"paginationLink {activeClass}\">{i}</a></li>");
        }

        if (currentPage < totalPages)
        {
            string nextUrl = $"{_baseUrl}/glassdoor/jobs?p={currentPage + 1}&keyword={Uri.EscapeDataString(searchTerm ?? "")}";
            sb.AppendLine(CultureInfo.InvariantCulture, $"            <li class=\"paginationItem\"><a href=\"{nextUrl}\" class=\"paginationLink next\">Next</a></li>");
        }

        sb.AppendLine("          </ul>");
        sb.AppendLine("        </nav>");
        return sb.ToString();
    }

    private static string GetGlassdoorStyles()
    {
        return """
            body { font-family: 'Lato', 'Helvetica Neue', Helvetica, Arial, sans-serif; margin: 0; background: #fff; color: #20262e; }
            .header { background: #fff; border-bottom: 1px solid #e5e5e5; }
            .headerContent { max-width: 1200px; margin: 0 auto; padding: 16px 24px; display: flex; align-items: center; justify-content: space-between; }
            .logo { font-size: 24px; font-weight: bold; color: #0caa41; }
            .navigation { display: flex; gap: 24px; }
            .navItem { color: #20262e; text-decoration: none; font-weight: 500; padding: 8px 0; }
            .navItem.active { border-bottom: 2px solid #0caa41; }
            .searchHeader { background: #f5f6f7; border-bottom: 1px solid #e5e5e5; padding: 24px; }
            .searchContainer { max-width: 1200px; margin: 0 auto; }
            .searchForm { display: flex; gap: 8px; }
            .searchInput { flex: 1; border: 1px solid #c4c7cc; border-radius: 4px; padding: 12px 16px; font-size: 14px; }
            .searchButton { background: #0caa41; color: #fff; border: none; border-radius: 4px; padding: 12px 24px; font-size: 14px; font-weight: bold; cursor: pointer; }
            .mainContent { max-width: 1200px; margin: 0 auto; padding: 24px; }
            .resultsContainer { display: flex; gap: 24px; }
            .leftColumn { flex: 0 0 500px; }
            .rightColumn { flex: 1; }
            .resultsHeading { font-size: 24px; margin-bottom: 8px; }
            .searchEcho { color: #666; margin-bottom: 16px; }
            .jobListings { list-style: none; padding: 0; margin: 0; }
            .jobListing { border-bottom: 1px solid #e5e5e5; padding: 16px 0; }
            .jobCard:hover { background: #f5f6f7; }
            .jobCardContent { padding: 8px; }
            .jobHeaderRow { margin-bottom: 8px; }
            .jobTitle { margin: 0 0 4px 0; font-size: 16px; }
            .jobTitleLink { color: #1861bf; text-decoration: none; }
            .companyName { color: #0caa41; font-size: 14px; margin-bottom: 4px; }
            .jobDetails { color: #5a5a5a; font-size: 14px; margin-bottom: 8px; }
            .location { margin-bottom: 4px; }
            .salary { color: #0c0c0c; font-weight: 500; }
            .jobFooter { display: flex; gap: 16px; font-size: 12px; color: #5a5a5a; }
            .postDate { color: #5a5a5a; }
            .jobType { background: #e5e5e5; padding: 2px 8px; border-radius: 4px; }
            .pagination { margin-top: 24px; }
            .paginationList { display: flex; gap: 8px; list-style: none; padding: 0; }
            .paginationLink { color: #1861bf; text-decoration: none; padding: 8px 12px; border: 1px solid #e5e5e5; border-radius: 4px; }
            .paginationLink.active { background: #1861bf; color: #fff; }
            .paginationLink.prev, .paginationLink.next { font-weight: bold; }
            .jobDetailsPlaceholder { background: #f5f6f7; border-radius: 8px; padding: 24px; text-align: center; color: #666; }
            .jobDetailPage { max-width: 800px; margin: 24px auto; padding: 0 24px; }
            .jobHeader { border-bottom: 1px solid #e5e5e5; padding-bottom: 24px; margin-bottom: 24px; }
            .jobTitle { font-size: 28px; margin: 0 0 12px 0; }
            .employerName { font-size: 20px; color: #0caa41; margin-bottom: 8px; }
            .location { color: #5a5a5a; margin-bottom: 8px; }
            .salary { font-size: 18px; font-weight: 500; margin-bottom: 16px; }
            .jobActions { display: flex; gap: 12px; }
            .applyButton { background: #0caa41; color: #fff; border: none; border-radius: 4px; padding: 12px 24px; font-size: 16px; font-weight: bold; cursor: pointer; }
            .saveButton { background: #fff; color: #1861bf; border: 2px solid #1861bf; border-radius: 4px; padding: 10px 22px; font-size: 16px; cursor: pointer; }
            .jobContent { line-height: 1.6; }
            .jobDescription { margin-bottom: 24px; }
            .jobInfo { background: #f5f6f7; padding: 24px; border-radius: 8px; }
            .infoGrid { display: grid; gap: 12px; }
            .infoRow { display: flex; }
            .infoLabel { font-weight: bold; min-width: 150px; color: #5a5a5a; }
            .infoValue { color: #20262e; }
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
