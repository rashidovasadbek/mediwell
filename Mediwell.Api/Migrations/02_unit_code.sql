

CREATE TABLE unit_code (
                           id              BIGSERIAL PRIMARY KEY,
                           code            TEXT NOT NULL UNIQUE,           -- takrorlanmaydi
                           receipt_id      BIGINT NOT NULL REFERENCES receipt(id),
                           physical_status TEXT NOT NULL DEFAULT 'in_stock'
                               CHECK (physical_status IN ('in_stock', 'shipped', 'returned', 'damaged')),
                           sales_channel   TEXT CHECK (sales_channel IN ('official', 'cash')),
                           created_at      TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS ix_unit_code_receipt ON unit_code(receipt_id);
CREATE INDEX IF NOT EXISTS ix_unit_code_physical ON unit_code(physical_status);