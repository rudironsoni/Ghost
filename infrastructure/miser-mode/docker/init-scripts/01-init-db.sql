-- Ghost Platform Database Initialization
-- Ultra Miser Mode - Single Node Deployment
-- This script creates the initial database schema

-- Enable required extensions
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS "pgcrypto";

-- Create schema
CREATE SCHEMA IF NOT EXISTS ghost;

-- ============================================================================
-- OUTBOX PATTERN TABLES
-- ============================================================================

-- Outbox messages for event publishing
CREATE TABLE IF NOT EXISTS outbox_messages (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    message_id UUID NOT NULL,
    message_type VARCHAR(255) NOT NULL,
    exchange VARCHAR(255) NOT NULL,
    routing_key VARCHAR(255) NOT NULL,
    payload JSONB NOT NULL,
    headers JSONB DEFAULT '{}',
    status VARCHAR(50) DEFAULT 'Pending',
    retry_count INT DEFAULT 0,
    created_at TIMESTAMPTZ DEFAULT NOW(),
    scheduled_at TIMESTAMPTZ,
    processed_at TIMESTAMPTZ,
    error_message TEXT,
    CONSTRAINT uq_outbox_message_id UNIQUE (message_id)
);

-- Partition outbox by date for performance
CREATE TABLE IF NOT EXISTS outbox_messages_2024_01 
    PARTITION OF outbox_messages 
    FOR VALUES FROM ('2024-01-01') TO ('2024-02-01');

-- Create indexes
CREATE INDEX IF NOT EXISTS idx_outbox_status_scheduled 
    ON outbox_messages(status, scheduled_at) 
    WHERE status = 'Pending';

CREATE INDEX IF NOT EXISTS idx_outbox_created_at 
    ON outbox_messages(created_at DESC);

-- Inbox for idempotent message processing
CREATE TABLE IF NOT EXISTS inbox_messages (
    message_id UUID PRIMARY KEY,
    consumer_name VARCHAR(255) NOT NULL,
    processed_at TIMESTAMPTZ DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_inbox_consumer 
    ON inbox_messages(consumer_name, processed_at);

-- ============================================================================
-- CORE APPLICATION TABLES
-- ============================================================================

-- Sessions table
CREATE TABLE IF NOT EXISTS sessions (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    external_id VARCHAR(255),
    browser_type VARCHAR(50) NOT NULL,
    fingerprint_profile_id UUID,
    proxy_id UUID,
    status VARCHAR(50) NOT NULL DEFAULT 'available',
    platform VARCHAR(100),
    created_at TIMESTAMPTZ DEFAULT NOW(),
    expires_at TIMESTAMPTZ NOT NULL,
    last_used_at TIMESTAMPTZ,
    health_status VARCHAR(50) DEFAULT 'healthy',
    metadata JSONB DEFAULT '{}',
    instance_id VARCHAR(255),
    deleted_at TIMESTAMPTZ
);

CREATE INDEX IF NOT EXISTS idx_sessions_status_platform 
    ON sessions(status, platform);

CREATE INDEX IF NOT EXISTS idx_sessions_expires_at 
    ON sessions(expires_at) 
    WHERE status = 'available';

CREATE INDEX IF NOT EXISTS idx_sessions_instance_id 
    ON sessions(instance_id);

-- Jobs table
CREATE TABLE IF NOT EXISTS jobs (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    external_id VARCHAR(255),
    platform VARCHAR(100) NOT NULL,
    title TEXT NOT NULL,
    company VARCHAR(255),
    location VARCHAR(255),
    description TEXT,
    url TEXT,
    salary_min DECIMAL(12,2),
    salary_max DECIMAL(12,2),
    salary_currency VARCHAR(3),
    employment_type VARCHAR(50),
    remote_policy VARCHAR(50),
    skills TEXT[],
    posted_at TIMESTAMPTZ,
    scraped_at TIMESTAMPTZ DEFAULT NOW(),
    raw_data JSONB,
    fingerprint_used UUID,
    proxy_used UUID,
    session_id UUID,
    search_criteria_id UUID,
    CONSTRAINT unique_platform_external_id UNIQUE(platform, external_id)
);

CREATE INDEX IF NOT EXISTS idx_jobs_platform_posted 
    ON jobs(platform, posted_at DESC);

CREATE INDEX IF NOT EXISTS idx_jobs_scraped_at 
    ON jobs(scraped_at);

CREATE INDEX IF NOT EXISTS idx_jobs_location 
    ON jobs USING GIN(location gin_trgm_ops);

CREATE INDEX IF NOT EXISTS idx_jobs_skills 
    ON jobs USING GIN(skills);

CREATE INDEX IF NOT EXISTS idx_jobs_raw_data 
    ON jobs USING GIN(raw_data);

-- Search criteria table
CREATE TABLE IF NOT EXISTS search_criteria (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name VARCHAR(255),
    keywords TEXT[],
    location VARCHAR(255),
    radius_km INT,
    job_type VARCHAR(50),
    experience_level VARCHAR(50),
    salary_min DECIMAL(12,2),
    salary_max DECIMAL(12,2),
    platforms TEXT[],
    is_active BOOLEAN DEFAULT true,
    schedule_type VARCHAR(50),
    cron_expression VARCHAR(100),
    created_by VARCHAR(255),
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW()
);

-- Platform configurations
CREATE TABLE IF NOT EXISTS platform_configs (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    platform VARCHAR(100) UNIQUE NOT NULL,
    base_url TEXT NOT NULL,
    api_version VARCHAR(50),
    rate_limits JSONB NOT NULL DEFAULT '{}',
    auth_config JSONB,
    selectors JSONB,
    anti_detection_rules JSONB,
    is_enabled BOOLEAN DEFAULT true,
    circuit_breaker_threshold INT DEFAULT 5,
    circuit_breaker_timeout_secs INT DEFAULT 300,
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW()
);

-- Proxies table
CREATE TABLE IF NOT EXISTS proxies (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    type VARCHAR(20) NOT NULL CHECK (type IN ('http', 'socks5', 'socks4')),
    host INET NOT NULL,
    port INT NOT NULL,
    username VARCHAR(255),
    encrypted_password TEXT,
    country_code CHAR(2),
    region VARCHAR(100),
    city VARCHAR(100),
    isp VARCHAR(255),
    is_rotating BOOLEAN DEFAULT false,
    is_residential BOOLEAN DEFAULT false,
    health_status VARCHAR(50) DEFAULT 'unknown',
    success_rate DECIMAL(5,2) DEFAULT 0.00,
    avg_response_time_ms INT,
    last_health_check TIMESTAMPTZ,
    failure_count INT DEFAULT 0,
    total_requests INT DEFAULT 0,
    created_at TIMESTAMPTZ DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_proxies_country 
    ON proxies(country_code);

CREATE INDEX IF NOT EXISTS idx_proxies_health 
    ON proxies(health_status, success_rate DESC);

-- Fingerprint profiles
CREATE TABLE IF NOT EXISTS fingerprint_profiles (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    profile_id VARCHAR(255) UNIQUE NOT NULL,
    type VARCHAR(50) NOT NULL,
    user_agent TEXT NOT NULL,
    viewport_width INT,
    viewport_height INT,
    platform VARCHAR(50),
    vendor VARCHAR(100),
    language VARCHAR(10),
    hardware_concurrency INT,
    device_memory INT,
    max_touch_points INT,
    screen_data JSONB,
    webgl_data JSONB,
    canvas_data JSONB,
    fonts TEXT[],
    plugins JSONB,
    timezone_id VARCHAR(100),
    timezone_offset INT,
    geolocation JSONB,
    web_rtc_config JSONB,
    usage_count INT DEFAULT 0,
    last_used_at TIMESTAMPTZ,
    created_at TIMESTAMPTZ DEFAULT NOW()
);

-- ============================================================================
-- METRICS TABLES (Time-series)
-- ============================================================================

CREATE TABLE IF NOT EXISTS session_metrics (
    time TIMESTAMPTZ NOT NULL,
    session_id UUID NOT NULL,
    metric_name VARCHAR(100) NOT NULL,
    metric_value DOUBLE PRECISION NOT NULL,
    tags JSONB
);

CREATE INDEX IF NOT EXISTS idx_session_metrics_time 
    ON session_metrics(time DESC);

CREATE INDEX IF NOT EXISTS idx_session_metrics_session 
    ON session_metrics(session_id, time DESC);

-- ============================================================================
-- CIRCUIT BREAKER STATE
-- ============================================================================

CREATE TABLE IF NOT EXISTS circuit_breaker_states (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    platform VARCHAR(100) UNIQUE NOT NULL,
    state VARCHAR(50) NOT NULL DEFAULT 'Closed',
    failure_count INT DEFAULT 0,
    success_count INT DEFAULT 0,
    last_failure_at TIMESTAMPTZ,
    last_success_at TIMESTAMPTZ,
    opened_at TIMESTAMPTZ,
    next_attempt_at TIMESTAMPTZ,
    updated_at TIMESTAMPTZ DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_circuit_breaker_platform 
    ON circuit_breaker_states(platform);

CREATE INDEX IF NOT EXISTS idx_circuit_breaker_state 
    ON circuit_breaker_states(state);

-- ============================================================================
-- CLEANUP FUNCTION
-- ============================================================================

CREATE OR REPLACE FUNCTION cleanup_old_data() RETURNS void AS $$
BEGIN
    -- Clean inbox messages older than 7 days
    DELETE FROM inbox_messages 
    WHERE processed_at < NOW() - INTERVAL '7 days';
    
    -- Clean processed outbox messages older than 30 days
    DELETE FROM outbox_messages 
    WHERE status = 'Sent' 
    AND processed_at < NOW() - INTERVAL '30 days';
    
    -- Clean old metrics (keep 90 days)
    DELETE FROM session_metrics 
    WHERE time < NOW() - INTERVAL '90 days';
    
    -- Clean expired sessions
    DELETE FROM sessions 
    WHERE expires_at < NOW() - INTERVAL '7 days';
END;
$$ LANGUAGE plpgsql;

-- ============================================================================
-- SEED DATA
-- ============================================================================

-- Insert default platform configurations
INSERT INTO platform_configs (platform, base_url, rate_limits, is_enabled)
VALUES 
    ('LinkedIn', 'https://www.linkedin.com', '{"requests_per_minute": 10, "concurrent_sessions": 3}', true),
    ('Indeed', 'https://www.indeed.com', '{"requests_per_minute": 15, "concurrent_sessions": 5}', true),
    ('Google', 'https://www.google.com', '{"requests_per_minute": 20, "concurrent_sessions": 5}', true),
    ('Glassdoor', 'https://www.glassdoor.com', '{"requests_per_minute": 10, "concurrent_sessions": 3}', false)
ON CONFLICT (platform) DO NOTHING;

-- Insert initial circuit breaker states
INSERT INTO circuit_breaker_states (platform, state)
SELECT platform, 'Closed'
FROM platform_configs
WHERE is_enabled = true
ON CONFLICT (platform) DO NOTHING;

-- Grant permissions
GRANT ALL PRIVILEGES ON ALL TABLES IN SCHEMA public TO ghost;
GRANT ALL PRIVILEGES ON ALL SEQUENCES IN SCHEMA public TO ghost;

-- Success message
SELECT 'Database initialization completed successfully' AS status;