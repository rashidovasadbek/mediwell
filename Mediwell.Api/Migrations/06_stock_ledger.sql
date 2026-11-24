-- ============================================================
--  Sklad hisobi (markirovkasiz): guruhlar, dorilar, harakatlar jurnali
--  Qoldiq SAQLANMAYDI — har doim stock_movement'dan hisoblanadi.
--  Omborlar: 'nal' (naqd) va 'mediwell' (rasmiy).
-- ============================================================

-- 1. Guruhlar (MEDIWELL, ZEROMED, MW04JF)
CREATE TABLE product_group (
    id          BIGSERIAL PRIMARY KEY,
    code        TEXT NOT NULL UNIQUE,
    name        TEXT NOT NULL,
    sort_order  INTEGER NOT NULL DEFAULT 0,
    active      BOOLEAN NOT NULL DEFAULT TRUE,
    created_at  TIMESTAMPTZ NOT NULL DEFAULT now()
);

INSERT INTO product_group (code, name, sort_order) VALUES
    ('MEDIWELL', 'Mediwell',       1),
    ('ZEROMED',  'Zeromed Pharma', 2),
    ('MW04JF',   'MW-04 (JF)',     3);

-- 2. Guruh dorilari (bir dori bir nechta guruhda bo'lishi mumkin)
CREATE TABLE stock_product (
    id            BIGSERIAL PRIMARY KEY,
    group_id      BIGINT NOT NULL REFERENCES product_group(id),
    name          TEXT NOT NULL,
    perech_price  NUMERIC(14,2) CHECK (perech_price IS NULL OR perech_price >= 0),
    sort_order    INTEGER NOT NULL DEFAULT 0,
    active        BOOLEAN NOT NULL DEFAULT TRUE,
    created_at    TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT uq_stock_product_group_name UNIQUE (group_id, name)
);

-- 3. Harakatlar jurnali
CREATE TABLE stock_movement (
    id              BIGSERIAL PRIMARY KEY,
    movement_date   DATE NOT NULL,
    group_id        BIGINT NOT NULL REFERENCES product_group(id),
    product_id      BIGINT NOT NULL REFERENCES stock_product(id),
    movement_type   TEXT NOT NULL
        CHECK (movement_type IN ('opening','receipt','transfer','write_off','sale_perech','sale_nal')),
    from_warehouse  TEXT NULL CHECK (from_warehouse IN ('nal','mediwell')),
    to_warehouse    TEXT NULL CHECK (to_warehouse   IN ('nal','mediwell')),
    qty             INTEGER NOT NULL CHECK (qty > 0),
    price           NUMERIC(14,2) NULL CHECK (price IS NULL OR price >= 0),
    counterparty    TEXT,
    region          TEXT,
    agent           TEXT,
    doc_no          TEXT NULL,
    note            TEXT,
    created_by      BIGINT NULL REFERENCES app_user(id),
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at      TIMESTAMPTZ NULL,
    deleted_at      TIMESTAMPTZ NULL,

    -- Turga qarab ombor qoidalari
    CONSTRAINT ck_stock_movement_warehouses CHECK (
        (movement_type IN ('opening','receipt')
            AND to_warehouse IS NOT NULL AND from_warehouse IS NULL)
     OR (movement_type = 'transfer'
            AND from_warehouse IS NOT NULL AND to_warehouse IS NOT NULL
            AND from_warehouse <> to_warehouse)
     OR (movement_type = 'write_off'
            AND from_warehouse IS NOT NULL AND to_warehouse IS NULL)
     OR (movement_type = 'sale_perech'
            AND from_warehouse = 'mediwell' AND to_warehouse IS NULL)
     OR (movement_type = 'sale_nal'
            AND from_warehouse = 'nal' AND to_warehouse IS NULL)
    )
);

CREATE INDEX ix_stock_movement_group_product ON stock_movement(group_id, product_id) WHERE deleted_at IS NULL;
CREATE INDEX ix_stock_movement_date          ON stock_movement(movement_date)        WHERE deleted_at IS NULL;
CREATE INDEX ix_stock_movement_type          ON stock_movement(movement_type)        WHERE deleted_at IS NULL;
CREATE INDEX ix_stock_movement_doc_no        ON stock_movement(doc_no)               WHERE doc_no IS NOT NULL;

-- 4. Hujjat raqamlari hisoblagichi (PER-000123, NAL-000045, ...)
CREATE TABLE stock_doc_counter (
    prefix   TEXT PRIMARY KEY,
    last_no  INTEGER NOT NULL DEFAULT 0
);

INSERT INTO stock_doc_counter (prefix) VALUES
    ('OPN'), ('KIR'), ('KOC'), ('SPI'), ('PER'), ('NAL');

-- 5. Qoldiq: har (guruh, dori) uchun нал | mediwell | Жами
--    Harakati yo'q dorilar ham 0 bilan chiqadi.
CREATE VIEW v_stock_balance AS
SELECT
    p.group_id,
    p.id            AS product_id,
    p.name          AS product_name,
    p.sort_order,
    p.active,
    COALESCE(SUM(CASE WHEN m.to_warehouse   = 'nal'      THEN m.qty ELSE 0 END), 0)
  - COALESCE(SUM(CASE WHEN m.from_warehouse = 'nal'      THEN m.qty ELSE 0 END), 0) AS nal,
    COALESCE(SUM(CASE WHEN m.to_warehouse   = 'mediwell' THEN m.qty ELSE 0 END), 0)
  - COALESCE(SUM(CASE WHEN m.from_warehouse = 'mediwell' THEN m.qty ELSE 0 END), 0) AS mediwell,
    COALESCE(SUM(CASE WHEN m.to_warehouse   IS NOT NULL  THEN m.qty ELSE 0 END), 0)
  - COALESCE(SUM(CASE WHEN m.from_warehouse IS NOT NULL  THEN m.qty ELSE 0 END), 0) AS total
FROM stock_product p
LEFT JOIN stock_movement m
       ON m.product_id = p.id
      AND m.deleted_at IS NULL
GROUP BY p.group_id, p.id, p.name, p.sort_order, p.active;
