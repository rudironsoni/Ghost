-- Event store for Orleans
CREATE TABLE IF NOT EXISTS OrleansEventStore (
    GrainId VARCHAR(250) NOT NULL,
    GrainType VARCHAR(500) NOT NULL,
    Version BIGINT NOT NULL,
    EventType VARCHAR(500) NOT NULL,
    EventData JSONB NOT NULL,
    Timestamp TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    TenantId UUID,
    PRIMARY KEY (GrainId, Version)
);

-- Snapshots
CREATE TABLE IF NOT EXISTS OrleansGrainSnapshots (
    GrainId VARCHAR(250) NOT NULL PRIMARY KEY,
    GrainType VARCHAR(500) NOT NULL,
    Version BIGINT NOT NULL,
    SnapshotData JSONB NOT NULL,
    Timestamp TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Endpoint registry
CREATE TABLE IF NOT EXISTS EndpointRegistry (
    EndpointId VARCHAR(250) PRIMARY KEY,
    PluginId VARCHAR(250) NOT NULL,
    Version VARCHAR(50) NOT NULL,
    DisplayName VARCHAR(500) NOT NULL,
    Capability VARCHAR(50) NOT NULL,
    InputSchema JSONB NOT NULL,
    OutputSchema JSONB NOT NULL,
    DeliveryModes JSONB NOT NULL,
    Limits JSONB NOT NULL,
    SupportsArtifacts BOOLEAN NOT NULL DEFAULT FALSE,
    IsActive BOOLEAN NOT NULL DEFAULT TRUE,
    CreatedAt TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Scrape runs read model
CREATE TABLE IF NOT EXISTS ScrapeRunReadModels (
    RunId VARCHAR(250) PRIMARY KEY,
    EndpointId VARCHAR(250) NOT NULL REFERENCES EndpointRegistry(EndpointId),
    TenantId UUID NOT NULL,
    Status VARCHAR(50) NOT NULL,
    Mode VARCHAR(50) NOT NULL,
    WorkerId VARCHAR(250),
    ItemsDiscovered INTEGER NOT NULL DEFAULT 0,
    ItemsDelivered INTEGER NOT NULL DEFAULT 0,
    ArtifactsCaptured INTEGER NOT NULL DEFAULT 0,
    StartedAt TIMESTAMPTZ NOT NULL,
    CompletedAt TIMESTAMPTZ,
    ErrorMessage TEXT,
    DeliveryConfig JSONB,
    ResultLocation TEXT
);

-- Results (sharded/partitioned for scale)
CREATE TABLE IF NOT EXISTS ScrapeResults (
    Id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    RunId VARCHAR(250) NOT NULL REFERENCES ScrapeRunReadModels(RunId),
    ItemId VARCHAR(250) NOT NULL,
    Data JSONB NOT NULL,
    DiscoveredAt TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS IX_ScrapeResults_RunId ON ScrapeResults(RunId);

-- Artifacts metadata
CREATE TABLE IF NOT EXISTS ArtifactMetadata (
    Id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    RunId VARCHAR(250) NOT NULL REFERENCES ScrapeRunReadModels(RunId),
    ItemId VARCHAR(250) NOT NULL,
    ArtifactType VARCHAR(50) NOT NULL,
    StorageUri TEXT NOT NULL,
    Hash VARCHAR(64) NOT NULL,
    SizeBytes BIGINT,
    CapturedAt TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS IX_ArtifactMetadata_RunId ON ArtifactMetadata(RunId);

-- Idempotency keys
CREATE TABLE IF NOT EXISTS IdempotencyKeys (
    Key VARCHAR(500) PRIMARY KEY,
    RunId VARCHAR(250) NOT NULL,
    ExpiresAt TIMESTAMPTZ NOT NULL,
    CreatedAt TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Tenant quotas
CREATE TABLE IF NOT EXISTS TenantQuotas (
    TenantId UUID PRIMARY KEY,
    EndpointId VARCHAR(250),
    MaxConcurrentRuns INTEGER NOT NULL DEFAULT 5,
    DailyRunLimit INTEGER NOT NULL DEFAULT 1000,
    DailyRunCount INTEGER NOT NULL DEFAULT 0,
    LastResetDate DATE NOT NULL DEFAULT CURRENT_DATE
);
