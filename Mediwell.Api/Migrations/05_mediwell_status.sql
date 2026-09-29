-- Naqd/rasmiy holat: har agregat va ichki kod naqdda yoki Mediwellga prixod (rasmiy) bo'lganini bildiradi
-- Boshda hammasi 'cash' (naqd, nalichida). Mediwellga prixod bo'lganda 'official' (rasmiy, Asl Belgida)

-- Agregatlarga naqd/rasmiy holati
ALTER TABLE aggregate
    ADD COLUMN IF NOT EXISTS official_status TEXT NOT NULL DEFAULT 'cash'
    CHECK (official_status IN ('cash', 'official'));

-- Ichki kodlarga naqd/rasmiy holati
ALTER TABLE unit_code
    ADD COLUMN IF NOT EXISTS official_status TEXT NOT NULL DEFAULT 'cash'
    CHECK (official_status IN ('cash', 'official'));

CREATE INDEX IF NOT EXISTS ix_aggregate_official ON aggregate(official_status);
CREATE INDEX IF NOT EXISTS ix_unit_code_official ON unit_code(official_status);