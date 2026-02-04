### Learnings - job search logging change

- Added request timing and logging to JobsEndpoints.SearchJobs.
- Injected ILoggerFactory via [FromServices] to avoid static HttpContext usage.
- Used Stopwatch for timing; logged platform name, status, elapsed ms, and query.
- Re-throw exceptions after capturing 'FAILURE' status so existing error middleware handles them.
- LSP diagnostics (csharp-ls) was not available in the environment; used `dotnet build` of the WebApi project to validate compilation of the changed file.

Observations:
- `dotnet build` for the full solution fails due to unrelated errors in Google platform projects (pre-existing). The WebApi project compiled to the point where JobsEndpoints.cs did not introduce errors.
