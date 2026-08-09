-- ============================================================
--  MEDIWELL — Faza 1: Sklad yadrosi (schema)
-- ============================================================

-- 1. Dorilar katalogi
CREATE TABLE product (
                         id                BIGSERIAL PRIMARY KEY,
                         name              TEXT NOT NULL,
                         manufacturer      TEXT,
                         nominal_capacity  INTEGER NOT NULL DEFAULT 0,
                         gtin              TEXT,
                         active            BOOLEAN NOT NULL DEFAULT TRUE,
                         created_at        TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE INDEX idx_product_name ON product(name);

-- 2. Tizim foydalanuvchilari
CREATE TABLE app_user (
                          id           BIGSERIAL PRIMARY KEY,
                          full_name    TEXT NOT NULL,
                          role         TEXT NOT NULL
                              CHECK (role IN ('admin','accountant','warehouse','manager','mp')),
                          telegram_id  BIGINT,
                          active       BOOLEAN NOT NULL DEFAULT TRUE,
                          created_at   TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE INDEX idx_app_user_telegram ON app_user(telegram_id);

-- 3. Kirim (partiya = seriya)
CREATE TABLE receipt (
                         id             BIGSERIAL PRIMARY KEY,
                         product_id     BIGINT NOT NULL REFERENCES product(id),
                         series         TEXT NOT NULL,
                         expiry_date    DATE NOT NULL,
                         supplier       TEXT,
                         entry_method   TEXT CHECK (entry_method IN ('file','manual','scan')),
                         received_at    DATE NOT NULL DEFAULT CURRENT_DATE,
                         note           TEXT,
                         created_by     BIGINT REFERENCES app_user(id),
                         created_at     TIMESTAMPTZ NOT NULL DEFAULT now(),
                         UNIQUE (product_id, series)
);
CREATE INDEX idx_receipt_product ON receipt(product_id);
CREATE INDEX idx_receipt_series ON receipt(series);

-- 4. Agregat (orginal karobka) — YURAK
CREATE TABLE aggregate (
                           id               BIGSERIAL PRIMARY KEY,
                           code             TEXT NOT NULL UNIQUE,
                           receipt_id       BIGINT NOT NULL REFERENCES receipt(id),
                           actual_count     INTEGER NOT NULL,
                           arrival_status   TEXT NOT NULL DEFAULT 'queued'
                               CHECK (arrival_status IN ('queued','arrived')),
                           arrival_firm     TEXT,
                           physical_status  TEXT NOT NULL DEFAULT 'in_stock'
                               CHECK (physical_status IN ('in_stock','reserved','shipped','returned','damaged')),
                           sales_channel    TEXT CHECK (sales_channel IN ('official','cash')),
                           is_opened        BOOLEAN NOT NULL DEFAULT FALSE,
                           created_at       TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE INDEX idx_aggregate_receipt ON aggregate(receipt_id);
CREATE INDEX idx_aggregate_physical ON aggregate(physical_status);
CREATE INDEX idx_aggregate_arrival ON aggregate(arrival_status);

-- 5. Audit log (harakatlar tarixi, append-only)
CREATE TABLE code_event (
                            id             BIGSERIAL PRIMARY KEY,
                            aggregate_id   BIGINT NOT NULL REFERENCES aggregate(id),
                            event_type     TEXT NOT NULL
                                CHECK (event_type IN ('created','arrival_changed','status_changed','count_corrected','shipped','returned')),
                            old_value      TEXT,
                            new_value      TEXT,
                            reason         TEXT,
                            performed_by   BIGINT REFERENCES app_user(id),
                            created_at     TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE INDEX idx_code_event_aggregate ON code_event(aggregate_id);
CREATE INDEX idx_code_event_created ON code_event(created_at);

-- Qoldiq (real-time view)
CREATE VIEW v_stock AS
SELECT
    p.id                AS product_id,
    p.name              AS product_name,
    r.id                AS receipt_id,
    r.series            AS series,
    r.expiry_date       AS expiry_date,
    a.arrival_status    AS arrival_status,
    COUNT(a.id)         AS box_count,
    SUM(a.actual_count) AS unit_count
FROM aggregate a
         JOIN receipt r ON a.receipt_id = r.id
         JOIN product p ON r.product_id = p.id
WHERE a.physical_status = 'in_stock'
GROUP BY p.id, p.name, r.id, r.series, r.expiry_date, a.arrival_status
ORDER BY p.name, r.expiry_date;