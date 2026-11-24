-- ============================================================
--  Auth: app_user'ga login + parol xeshi
--  Rollar 01_schema'dagi CHECK bo'yicha: admin, warehouse (skladchi), ...
-- ============================================================

ALTER TABLE app_user
    ADD COLUMN login          TEXT,
    ADD COLUMN password_hash  TEXT,
    ADD COLUMN last_login_at  TIMESTAMPTZ;

-- Login katta-kichik harfga sezgir emas va takrorlanmaydi
CREATE UNIQUE INDEX ux_app_user_login ON app_user (lower(login)) WHERE login IS NOT NULL;
