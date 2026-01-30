# Ghost Web API Examples

This directory contains examples and scripts for testing the Ghost Web API with Spanish job platforms (InfoJobs and Tecnoempleo).

## Directory Structure

```
examples/
├── config/
│   ├── appsettings.json          # Complete configuration example
│   └── .env.example              # Environment variables template
└── scripts/
    ├── search-jobs.sh            # Comprehensive job search examples
    ├── test-infojobs.sh          # InfoJobs-specific testing
    ├── test-tecnoempleo.sh       # Tecnoempleo-specific testing
    └── health-check.sh           # API health and platform status checks
```

## Quick Start

### 1. Setup Configuration

Copy the example configuration files:

```bash
# Copy environment variables template
cp examples/config/.env.example .env

# Edit .env file with your credentials
nano .env
```

### 2. Run Health Check

```bash
# Make scripts executable
chmod +x examples/scripts/*.sh

# Test API availability
./examples/scripts/health-check.sh
```

### 3. Test Job Search

```bash
# Comprehensive examples
./examples/scripts/search-jobs.sh

# Platform-specific testing
./examples/scripts/test-infojobs.sh
./examples/scripts/test-tecnoempleo.sh
```

## Configuration

### Environment Variables (.env)

Set your platform credentials in the `.env` file:

```bash
# InfoJobs Configuration
INFOJOBS_API_KEY=your_infojobs_api_key
INFOJOBS_API_SECRET=your_infojobs_api_secret

# Tecnoempleo Configuration  
TECNOEMPLEO_USERNAME=your_tecnoempleo_username
TECNOEMPLEO_PASSWORD=your_tecnoempleo_password
```

### AppSettings Configuration

The `appsettings.json` file contains the complete configuration:

```json
{
  "Ghost": {
    "Extensions": {
      "InfoJobs": {
        "Enabled": true,
        "ApiKey": "${INFOJOBS_API_KEY}",
        "ApiSecret": "${INFOJOBS_API_SECRET}"
      },
      "Tecnoempleo": {
        "Enabled": true,
        "Username": "${TECNOEMPLEO_USERNAME}",
        "Password": "${TECNOEMPLEO_PASSWORD}"
      }
    }
  }
}
```

## API Usage Examples

### Search Jobs Endpoint

**Endpoint**: `POST /api/jobs/search`

**Request Body**:
```json
{
    "query": "desarrollador",
    "location": "Madrid", 
    "maxResults": 10,
    "platforms": ["InfoJobs", "Tecnoempleo"]
}
```

**Response**:
```json
[
    {
        "id": "job-123",
        "title": "Desarrollador Senior",
        "company": "TechCorp España",
        "location": "Madrid",
        "description": "Buscamos desarrollador senior con experiencia...",
        "salary": "45.000€ - 55.000€ anuales",
        "jobType": "FullTime",
        "experienceLevel": "Senior",
        "postedAt": "2024-01-15T10:30:00Z",
        "remote": false,
        "url": "https://infojobs.net/oferta/123",
        "source": "InfoJobs",
        "isEasyApply": true
    }
]
```

### Platform Filtering

You can search specific platforms:

```json
{
    "query": "python",
    "platforms": ["InfoJobs"]  // Only search InfoJobs
}
```

```json
{
    "query": "java",
    "platforms": ["Tecnoempleo"]  // Only search Tecnoempleo
}
```

### Spanish Job Search Examples

Common Spanish job titles and keywords:

- `"desarrollador"` - Developer
- `"ingeniero software"` - Software Engineer
- `"programador"` - Programmer
- `"analista"` - Analyst
- `"consultor"` - Consultant
- `"técnico"` - Technician
- `"remoto"` - Remote

## Script Descriptions

### search-jobs.sh
- Comprehensive examples with various search criteria
- Demonstrates platform aggregation
- Includes error handling examples

### test-infojobs.sh
- InfoJobs-specific testing
- Spanish job titles optimized for InfoJobs
- Platform filtering examples

### test-tecnoempleo.sh  
- Tecnoempleo-specific testing
- Spanish job titles optimized for Tecnoempleo
- Platform filtering examples

### health-check.sh
- API availability testing
- Platform status verification
- Configuration validation

## Troubleshooting

### API Not Responding

1. **Check if API is running**:
   ```bash
   curl http://localhost:5000/health
   ```

2. **Verify configuration**:
   ```bash
   ./examples/scripts/health-check.sh
   ```

3. **Check environment variables**:
   ```bash
   cat .env
   ```

### Platform-Specific Issues

1. **InfoJobs not working**:
   - Verify API credentials in `.env`
   - Check if InfoJobs extension is enabled in configuration

2. **Tecnoempleo not working**:
   - Verify username/password in `.env`
   - Check if Tecnoempleo extension is enabled

### Error Responses

- **HTTP 400**: Invalid request (check request body format)
- **HTTP 500**: Server error (check API logs)
- **Empty results**: Verify platform credentials and search criteria

## Development Notes

- Scripts use `jq` for JSON pretty-printing
- Default API URL is `http://localhost:5000`
- Modify scripts for your specific environment
- All scripts include error handling and status reporting

## Next Steps

After testing with these examples, you can:

1. Integrate the API into your applications
2. Build custom search interfaces
3. Extend the examples with additional platforms
4. Implement caching and rate limiting

---

For more information, refer to the main Ghost documentation.