-- ============================================================================
-- 006_send_and_verify_runs.sql
-- Bitácora de Worker.Verify: comprobación de productos enviados contra la API del destino.
-- ============================================================================

BEGIN;

CREATE TABLE IF NOT EXISTS verify_runs (
    id            BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    target        VARCHAR(30)  NOT NULL,            -- ComprasParaguai | ComparaParaguai
    started_at    TIMESTAMPTZ  NOT NULL,
    finished_at   TIMESTAMPTZ,
    sample_size   INT          NOT NULL,
    found         INT          NOT NULL DEFAULT 0,
    price_match   INT          NOT NULL DEFAULT 0,
    stock_match   INT          NOT NULL DEFAULT 0,
    csv_path      VARCHAR(1000)
);

CREATE TABLE IF NOT EXISTS verify_results (
    run_id         BIGINT       NOT NULL REFERENCES verify_runs(id) ON DELETE CASCADE,
    code           VARCHAR(255) NOT NULL,
    name_pt        VARCHAR(255),
    found          BOOLEAN      NOT NULL,
    price_local    NUMERIC(12,2),
    price_api      NUMERIC(12,2),
    stock_local    INT,
    stock_api      NUMERIC(12,2),
    available_api  BOOLEAN,
    updated_at_api TIMESTAMPTZ,
    PRIMARY KEY (run_id, code)
);

CREATE INDEX IF NOT EXISTS ix_verify_runs_started ON verify_runs(started_at DESC);

COMMIT;

-- ============================================================================
-- send_runs: bitácora de Worker.Compras / Worker.Compara (una fila por corrida).
-- ============================================================================

BEGIN;

CREATE TABLE IF NOT EXISTS send_runs (
    id            BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    target        VARCHAR(30)  NOT NULL,            -- ComprasParaguai | ComparaParaguai
    mode          VARCHAR(20)  NOT NULL,            -- pending | all | sample | service
    started_at    TIMESTAMPTZ  NOT NULL,
    finished_at   TIMESTAMPTZ,
    total         INT          NOT NULL DEFAULT 0,
    sent          INT          NOT NULL DEFAULT 0,
    failed        INT          NOT NULL DEFAULT 0,
    error         VARCHAR(2000)
);

CREATE INDEX IF NOT EXISTS ix_send_runs_target_started ON send_runs(target, started_at DESC);

COMMIT;
