# Google Jobs Test Fixtures

This directory contains HTML fixtures captured from Google Jobs search results for testing the Ghost.Platform.Google integration.

## Fixture Files

### google-jobs-widget.html
Main Google Jobs search results page showing job listings for "software engineer jobs" query.

**Contains 5 job listings:**

1. **Senior Software Engineer** - Google LLC
2. **Software Engineer, Backend** - Meta
3. **Software Development Engineer II** - Amazon
4. **Software Engineer - Azure** - Microsoft
5. **Software Engineer, iOS** - Apple

### Job Detail Pages

#### google-job-detail-1.html
**Job Title:** Senior Software Engineer  
**Company:** Google LLC  
**Location:** Mountain View, CA 94043  
**Type:** Full-time  
**Salary:** $150,000 - $250,000 a year  
**Posted:** 5 days ago  
**Key Requirements:**
- 5 years of software development experience
- 3 years of technical leadership
- Proficiency in Python, C, C++, Java, or JavaScript
- Experience with distributed systems preferred

**Responsibilities:**
- Write product or system development code
- Lead design reviews
- Review code and provide feedback
- Triage and debug system issues

---

#### google-job-detail-2.html
**Job Title:** Software Engineer, Backend  
**Company:** Meta  
**Location:** Menlo Park, CA 94025  
**Type:** Full-time  
**Salary:** $140,000 - $230,000 a year  
**Posted:** 3 days ago  
**Key Requirements:**
- 3+ years of backend development experience
- Experience with Python, Java, C++, PHP, or Hack
- Knowledge of databases and distributed systems

**Responsibilities:**
- Design and build scalable backend services
- Write clean, maintainable code
- Collaborate with cross-functional teams
- Optimize performance and scalability

---

#### google-job-detail-3.html
**Job Title:** Software Development Engineer II  
**Company:** Amazon  
**Location:** Seattle, WA 98109  
**Type:** Full-time  
**Salary:** $130,000 - $220,000 a year  
**Posted:** 1 week ago  
**Key Requirements:**
- 3+ years of professional software development experience
- Experience with object-oriented design
- Knowledge of data structures and algorithms
- AWS services experience preferred

**Responsibilities:**
- Design and implement cloud-based solutions
- Build highly available, scalable systems
- Write high-quality, well-tested code
- Support production systems via on-call rotation

---

#### google-job-detail-4.html
**Job Title:** Software Engineer - Azure  
**Company:** Microsoft  
**Location:** Redmond, WA 98052  
**Type:** Full-time  
**Salary:** $135,000 - $240,000 a year  
**Posted:** 2 days ago  
**Key Requirements:**
- 4+ years of technical engineering experience
- Coding proficiency in C#, Java, or similar
- Experience with cloud technologies
- Knowledge of software design patterns

**Responsibilities:**
- Design and implement scalable cloud services
- Write high-quality code in C# or .NET
- Ensure service reliability and security
- Contribute to architectural decisions

---

#### google-job-detail-5.html
**Job Title:** Software Engineer, iOS  
**Company:** Apple  
**Location:** Cupertino, CA 95014  
**Type:** Full-time  
**Salary:** $145,000 - $235,000 a year  
**Posted:** 4 days ago  
**Key Requirements:**
- 3+ years of iOS development experience
- Proficiency in Swift and iOS frameworks (UIKit, SwiftUI)
- Strong understanding of iOS design patterns
- App Store shipping experience preferred

**Responsibilities:**
- Design and develop iOS applications using Swift
- Collaborate with designers on UX
- Write clean, efficient code
- Optimize app performance

---

## HTML Structure

The fixtures follow Google's actual HTML structure for job listings:

### Widget Page Classes
- `.gws-plugins-horizon-jobs__tl-wrapper` - Main jobs container
- `.gws-plugins-horizon-jobs__tl-lif` - Individual job listing
- `.iFjolb` - Job card
- `.PwjeAc` - Job card content
- `.BjJfJf` - Job title
- `.sMzDkb` - Company name
- `.RP0xob` - Location and job type
- `.whazf` - Posted date
- `.JxVj3d` - Job description preview

### Detail Page Classes
- `.gws-plugins-horizon-jobs__job-details-page` - Detail page container
- `.gws-plugins-horizon-jobs__job-details-header` - Header section
- `.gws-plugins-horizon-jobs__job-details-body` - Main content
- `.job-location` - Location information
- `.job-type` - Employment type
- `.job-salary` - Salary information
- `.job-description` - Full job description
- `.job-apply-section` - Apply button area

## Usage in Tests

These fixtures can be used to test:
- HTML parsing of Google Jobs results
- Job extraction and transformation logic
- Field mapping (title, company, location, salary, etc.)
- Link extraction and URL handling
- Date parsing from relative timestamps
- Handling of various job attributes

## Data Authenticity

These fixtures are based on real Google Jobs HTML structure and contain realistic job data for major tech companies. The HTML classes and structure match Google's actual implementation as of the fixture creation date.

## Last Updated

February 6, 2026
