using System.Globalization;
using System.Text;

namespace Ghost.Testing.Server.Fixtures;

/// <summary>
/// Generates realistic Indeed-style HTML for E2E testing.
/// </summary>
public sealed class IndeedHtmlFixture
{
    private readonly string _baseUrl;

    /// <summary>
    /// Creates a new Indeed HTML fixture.
    /// </summary>
    /// <param name="baseUrl">The base URL for generating links.</param>
    public IndeedHtmlFixture(string baseUrl)
    {
        _baseUrl = baseUrl;
    }

    /// <summary>
    /// Generates an Indeed-style search results page.
    /// </summary>
    public string GenerateSearchResultsPage(string? searchTerm, string? location, int page, int count)
    {
        IReadOnlyList<SampleJob> jobs = SampleJobData.SearchJobs(searchTerm, page, count);
        int totalResults = SampleJobData.Jobs.Count;

        StringBuilder sb = new();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"en\">");
        sb.AppendLine("<head>");
        sb.AppendLine("  <meta charset=\"UTF-8\">");
        sb.AppendLine("  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
        sb.AppendLine(CultureInfo.InvariantCulture, $"  <title>{EscapeHtml(searchTerm)} Jobs in {EscapeHtml(location)} - Indeed</title>");
        sb.AppendLine("  <style>");
        sb.AppendLine(GetIndeedStyles());
        sb.AppendLine("  </style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine("  <div class=\"page-wrapper\">");
        sb.AppendLine("    <header class=\"header\">");
        sb.AppendLine("      <div class=\"header-container\">");
        sb.AppendLine("        <div class=\"logo\">Indeed</div>");
        sb.AppendLine("        <form class=\"search-form\">");
        sb.AppendLine(CultureInfo.InvariantCulture, $"          <input type=\"text\" name=\"q\" class=\"search-input\" placeholder=\"Job title, keywords, or company\" value=\"{EscapeHtml(searchTerm)}\" />");
        sb.AppendLine(CultureInfo.InvariantCulture, $"          <input type=\"text\" name=\"l\" class=\"search-input\" placeholder=\"City, state, or zip code\" value=\"{EscapeHtml(location)}\" />");
        sb.AppendLine("          <button type=\"submit\" class=\"search-button\">Find jobs</button>");
        sb.AppendLine("        </form>");
        sb.AppendLine("      </div>");
        sb.AppendLine("    </header>");
        sb.AppendLine("    <main class=\"main-content\">");
        sb.AppendLine("      <div class=\"results-container\">");
        sb.AppendLine("        <h1 class=\"results-header\">Job Search Results</h1>");
        sb.AppendLine(CultureInfo.InvariantCulture, $"        <p class=\"results-count\">{totalResults} jobs found</p>");
        sb.AppendLine(CultureInfo.InvariantCulture, $"        <p class=\"search-term-display\">Search: \"{EscapeHtml(searchTerm)}\" in \"{EscapeHtml(location)}\"</p>");
        sb.AppendLine("        <ul class=\"job-results\">");

        foreach (SampleJob job in jobs)
        {
            sb.AppendLine(GenerateIndeedJobCard(job));
        }

        sb.AppendLine("        </ul>");
        sb.AppendLine(GenerateIndeedPagination(page, count, totalResults, searchTerm, location));
        sb.AppendLine("      </div>");
        sb.AppendLine("    </main>");
        sb.AppendLine("    <footer class=\"footer\">");
        sb.AppendLine("      <p>Indeed</p>");
        sb.AppendLine("    </footer>");
        sb.AppendLine("  </div>");
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return sb.ToString();
    }

    /// <summary>
    /// Generates an Indeed-style job detail page.
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
        sb.AppendLine(CultureInfo.InvariantCulture, $"  <title>{EscapeHtml(job.Title)} - {EscapeHtml(job.Company)} | Indeed</title>");
        sb.AppendLine("  <style>");
        sb.AppendLine(GetIndeedStyles());
        sb.AppendLine("  </style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine("  <div class=\"page-wrapper\">");
        sb.AppendLine("    <header class=\"header\">");
        sb.AppendLine("      <div class=\"header-container\">");
        sb.AppendLine("        <div class=\"logo\">Indeed</div>");
        sb.AppendLine("      </div>");
        sb.AppendLine("    </header>");
        sb.AppendLine("    <main class=\"main-content\">");
        sb.AppendLine("      <div class=\"job-viewer\">");
        sb.AppendLine("        <div class=\"job-detail-header\">");
        sb.AppendLine(CultureInfo.InvariantCulture, $"          <h1 class=\"job-title\" data-testid=\"jobTitle\">{EscapeHtml(job.Title)}</h1>");
        sb.AppendLine(CultureInfo.InvariantCulture, $"          <div class=\"company-name\" data-testid=\"companyName\">{EscapeHtml(job.Company)}</div>");
        sb.AppendLine(CultureInfo.InvariantCulture, $"          <div class=\"job-location\" data-testid=\"jobLocation\">{EscapeHtml(job.Location)}</div>");
        if (!string.IsNullOrEmpty(job.Salary))
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"          <div class=\"salary\" data-testid=\"salary\">{EscapeHtml(job.Salary)}</div>");
        }
        sb.AppendLine(CultureInfo.InvariantCulture, $"          <div class=\"post-date\" data-testid=\"jobDate\">Posted {job.PostedDaysAgo} days ago</div>");
        sb.AppendLine("          <div class=\"job-actions\">");
        sb.AppendLine("            <button class=\"apply-button\" data-testid=\"applyButton\">Apply Now</button>");
        sb.AppendLine("            <button class=\"save-button\">Save</button>");
        sb.AppendLine("          </div>");
        sb.AppendLine("        </div>");
        sb.AppendLine("        <div class=\"job-detail-body\">");
        sb.AppendLine("          <h2>Job Details</h2>");
        sb.AppendLine("          <div class=\"job-description\" data-testid=\"jobDescription\">");
        sb.AppendLine(CultureInfo.InvariantCulture, $"            <p>{EscapeHtml(job.Description)}</p>");
        sb.AppendLine("          </div>");
        sb.AppendLine("          <div class=\"job-metadata\">");
        sb.AppendLine("            <div class=\"metadata-row\">");
        sb.AppendLine("              <span class=\"metadata-label\">Job Type:</span>");
        sb.AppendLine(CultureInfo.InvariantCulture, $"              <span class=\"metadata-value\">{job.JobType}</span>");
        sb.AppendLine("            </div>");
        sb.AppendLine("            <div class=\"metadata-row\">");
        sb.AppendLine("              <span class=\"metadata-label\">Experience Level:</span>");
        sb.AppendLine(CultureInfo.InvariantCulture, $"              <span class=\"metadata-value\">{job.ExperienceLevel}</span>");
        sb.AppendLine("            </div>");
        sb.AppendLine("            <div class=\"metadata-row\">");
        sb.AppendLine("              <span class=\"metadata-label\">Remote:</span>");
        sb.AppendLine(CultureInfo.InvariantCulture, $"              <span class=\"metadata-value\">{(job.IsRemote ? "Yes" : "No")}</span>");
        sb.AppendLine("            </div>");
        sb.AppendLine("          </div>");
        sb.AppendLine("        </div>");
        sb.AppendLine("      </div>");
        sb.AppendLine("    </main>");
        sb.AppendLine("  </div>");
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return sb.ToString();
    }

    private string GenerateIndeedJobCard(SampleJob job)
    {
        StringBuilder sb = new();
        sb.AppendLine(CultureInfo.InvariantCulture, $"          <li class=\"job-result\" data-jk=\"{job.Id}\">");
        sb.AppendLine("            <div class=\"slider_container\">");
        sb.AppendLine("              <div class=\"slider_list\">");
        sb.AppendLine("                <div class=\"slider_item\">");
        sb.AppendLine("                  <div class=\"job_seen_beacon\">");
        sb.AppendLine("                    <table class=\"jobCard_mainContent\">");
        sb.AppendLine("                      <tbody>");
        sb.AppendLine("                        <tr>");
        sb.AppendLine("                          <td class=\"resultContent\">");
        sb.AppendLine("                            <div class=\"heading-base\">");
        sb.AppendLine("                              <h2 class=\"jobTitle\">");
        sb.AppendLine(CultureInfo.InvariantCulture, $"                                <a href=\"{_baseUrl}/indeed/viewjob?jk={job.Id}\" class=\"jcs-JobTitle\">");
        sb.AppendLine(CultureInfo.InvariantCulture, $"                                  <span data-testid=\"jobTitle\">{EscapeHtml(job.Title)}</span>");
        sb.AppendLine("                                </a>");
        sb.AppendLine("                              </h2>");
        sb.AppendLine("                            </div>");
        sb.AppendLine(CultureInfo.InvariantCulture, $"                            <div data-testid=\"company-name\" class=\"company_name\">{EscapeHtml(job.Company)}</div>");
        sb.AppendLine(CultureInfo.InvariantCulture, $"                            <div data-testid=\"job-location\" class=\"company_location\">{EscapeHtml(job.Location)}</div>");
        if (!string.IsNullOrEmpty(job.Salary))
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"                            <div data-testid=\"job-salary\" class=\"salary-snippet-container\">{EscapeHtml(job.Salary)}</div>");
        }
        sb.AppendLine("                            <div class=\"job-meta\">");
        sb.AppendLine(CultureInfo.InvariantCulture, $"                              <span data-testid=\"job-type\">{job.JobType}</span>");
        sb.AppendLine("                            </div>");
        sb.AppendLine("                            <div class=\"result-footer\">");
        sb.AppendLine(CultureInfo.InvariantCulture, $"                              <span class=\"date\">{job.PostedDaysAgo} days ago</span>");
        sb.AppendLine("                            </div>");
        sb.AppendLine("                          </td>");
        sb.AppendLine("                        </tr>");
        sb.AppendLine("                      </tbody>");
        sb.AppendLine("                    </table>");
        sb.AppendLine("                  </div>");
        sb.AppendLine("                </div>");
        sb.AppendLine("              </div>");
        sb.AppendLine("            </div>");
        sb.AppendLine("          </li>");
        return sb.ToString();
    }

    private string GenerateIndeedPagination(int page, int count, int totalResults, string? searchTerm, string? location)
    {
        int startIndex = (page - 1) * count;
        int totalPages = (int)Math.Ceiling((double)totalResults / count);

        StringBuilder sb = new();
        sb.AppendLine("        <nav class=\"pagination-list\">");
        sb.AppendLine("          <ul>");

        if (page > 1)
        {
            int prevStart = Math.Max(0, startIndex - count);
            string prevUrl = $"{_baseUrl}/indeed/jobs?start={prevStart}&q={Uri.EscapeDataString(searchTerm ?? "")}&l={Uri.EscapeDataString(location ?? "")}";
            sb.AppendLine(CultureInfo.InvariantCulture, $"            <li><a href=\"{prevUrl}\" class=\"previous-button\">Previous</a></li>");
        }

        for (int i = 1; i <= Math.Min(totalPages, 5); i++)
        {
            int pageStart = (i - 1) * count;
            string pageUrl = $"{_baseUrl}/indeed/jobs?start={pageStart}&q={Uri.EscapeDataString(searchTerm ?? "")}&l={Uri.EscapeDataString(location ?? "")}";
            string activeClass = i == page ? "pagination-page-active" : "";
            sb.AppendLine(CultureInfo.InvariantCulture, $"            <li><a href=\"{pageUrl}\" class=\"{activeClass}\">{i}</a></li>");
        }

        if (page < totalPages)
        {
            int nextStart = startIndex + count;
            string nextUrl = $"{_baseUrl}/indeed/jobs?start={nextStart}&q={Uri.EscapeDataString(searchTerm ?? "")}&l={Uri.EscapeDataString(location ?? "")}";
            sb.AppendLine(CultureInfo.InvariantCulture, $"            <li><a href=\"{nextUrl}\" class=\"next-button\">Next</a></li>");
        }

        sb.AppendLine("          </ul>");
        sb.AppendLine("        </nav>");
        return sb.ToString();
    }

    private static string GetIndeedStyles()
    {
        return """
            body { font-family: 'Noto Sans', 'Helvetica Neue', Helvetica, Arial, sans-serif; margin: 0; background: #fff; }
            .page-wrapper { min-height: 100vh; display: flex; flex-direction: column; }
            .header { background: #fff; border-bottom: 1px solid #d4d2d0; padding: 16px 24px; }
            .header-container { max-width: 1200px; margin: 0 auto; display: flex; align-items: center; gap: 24px; }
            .logo { font-size: 28px; font-weight: bold; color: #2557a7; }
            .search-form { display: flex; gap: 8px; flex: 1; }
            .search-input { border: 1px solid #949494; border-radius: 8px; padding: 12px 16px; font-size: 14px; min-width: 200px; }
            .search-button { background: #2557a7; color: #fff; border: none; border-radius: 8px; padding: 12px 24px; font-size: 14px; font-weight: bold; cursor: pointer; }
            .main-content { flex: 1; background: #f3f2f1; }
            .results-container { max-width: 900px; margin: 0 auto; padding: 24px; }
            .results-header { font-size: 24px; margin-bottom: 8px; }
            .results-count { color: #595959; margin-bottom: 8px; }
            .search-term-display { color: #767676; margin-bottom: 16px; }
            .job-results { list-style: none; padding: 0; margin: 0; }
            .job-result { background: #fff; border: 1px solid #d4d2d0; border-radius: 8px; margin-bottom: 16px; padding: 16px; }
            .jobCard_mainContent { width: 100%; }
            .resultContent { vertical-align: top; }
            .jobTitle { margin: 0 0 8px 0; font-size: 18px; }
            .jcs-JobTitle { color: #2557a7; text-decoration: none; }
            .company_name { color: #595959; font-size: 14px; margin-bottom: 4px; }
            .company_location { color: #595959; font-size: 14px; margin-bottom: 8px; }
            .salary-snippet-container { color: #2d2d2d; font-size: 14px; margin-bottom: 8px; }
            .job-meta { margin-bottom: 8px; }
            .result-footer { color: #767676; font-size: 12px; }
            .date { color: #767676; }
            .pagination-list { margin-top: 24px; }
            .pagination-list ul { display: flex; gap: 8px; list-style: none; padding: 0; justify-content: center; }
            .pagination-list a { color: #2557a7; text-decoration: none; padding: 8px 12px; border: 1px solid #d4d2d0; border-radius: 4px; }
            .pagination-page-active { background: #2557a7; color: #fff !important; }
            .footer { background: #f3f2f1; padding: 24px; text-align: center; color: #595959; }
            .job-viewer { background: #fff; border: 1px solid #d4d2d0; border-radius: 8px; padding: 24px; }
            .job-detail-header { border-bottom: 1px solid #d4d2d0; padding-bottom: 24px; margin-bottom: 24px; }
            .job-title { font-size: 28px; margin: 0 0 12px 0; }
            .company-name { font-size: 18px; color: #2557a7; margin-bottom: 8px; }
            .job-location { color: #595959; margin-bottom: 8px; }
            .salary { color: #2d2d2d; font-weight: bold; margin-bottom: 8px; }
            .post-date { color: #767676; margin-bottom: 16px; }
            .job-actions { display: flex; gap: 12px; }
            .apply-button { background: #2557a7; color: #fff; border: none; border-radius: 8px; padding: 12px 24px; font-size: 16px; font-weight: bold; cursor: pointer; }
            .save-button { background: #fff; color: #2557a7; border: 2px solid #2557a7; border-radius: 8px; padding: 10px 22px; font-size: 16px; cursor: pointer; }
            .job-detail-body { line-height: 1.6; }
            .job-description { margin-bottom: 24px; }
            .job-metadata { background: #f9f9f9; padding: 16px; border-radius: 8px; }
            .metadata-row { display: flex; margin-bottom: 8px; }
            .metadata-label { font-weight: bold; min-width: 150px; }
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
