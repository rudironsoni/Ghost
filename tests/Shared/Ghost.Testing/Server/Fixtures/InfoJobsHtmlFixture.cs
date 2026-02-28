using System.Globalization;
using System.Text;

namespace Ghost.Testing.Server.Fixtures;

/// <summary>
/// Generates realistic InfoJobs (Spanish job site)-style HTML for E2E testing.
/// </summary>
public sealed class InfoJobsHtmlFixture
{
    private readonly string _baseUrl;

    /// <summary>
    /// Creates a new InfoJobs HTML fixture.
    /// </summary>
    /// <param name="baseUrl">The base URL for generating links.</param>
    public InfoJobsHtmlFixture(string baseUrl)
    {
        _baseUrl = baseUrl;
    }

    /// <summary>
    /// Generates an InfoJobs-style search results page.
    /// </summary>
    public string GenerateSearchResultsPage(string? searchTerm, string? location, int page)
    {
        int count = 10;
        IReadOnlyList<SampleJob> jobs = SampleJobData.SearchJobs(searchTerm, page, count);
        int totalResults = SampleJobData.Jobs.Count;

        StringBuilder sb = new();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"es\">");
        sb.AppendLine("<head>");
        sb.AppendLine("  <meta charset=\"UTF-8\">");
        sb.AppendLine("  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
        sb.AppendLine(CultureInfo.InvariantCulture, $"  <title>{EscapeHtml(searchTerm)} - Empleo | InfoJobs</title>");
        sb.AppendLine("  <style>");
        sb.AppendLine(GetInfoJobsStyles());
        sb.AppendLine("  </style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine("  <header class=\"main-header\">");
        sb.AppendLine("    <div class=\"header-wrapper\">");
        sb.AppendLine("      <div class=\"logo-container\">");
        sb.AppendLine("        <a href=\"/\" class=\"logo\">InfoJobs</a>");
        sb.AppendLine("      </div>");
        sb.AppendLine("      <nav class=\"main-nav\">");
        sb.AppendLine("        <a href=\"#\" class=\"nav-link\">Buscar ofertas</a>");
        sb.AppendLine("        <a href=\"#\" class=\"nav-link\">Empresas</a>");
        sb.AppendLine("        <a href=\"#\" class=\"nav-link\">Salarios</a>");
        sb.AppendLine("      </nav>");
        sb.AppendLine("    </div>");
        sb.AppendLine("  </header>");
        sb.AppendLine("  <div class=\"search-section\">");
        sb.AppendLine("    <div class=\"search-wrapper\">");
        sb.AppendLine("      <form class=\"search-form\">");
        sb.AppendLine(CultureInfo.InvariantCulture, $"        <input type=\"text\" name=\"palabra\" class=\"search-input\" placeholder=\"Buscar empleo, empresa...\" value=\"{EscapeHtml(searchTerm)}\" />");
        sb.AppendLine("        <select name=\"provincia\" class=\"location-select\">");
        sb.AppendLine("          <option value=\"\">Toda Espana</option>");
        sb.AppendLine(CultureInfo.InvariantCulture, $"          <option value=\"madrid\" selected>{EscapeHtml(location)}</option>");
        sb.AppendLine("        </select>");
        sb.AppendLine("        <button type=\"submit\" class=\"search-button\">Buscar</button>");
        sb.AppendLine("      </form>");
        sb.AppendLine("    </div>");
        sb.AppendLine("  </div>");
        sb.AppendLine("  <main class=\"main-content\">");
        sb.AppendLine("    <div class=\"content-wrapper\">");
        sb.AppendLine("      <div class=\"results-header\">");
        sb.AppendLine(CultureInfo.InvariantCulture, $"        <h1 class=\"results-title\">{totalResults} ofertas de empleo</h1>");
        sb.AppendLine(CultureInfo.InvariantCulture, $"        <p class=\"search-terms\">Busqueda: \"{EscapeHtml(searchTerm)}\" en {EscapeHtml(location)}</p>");
        sb.AppendLine("      </div>");
        sb.AppendLine("      <div class=\"offers-container\">");
        sb.AppendLine("        <ul class=\"offers-list\">");

        foreach (SampleJob job in jobs)
        {
            sb.AppendLine(GenerateInfoJobsOfferCard(job));
        }

        sb.AppendLine("        </ul>");
        sb.AppendLine(GenerateInfoJobsPagination(page, totalResults, searchTerm, location));
        sb.AppendLine("      </div>");
        sb.AppendLine("    </div>");
        sb.AppendLine("  </main>");
        sb.AppendLine("  <footer class=\"main-footer\">");
        sb.AppendLine("    <div class=\"footer-wrapper\">");
        sb.AppendLine("      <p>InfoJobs - Portal de empleo</p>");
        sb.AppendLine("    </div>");
        sb.AppendLine("  </footer>");
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return sb.ToString();
    }

    /// <summary>
    /// Generates an InfoJobs-style job detail page.
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
        sb.AppendLine("<html lang=\"es\">");
        sb.AppendLine("<head>");
        sb.AppendLine("  <meta charset=\"UTF-8\">");
        sb.AppendLine("  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
        sb.AppendLine(CultureInfo.InvariantCulture, $"  <title>{EscapeHtml(job.Title)} - {EscapeHtml(job.Company)} | InfoJobs</title>");
        sb.AppendLine("  <style>");
        sb.AppendLine(GetInfoJobsStyles());
        sb.AppendLine("  </style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine("  <header class=\"main-header\">");
        sb.AppendLine("    <div class=\"header-wrapper\">");
        sb.AppendLine("      <div class=\"logo-container\">");
        sb.AppendLine("        <a href=\"/\" class=\"logo\">InfoJobs</a>");
        sb.AppendLine("      </div>");
        sb.AppendLine("    </div>");
        sb.AppendLine("  </header>");
        sb.AppendLine("  <main class=\"offer-detail\">");
        sb.AppendLine("    <div class=\"detail-wrapper\">");
        sb.AppendLine("      <div class=\"offer-header\">");
        sb.AppendLine(CultureInfo.InvariantCulture, $"        <h1 class=\"offer-title\" data-qa=\"offer-title\">{EscapeHtml(job.Title)}</h1>");
        sb.AppendLine(CultureInfo.InvariantCulture, $"        <h2 class=\"company-name\" data-qa=\"company-name\">{EscapeHtml(job.Company)}</h2>");
        sb.AppendLine(CultureInfo.InvariantCulture, $"        <div class=\"offer-location\" data-qa=\"offer-location\">{EscapeHtml(job.Location)}</div>");
        if (!string.IsNullOrEmpty(job.Salary))
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"        <div class=\"offer-salary\" data-qa=\"offer-salary\">{EscapeHtml(job.Salary)}</div>");
        }
        sb.AppendLine("        <div class=\"offer-actions\">");
        sb.AppendLine("          <button class=\"btn-inscribirse\" data-qa=\"btn-inscribirse\">Inscribirse</button>");
        sb.AppendLine("          <button class=\"btn-guardar\">Guardar</button>");
        sb.AppendLine("        </div>");
        sb.AppendLine("      </div>");
        sb.AppendLine("      <div class=\"offer-body\">");
        sb.AppendLine("        <div class=\"offer-description\">");
        sb.AppendLine("          <h3>Descripcion del puesto</h3>");
        sb.AppendLine(CultureInfo.InvariantCulture, $"          <div data-qa=\"offer-description\">{EscapeHtml(job.Description)}</div>");
        sb.AppendLine("        </div>");
        sb.AppendLine("        <div class=\"offer-details\">");
        sb.AppendLine("          <h3>Detalles</h3>");
        sb.AppendLine("          <div class=\"details-grid\">");
        sb.AppendLine("            <div class=\"detail-row\">");
        sb.AppendLine("              <span class=\"detail-label\">Tipo de contrato:</span>");
        sb.AppendLine(CultureInfo.InvariantCulture, $"              <span class=\"detail-value\">{job.JobType}</span>");
        sb.AppendLine("            </div>");
        sb.AppendLine("            <div class=\"detail-row\">");
        sb.AppendLine("              <span class=\"detail-label\">Experiencia:</span>");
        sb.AppendLine(CultureInfo.InvariantCulture, $"              <span class=\"detail-value\">{job.ExperienceLevel}</span>");
        sb.AppendLine("            </div>");
        sb.AppendLine("            <div class=\"detail-row\">");
        sb.AppendLine("              <span class=\"detail-label\">Teletrabajo:</span>");
        sb.AppendLine(CultureInfo.InvariantCulture, $"              <span class=\"detail-value\">{(job.IsRemote ? "Si" : "No")}</span>");
        sb.AppendLine("            </div>");
        sb.AppendLine("            <div class=\"detail-row\">");
        sb.AppendLine("              <span class=\"detail-label\">Publicado:</span>");
        sb.AppendLine(CultureInfo.InvariantCulture, $"              <span class=\"detail-value\">hace {job.PostedDaysAgo} dias</span>");
        sb.AppendLine("            </div>");
        sb.AppendLine("          </div>");
        sb.AppendLine("        </div>");
        sb.AppendLine("      </div>");
        sb.AppendLine("    </div>");
        sb.AppendLine("  </main>");
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return sb.ToString();
    }

    private string GenerateInfoJobsOfferCard(SampleJob job)
    {
        StringBuilder sb = new();
        sb.AppendLine(CultureInfo.InvariantCulture, $"          <li class=\"offer-item\" data-offer-id=\"{job.Id}\">");
        sb.AppendLine("            <article class=\"offer-card\">");
        sb.AppendLine("              <div class=\"offer-content\">");
        sb.AppendLine("                <header class=\"offer-header-row\">");
        sb.AppendLine("                  <h3 class=\"offer-title\">");
        sb.AppendLine(CultureInfo.InvariantCulture, $"                    <a href=\"{_baseUrl}/infojobs/oferta/{job.Id}\" class=\"offer-link\" data-qa=\"offer-title\">{EscapeHtml(job.Title)}</a>");
        sb.AppendLine("                  </h3>");
        sb.AppendLine(CultureInfo.InvariantCulture, $"                  <div class=\"offer-company\" data-qa=\"offer-company\">{EscapeHtml(job.Company)}</div>");
        sb.AppendLine("                </header>");
        sb.AppendLine("                <div class=\"offer-details-row\">");
        sb.AppendLine(CultureInfo.InvariantCulture, $"                  <span class=\"offer-location\" data-qa=\"offer-location\">{EscapeHtml(job.Location)}</span>");
        if (!string.IsNullOrEmpty(job.Salary))
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"                  <span class=\"offer-salary\" data-qa=\"offer-salary\">{EscapeHtml(job.Salary)}</span>");
        }
        sb.AppendLine("                </div>");
        sb.AppendLine("                <div class=\"offer-meta\">");
        sb.AppendLine(CultureInfo.InvariantCulture, $"                  <span class=\"offer-type\">{job.JobType}</span>");
        sb.AppendLine(CultureInfo.InvariantCulture, $"                  <span class=\"offer-date\">Hace {job.PostedDaysAgo} dias</span>");
        sb.AppendLine("                </div>");
        sb.AppendLine("              </div>");
        sb.AppendLine("            </article>");
        sb.AppendLine("          </li>");
        return sb.ToString();
    }

    private string GenerateInfoJobsPagination(int currentPage, int totalResults, string? searchTerm, string? location)
    {
        int count = 10;
        int totalPages = (int)Math.Ceiling((double)totalResults / count);

        StringBuilder sb = new();
        sb.AppendLine("        <nav class=\"pagination\">");
        sb.AppendLine("          <ul class=\"pagination-list\">");

        if (currentPage > 1)
        {
            string prevUrl = $"{_baseUrl}/infojobs/ofertas?pagina={currentPage - 1}&palabra={Uri.EscapeDataString(searchTerm ?? "")}";
            sb.AppendLine(CultureInfo.InvariantCulture, $"            <li><a href=\"{prevUrl}\" class=\"pagination-prev\">&laquo; Anterior</a></li>");
        }

        for (int i = 1; i <= Math.Min(totalPages, 5); i++)
        {
            string pageUrl = $"{_baseUrl}/infojobs/ofertas?pagina={i}&palabra={Uri.EscapeDataString(searchTerm ?? "")}";
            string activeClass = i == currentPage ? "pagination-active" : "";
            sb.AppendLine(CultureInfo.InvariantCulture, $"            <li><a href=\"{pageUrl}\" class=\"{activeClass}\">{i}</a></li>");
        }

        if (currentPage < totalPages)
        {
            string nextUrl = $"{_baseUrl}/infojobs/ofertas?pagina={currentPage + 1}&palabra={Uri.EscapeDataString(searchTerm ?? "")}";
            sb.AppendLine(CultureInfo.InvariantCulture, $"            <li><a href=\"{nextUrl}\" class=\"pagination-next\">Siguiente &raquo;</a></li>");
        }

        sb.AppendLine("          </ul>");
        sb.AppendLine("        </nav>");
        return sb.ToString();
    }

    private static string GetInfoJobsStyles()
    {
        return """
            body { font-family: 'Open Sans', 'Helvetica Neue', Helvetica, Arial, sans-serif; margin: 0; background: #f4f4f4; color: #333; }
            .main-header { background: #fff; border-bottom: 1px solid #e0e0e0; }
            .header-wrapper { max-width: 1200px; margin: 0 auto; padding: 12px 24px; display: flex; align-items: center; justify-content: space-between; }
            .logo-container { }
            .logo { font-size: 24px; font-weight: bold; color: #167db7; text-decoration: none; }
            .main-nav { display: flex; gap: 24px; }
            .nav-link { color: #333; text-decoration: none; font-size: 14px; padding: 8px 0; }
            .nav-link:hover { color: #167db7; }
            .search-section { background: #167db7; padding: 24px; }
            .search-wrapper { max-width: 1200px; margin: 0 auto; }
            .search-form { display: flex; gap: 12px; }
            .search-input, .location-select { flex: 1; border: none; border-radius: 4px; padding: 12px 16px; font-size: 14px; }
            .search-button { background: #ff6f00; color: #fff; border: none; border-radius: 4px; padding: 12px 32px; font-size: 14px; font-weight: bold; cursor: pointer; }
            .search-button:hover { background: #e65100; }
            .main-content { max-width: 1200px; margin: 0 auto; padding: 24px; }
            .content-wrapper { }
            .results-header { margin-bottom: 24px; }
            .results-title { font-size: 24px; margin: 0 0 8px 0; }
            .search-terms { color: #666; margin: 0; }
            .offers-container { }
            .offers-list { list-style: none; padding: 0; margin: 0; }
            .offer-item { margin-bottom: 16px; }
            .offer-card { background: #fff; border: 1px solid #e0e0e0; border-radius: 8px; padding: 20px; }
            .offer-card:hover { border-color: #167db7; box-shadow: 0 2px 8px rgba(0,0,0,0.1); }
            .offer-header-row { margin-bottom: 12px; }
            .offer-title { margin: 0 0 8px 0; font-size: 18px; }
            .offer-link { color: #167db7; text-decoration: none; }
            .offer-link:hover { text-decoration: underline; }
            .offer-company { color: #666; font-size: 14px; }
            .offer-details-row { display: flex; gap: 16px; margin-bottom: 12px; }
            .offer-location { color: #666; font-size: 14px; }
            .offer-salary { color: #2e7d32; font-size: 14px; font-weight: 500; }
            .offer-meta { display: flex; gap: 12px; font-size: 12px; color: #999; }
            .offer-type { background: #e3f2fd; color: #167db7; padding: 2px 8px; border-radius: 4px; }
            .pagination { margin-top: 32px; }
            .pagination-list { display: flex; gap: 8px; list-style: none; padding: 0; justify-content: center; }
            .pagination-list a { color: #167db7; text-decoration: none; padding: 8px 12px; border: 1px solid #e0e0e0; border-radius: 4px; }
            .pagination-list a:hover { background: #f5f5f5; }
            .pagination-active { background: #167db7 !important; color: #fff !important; border-color: #167db7 !important; }
            .pagination-prev, .pagination-next { font-weight: bold; }
            .main-footer { background: #333; color: #fff; padding: 24px; margin-top: 48px; }
            .footer-wrapper { max-width: 1200px; margin: 0 auto; text-align: center; }
            .offer-detail { max-width: 900px; margin: 24px auto; padding: 0 24px; }
            .detail-wrapper { background: #fff; border-radius: 8px; padding: 32px; }
            .offer-header { border-bottom: 1px solid #e0e0e0; padding-bottom: 24px; margin-bottom: 24px; }
            .offer-title { font-size: 28px; margin: 0 0 12px 0; }
            .company-name { font-size: 20px; color: #666; margin-bottom: 8px; font-weight: normal; }
            .offer-location { color: #666; margin-bottom: 8px; }
            .offer-salary { color: #2e7d32; font-size: 18px; margin-bottom: 16px; }
            .offer-actions { display: flex; gap: 12px; }
            .btn-inscribirse { background: #167db7; color: #fff; border: none; border-radius: 4px; padding: 12px 32px; font-size: 16px; font-weight: bold; cursor: pointer; }
            .btn-inscribirse:hover { background: #125a87; }
            .btn-guardar { background: #fff; color: #167db7; border: 2px solid #167db7; border-radius: 4px; padding: 10px 30px; font-size: 16px; cursor: pointer; }
            .offer-body { line-height: 1.6; }
            .offer-description { margin-bottom: 24px; }
            .offer-description h3 { font-size: 18px; margin-bottom: 12px; }
            .offer-details { background: #f9f9f9; padding: 20px; border-radius: 8px; }
            .offer-details h3 { font-size: 18px; margin-bottom: 16px; }
            .details-grid { }
            .detail-row { display: flex; margin-bottom: 12px; }
            .detail-label { font-weight: 600; min-width: 150px; color: #666; }
            .detail-value { color: #333; }
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
