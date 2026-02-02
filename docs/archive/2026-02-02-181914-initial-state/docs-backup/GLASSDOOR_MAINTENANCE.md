# Glassdoor Maintenance Guide

This guide covers common issues, troubleshooting steps, and maintenance procedures for the Glassdoor integration.

## 1. JobSpy Fallback Pattern

### Overview
The JobSpy fallback pattern ensures reliable data extraction by switching between multiple scraping methods when one fails.

### How It Works
```python
# Primary extraction method
try:
    data = extract_with_primary_method()
except (PrimaryMethodError, RateLimitError) as e:
    # Fallback to secondary method
    try:
        data = extract_with_secondary_method()
    except (SecondaryMethodError, RateLimitError) as e:
        # Final fallback to JobSpy
        data = extract_with_jobspy()
```

### Implementation Details
- **Primary Method**: Direct API calls or web scraping
- **Secondary Method**: Alternative scraping approach (different headers, user agents)
- **JobSpy**: Third-party library as ultimate fallback

### When to Use Each Method
- **Primary**: Normal operations, when rate limits allow
- **Secondary**: When primary hits rate limits or gets blocked
- **JobSpy**: When both primary and secondary methods fail

### Monitoring Fallback Usage
```python
# Track fallback usage in logs
logger.info(f"Method used: {method_name}")
logger.warning(f"Fallback triggered: {failed_method} -> {new_method}")
```

## 2. CSRF Token Extraction

### Understanding CSRF Tokens
Glassdoor uses CSRF (Cross-Site Request Forgery) tokens to prevent automated requests. These tokens are required for authenticated requests.

### Token Extraction Process

#### Method 1: From HTML Response
```python
def extract_csrf_token(html_content):
    """Extract CSRF token from HTML meta tag or JavaScript variable"""
    import re
    
    # Look for meta tag
    meta_pattern = r'<meta name="csrf-token" content="([^"]+)"'
    match = re.search(meta_pattern, html_content)
    if match:
        return match.group(1)
    
    # Look for JavaScript variable
    js_pattern = r'window\.__CSRF_TOKEN__\s*=\s*"([^"]+)"'
    match = re.search(js_pattern, html_content)
    if match:
        return match.group(1)
    
    return None
```

#### Method 2: From Cookies
```python
def extract_csrf_from_cookies(cookies):
    """Extract CSRF token from cookie jar"""
    for cookie in cookies:
        if cookie.name.lower() in ['csrf_token', 'csrf', '_csrf']:
            return cookie.value
    return None
```

#### Method 3: From Response Headers
```python
def extract_csrf_from_headers(headers):
    """Extract CSRF token from response headers"""
    csrf_headers = ['x-csrf-token', 'x-csrf-token', 'csrf-token']
    
    for header_name in csrf_headers:
        if header_name in headers:
            return headers[header_name]
    return None
```

### Token Refresh Strategy
- Extract token before each authenticated request
- Refresh token if request returns 403/401
- Store token with expiration timestamp
- Refresh proactively when 80% of token lifetime elapsed

## 3. Common GraphQL Errors and Solutions

### Error Types and Solutions

#### 1. Authentication Errors (401/403)
**Symptoms**: `GraphQL error: Unauthorized` or `Authentication required`

**Solutions**:
```python
# Check session validity
if not session.is_valid():
    # Re-authenticate
    session = authenticate_user()
    
# Verify CSRF token
if not validate_csrf_token(current_token):
    # Refresh CSRF token
    current_token = refresh_csrf_token()
```

#### 2. Rate Limiting (429)
**Symptoms**: `GraphQL error: Rate limit exceeded`

**Solutions**:
```python
# Implement exponential backoff
import time
from functools import wraps

def rate_limit_handler(func):
    @wraps(func)
    def wrapper(*args, **kwargs):
        max_retries = 3
        base_delay = 1
        
        for attempt in range(max_retries):
            try:
                return func(*args, **kwargs)
            except RateLimitError:
                if attempt == max_retries - 1:
                    raise
                
                delay = base_delay * (2 ** attempt)
                time.sleep(delay)
                
        return None
    return wrapper
```

#### 3. Schema Validation Errors
**Symptoms**: `GraphQL error: Field not found` or `Invalid query`

**Solutions**:
```python
# Validate query structure
def validate_graphql_query(query):
    """Validate GraphQL query structure"""
    try:
        # Parse query to check syntax
        parsed = parse(query)
        
        # Check required fields
        required_fields = ['data', 'errors']
        if not all(field in parsed for field in required_fields):
            raise ValidationError("Missing required fields")
            
        return True
    except Exception as e:
        logger.error(f"Query validation failed: {e}")
        return False
```

#### 4. Network Timeouts
**Symptoms**: `GraphQL error: Request timeout`

**Solutions**:
```python
# Increase timeout and implement retry
import requests

def make_graphql_request(query, variables=None, timeout=30):
    """Make GraphQL request with timeout and retry"""
    max_retries = 3
    
    for attempt in range(max_retries):
        try:
            response = requests.post(
                GRAPHQL_ENDPOINT,
                json={'query': query, 'variables': variables},
                timeout=timeout,
                headers={'Authorization': f'Bearer {access_token}'}
            )
            return response.json()
        except requests.Timeout:
            if attempt == max_retries - 1:
                raise
            time.sleep(2 ** attempt)
```

## 4. Session Management Troubleshooting

### Session Lifecycle Management

#### Session Creation
```python
def create_session():
    """Create new authenticated session"""
    session = requests.Session()
    
    # Set common headers
    session.headers.update({
        'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36',
        'Accept': 'application/json, text/plain, */*',
        'Accept-Language': 'en-US,en;q=0.9',
        'Accept-Encoding': 'gzip, deflate, br',
        'Connection': 'keep-alive',
        'Upgrade-Insecure-Requests': '1'
    })
    
    return session
```

#### Session Validation
```python
def validate_session(session):
    """Validate session is still active"""
    try:
        # Make lightweight request to check session
        response = session.get('https://www.glassdoor.com/profile/ajax/profile')
        return response.status_code == 200
    except Exception as e:
        logger.error(f"Session validation failed: {e}")
        return False
```

#### Session Refresh
```python
def refresh_session(session):
    """Refresh session when it becomes invalid"""
    try:
        # Attempt to refresh session
        response = session.post('https://www.glassdoor.com/profile/ajax/refresh')
        
        if response.status_code == 200:
            logger.info("Session refreshed successfully")
            return True
        else:
            logger.warning(f"Session refresh failed: {response.status_code}")
            return False
            
    except Exception as e:
        logger.error(f"Session refresh error: {e}")
        return False
```

### Common Session Issues

#### Issue 1: Session Expires Frequently
**Symptoms**: Frequent 401 errors, need to re-authenticate often

**Solutions**:
- Implement proactive session refresh
- Store session with longer expiration
- Use session pooling to reuse valid sessions

#### Issue 2: Session Gets Blocked
**Symptoms**: All requests return 403, session works but data extraction fails

**Solutions**:
```python
def handle_blocked_session(session):
    """Handle when session gets blocked by Glassdoor"""
    # Clear session cookies
    session.cookies.clear()
    
    # Rotate user agent
    session.headers['User-Agent'] = get_random_user_agent()
    
    # Reset session state
    session = create_session()
    
    return session
```

#### Issue 3: Session Pool Exhaustion
**Symptoms**: No available sessions in pool, all sessions invalid

**Solutions**:
```python
class SessionPool:
    def __init__(self, max_size=10):
        self.pool = queue.Queue(maxsize=max_size)
        self.active_sessions = set()
    
    def get_session(self):
        """Get available session from pool"""
        try:
            session = self.pool.get_nowait()
            if validate_session(session):
                self.active_sessions.add(id(session))
                return session
            else:
                # Session invalid, create new one
                return create_session()
        except queue.Empty:
            # Pool empty, create new session
            return create_session()
    
    def return_session(self, session):
        """Return session to pool"""
        if id(session) in self.active_sessions:
            self.active_sessions.remove(id(session))
            try:
                self.pool.put_nowait(session)
            except queue.Full:
                # Pool full, discard session
                pass
```

## 5. When to Refresh Tokens

### Token Types and Refresh Triggers

#### 1. CSRF Token Refresh
**When to Refresh**:
- Before each authenticated request
- When receiving 403/401 errors
- After 30 minutes of inactivity
- When token validation fails

```python
def should_refresh_csrf_token(last_refresh_time, current_time):
    """Determine if CSRF token should be refreshed"""
    token_lifetime = 30 * 60  # 30 minutes
    return (current_time - last_refresh_time) > token_lifetime
```

#### 2. Access Token Refresh
**When to Refresh**:
- When receiving 401 Unauthorized errors
- When token is close to expiration (within 5 minutes)
- After successful re-authentication

```python
def should_refresh_access_token(token_expiry_time, current_time):
    """Determine if access token should be refreshed"""
    refresh_threshold = 5 * 60  # 5 minutes
    return token_expiry_time - current_time < refresh_threshold
```

#### 3. Session Token Refresh
**When to Refresh**:
- When session validation fails
- After extended periods of inactivity
- When receiving session-specific errors

### Automated Refresh Strategy

```python
class TokenManager:
    def __init__(self):
        self.csrf_token = None
        self.access_token = None
        self.session_token = None
        self.last_refresh = {
            'csrf': time.time(),
            'access': time.time(),
            'session': time.time()
        }
    
    def refresh_if_needed(self, token_type):
        """Refresh token if needed based on type and timing"""
        current_time = time.time()
        
        if token_type == 'csrf':
            if self.should_refresh_csrf_token(
                self.last_refresh['csrf'], current_time
            ):
                self.refresh_csrf_token()
                self.last_refresh['csrf'] = current_time
                
        elif token_type == 'access':
            if self.should_refresh_access_token(
                self.get_access_token_expiry(), current_time
            ):
                self.refresh_access_token()
                self.last_refresh['access'] = current_time
                
        elif token_type == 'session':
            if self.should_refresh_session(
                self.last_refresh['session'], current_time
            ):
                self.refresh_session()
                self.last_refresh['session'] = current_time
```

### Manual Refresh Triggers

#### Emergency Refresh
```python
def emergency_token_refresh():
    """Emergency refresh all tokens when system detects issues"""
    logger.warning("Emergency token refresh triggered")
    
    # Refresh all token types
    refresh_csrf_token()
    refresh_access_token()
    refresh_session()
    
    # Clear any cached data that might be invalid
    clear_token_cache()
    
    logger.info("Emergency token refresh completed")
```

#### Scheduled Refresh
```python
import schedule
import time

def schedule_token_refresh():
    """Schedule regular token refresh"""
    # Refresh CSRF token every 25 minutes
    schedule.every(25).minutes.do(refresh_csrf_token)
    
    # Refresh access token every 4 hours
    schedule.every(4).hours.do(refresh_access_token)
    
    # Refresh session every 2 hours
    schedule.every(2).hours.do(refresh_session)
    
    while True:
        schedule.run_pending()
        time.sleep(60)
```

## Monitoring and Alerting

### Key Metrics to Monitor
1. **Token Refresh Rate**: How often tokens are being refreshed
2. **Session Success Rate**: Percentage of successful requests
3. **Fallback Usage**: How often JobSpy fallback is triggered
4. **Error Rates**: Frequency of different error types
5. **Response Times**: Average response times for different methods

### Alert Conditions
- Token refresh rate > 50% of requests
- Session success rate < 90%
- JobSpy fallback usage > 20%
- GraphQL error rate > 10%
- Average response time > 30 seconds

### Logging Best Practices
```python
import logging

# Configure structured logging
logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s - %(name)s - %(levelname)s - %(message)s'
)

# Log token operations
logger.info(f"Token refreshed: {token_type}")
logger.warning(f"Token refresh failed: {token_type} - {error}")
logger.error(f"Critical token failure: {error}")

# Log session operations
logger.info(f"Session created: {session_id}")
logger.warning(f"Session validation failed: {session_id}")
logger.error(f"Session pool exhausted")
```

## Troubleshooting Checklist

When encountering issues, check in this order:

1. **Validate Token Status**
   - [ ] Are tokens expired?
   - [ ] Are tokens properly formatted?
   - [ ] Do tokens match expected patterns?

2. **Check Session Health**
   - [ ] Is session still valid?
   - [ ] Are cookies properly set?
   - [ ] Is session blocked or rate-limited?

3. **Verify Network Connectivity**
   - [ ] Can reach Glassdoor endpoints?
   - [ ] Are there network timeouts?
   - [ ] Is DNS resolution working?

4. **Review Rate Limiting**
   - [ ] Are requests being throttled?
   - [ ] Is the request frequency appropriate?
   - [ ] Should backoff strategy be adjusted?

5. **Analyze Error Patterns**
   - [ ] What specific errors are occurring?
   - [ ] Are errors consistent or intermittent?
   - [ ] Do errors correlate with specific endpoints?

6. **Test Fallback Mechanisms**
   - [ ] Is JobSpy fallback working?
   - [ ] Are alternative methods available?
   - [ ] Is the fallback strategy appropriate?

## Emergency Procedures

### Complete System Failure
1. Stop all active requests
2. Clear all token caches
3. Restart with fresh authentication
4. Enable verbose logging
5. Monitor recovery progress

### Partial System Failure
1. Identify failing components
2. Switch to fallback methods
3. Isolate problematic endpoints
4. Gradually restore functionality
5. Update monitoring thresholds

### Data Integrity Issues
1. Stop data collection immediately
2. Validate existing data quality
3. Identify corruption sources
4. Implement data validation
5. Resume with enhanced checks