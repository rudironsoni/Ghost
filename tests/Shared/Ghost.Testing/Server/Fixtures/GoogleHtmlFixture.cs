using System.Globalization;
using System.Text;

namespace Ghost.Testing.Server.Fixtures;

#pragma warning disable CA1305 // Culture-specific formatting in test fixtures
#pragma warning disable CA1822 // Member can be static in test fixtures

/// <summary>
/// Generates realistic Google-style HTML for E2E testing.
/// </summary>
public sealed class GoogleHtmlFixture
{
    private readonly string _baseUrl;

    /// <summary>
    /// Creates a new Google HTML fixture.
    /// </summary>
    /// <param name="baseUrl">The base URL for generating links.</param>
    public GoogleHtmlFixture(string baseUrl)
    {
        _baseUrl = baseUrl;
    }

    /// <summary>
    /// Generates a Google-style search results page.
    /// </summary>
    public string GenerateSearchResultsPage(string? searchTerm, int page)
    {
        int count = 10;
        IReadOnlyList<SampleJob> jobs = SampleJobData.SearchJobs(searchTerm, page, count);
        int totalResults = SampleJobData.Jobs.Count;

        StringBuilder sb = new();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"en\">");
        sb.AppendLine("<head>");
        sb.AppendLine("  <meta charset=\"UTF-8\">");
        sb.AppendLine("  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
        sb.AppendLine(CultureInfo.InvariantCulture, $"  <title>{EscapeHtml(searchTerm)} jobs - Google Search</title>");
        sb.AppendLine("  <style>");
        sb.AppendLine(GetGoogleStyles());
        sb.AppendLine("  </style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine("  <div class=\"appbar\">");
        sb.AppendLine("    <div class=\"gb_Ed\">");
        sb.AppendLine("      <div class=\"logo-container\">");
        sb.AppendLine("        <span class=\"logo\">Google</span>");
        sb.AppendLine("      </div>");
        sb.AppendLine("      <div class=\"search-container\">");
        sb.AppendLine("        <form class=\"search-form\">");
        sb.AppendLine(CultureInfo.InvariantCulture, $"          <input type=\"text\" class=\"search-box\" value=\"{EscapeHtml(searchTerm)} jobs\" />");
        sb.AppendLine("        </form>");
        sb.AppendLine("      </div>");
        sb.AppendLine("    </div>");
        sb.AppendLine("  </div>");
        sb.AppendLine("  <div class=\"hdtb\">");
        sb.AppendLine("    <div class=\"hdtb-mitem\">All</div>");
        sb.AppendLine("    <div class=\"hdtb-mitem hdtb-msel\">Jobs</div>");
        sb.AppendLine("    <div class=\"hdtb-mitem\">Images</div>");
        sb.AppendLine("    <div class=\"hdtb-mitem\">News</div>");
        sb.AppendLine("  </div>");
        sb.AppendLine("  <div class=\"main\">");
        sb.AppendLine("    <div class=\"jobs-container\">");
        sb.AppendLine(CultureInfo.InvariantCulture, $"      <div class=\"result-stats\">About {totalResults} results</div>");
        sb.AppendLine(CultureInfo.InvariantCulture, $"      <div class=\"search-echo\">Showing jobs for: \"{EscapeHtml(searchTerm)}\"</div>");
        sb.AppendLine("      <div class=\"jobs-list\">");

        foreach (SampleJob job in jobs)
        {
            sb.AppendLine(GenerateGoogleJobCard(job));
        }

        sb.AppendLine("      </div>");
        sb.AppendLine(GenerateGooglePagination(page, totalResults, searchTerm));
        sb.AppendLine("    </div>");
        sb.AppendLine("  </div>");
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return sb.ToString();
    }

    private string GenerateGoogleJobCard(SampleJob job)
    {
        StringBuilder sb = new();
        sb.AppendLine("        <div class=\"g\">");
        sb.AppendLine("          <div class=\"job-listing\">");
        sb.AppendLine("            <div class=\"job-listing-content\">");
        sb.AppendLine("              <div class=\"job-header\">");
        sb.AppendLine("                <h3 class=\"job-title\">");
        sb.AppendLine(CultureInfo.InvariantCulture, $"                  <a href=\"{_baseUrl}/google/job/{job.Id}\">{EscapeHtml(job.Title)}</a>");
        sb.AppendLine("                </h3>");
        sb.AppendLine("              </div>");
        sb.AppendLine("              <div class=\"job-details\">");
        sb.AppendLine(CultureInfo.InvariantCulture, $"                <div class=\"company-name\">{EscapeHtml(job.Company)}</div>");
        sb.AppendLine(CultureInfo.InvariantCulture, $"                <div class=\"location\">{EscapeHtml(job.Location)}</div>");
        sb.AppendLine("              </div>");
        if (!string.IsNullOrEmpty(job.Salary))
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"              <div class=\"salary\">{EscapeHtml(job.Salary)}</div>");
        }
        sb.AppendLine("              <div class=\"job-metadata\">");
        sb.AppendLine(CultureInfo.InvariantCulture, $"                <span class=\"job-type\">{job.JobType}</span>");
        sb.AppendLine(CultureInfo.InvariantCulture, $"                <span class=\"experience-level\">{job.ExperienceLevel}</span>");
        sb.AppendLine("              </div>");
        sb.AppendLine(CultureInfo.InvariantCulture, $"              <div class=\"snippet\">{EscapeHtml(job.Description[..Math.Min(150, job.Description.Length)])}...</div>");
        sb.AppendLine(CultureInfo.InvariantCulture, $"              <div class=\"post-date\">Posted {job.PostedDaysAgo} days ago</div>");
        sb.AppendLine("            </div>");
        sb.AppendLine("          </div>");
        sb.AppendLine("        </div>");
        return sb.ToString();
    }

    private string GenerateGooglePagination(int currentPage, int totalResults, string? searchTerm)
    {
        int resultsPerPage = 10;
        int totalPages = (int)Math.Ceiling((double)totalResults / resultsPerPage);

        StringBuilder sb = new();
        sb.AppendLine("      <div class=\"pagination\">");
        sb.AppendLine("        <table>");
        sb.AppendLine("          <tr>");

        if (currentPage > 1)
        {
            string prevUrl = $"{_baseUrl}/google/jobs?q={Uri.EscapeDataString(searchTerm ?? "")}&page={currentPage - 1}";
            sb.AppendLine(CultureInfo.InvariantCulture, $"            <td><a href=\"{prevUrl}\" class=\"pn prev\">&lt; Prev</a></td>");
        }

        for (int i = 1; i <= Math.Min(totalPages, 10); i++)
        {
            string pageUrl = $"{_baseUrl}/google/jobs?q={Uri.EscapeDataString(searchTerm ?? "")}&page={i}";
            if (i == currentPage)
            {
                sb.AppendLine(CultureInfo.InvariantCulture, $"            <td class=\"cur\">{i}</td>");
            }
            else
            {
                sb.AppendLine(CultureInfo.InvariantCulture, $"            <td><a href=\"{pageUrl}\">{i}</a></td>");
            }
        }

        if (currentPage < totalPages)
        {
            string nextUrl = $"{_baseUrl}/google/jobs?q={Uri.EscapeDataString(searchTerm ?? "")}&page={currentPage + 1}";
            sb.AppendLine(CultureInfo.InvariantCulture, $"            <td><a href=\"{nextUrl}\" class=\"pn next\">Next &gt;</a></td>");
        }

        sb.AppendLine("          </tr>");
        sb.AppendLine("        </table>");
        sb.AppendLine("      </div>");
        return sb.ToString();
    }

    private static string GetGoogleStyles()
    {
        return """
            body { font-family: 'Roboto', arial, sans-serif; margin: 0; font-size: 14px; color: #202124; }
            .appbar { background: #fff; border-bottom: 1px solid #ebebeb; padding: 8px 0; }
            .gb_Ed { max-width: 1200px; margin: 0 auto; display: flex; align-items: center; padding: 0 16px; }
            .logo-container { margin-right: 24px; }
            .logo { font-size: 24px; color: #4285f4; font-weight: 500; }
            .search-container { flex: 1; max-width: 692px; }
            .search-form { width: 100%; }
            .search-box { width: 100%; border: 1px solid #dfe1e5; border-radius: 24px; padding: 12px 20px; font-size: 16px; outline: none; box-shadow: none; }
            .hdtb { max-width: 1200px; margin: 0 auto; padding: 12px 16px; border-bottom: 1px solid #ebebeb; display: flex; gap: 24px; }
            .hdtb-mitem { padding: 8px 0; color: #5f6368; cursor: pointer; }
            .hdtb-msel { color: #1a73e8; border-bottom: 3px solid #1a73e8; font-weight: 500; }
            .main { max-width: 1200px; margin: 0 auto; padding: 16px; }
            .jobs-container { max-width: 700px; }
            .result-stats { color: #70757a; font-size: 14px; margin-bottom: 8px; }
            .search-echo { color: #5f6368; margin-bottom: 16px; }
            .jobs-list { display: flex; flex-direction: column; gap: 16px; }
            .g { border: 1px solid #dfe1e5; border-radius: 8px; padding: 16px; }
            .g:hover { box-shadow: 0 1px 6px rgba(32, 33, 36, 0.28); }
            .job-listing-content { }
            .job-header { margin-bottom: 8px; }
            .job-title { margin: 0; font-size: 20px; line-height: 1.3; }
            .job-title a { color: #1a0dab; text-decoration: none; }
            .job-title a:hover { text-decoration: underline; }
            .job-details { display: flex; gap: 16px; margin-bottom: 8px; }
            .company-name { color: #202124; font-weight: 500; }
            .location { color: #5f6368; }
            .salary { color: #188038; margin-bottom: 8px; }
            .job-metadata { display: flex; gap: 8px; margin-bottom: 8px; }
            .job-type, .experience-level { background: #f1f3f4; padding: 4px 8px; border-radius: 4px; font-size: 12px; color: #5f6368; }
            .snippet { color: #4d5156; line-height: 1.58; margin-bottom: 8px; }
            .post-date { color: #70757a; font-size: 12px; }
            .pagination { margin-top: 24px; }
            .pagination table { margin: 0 auto; border-collapse: collapse; }
            .pagination td { padding: 8px; text-align: center; }
            .pagination a { color: #4285f4; text-decoration: none; }
            .pagination a:hover { text-decoration: underline; }
            .pagination .cur { color: #202124; font-weight: bold; }
            .pagination .pn { font-weight: bold; }
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
