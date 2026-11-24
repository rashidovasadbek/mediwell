#!/usr/bin/env bash
# ============================================================
#  Mediwell — serverni BIR MARTALIK tayyorlash (qayta ishga tushirish xavfsiz)
#  Ishlatish:  cd ~/mediwell-app && bash deploy/server-setup.sh
#  Boshqa servislar, eski bazalar (mediwell_db, mediwell_test) va nginx "default" saytiga TEGMAYDI.
# ============================================================
set -euo pipefail

REPO_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
API_DIR=/opt/mediwell/api
PANEL_DIR=/var/www/mediwell-panel
CONFIG="$API_DIR/appsettings.Production.json"
DB_NAME=mediwell_prod
DB_USER=mediwell_prod
HTPASSWD=/etc/nginx/.htpasswd-mediwell
NGINX_SITE=/etc/nginx/sites-available/mediwell.uz
SERVICE=/etc/systemd/system/mediwell-api.service
RUN_USER="$(id -un)"

step() { echo; echo "==> $*"; }
info() { echo "    $*"; }
warn() { echo "    [!] $*"; }
fail() { echo; echo "XATO: $*" >&2; exit 1; }

if [[ "$RUN_USER" == "root" ]]; then
    fail "Skriptni root sifatida emas, oddiy foydalanuvchi (asadbek) sifatida ishga tushiring. sudo kerak joyda o'zi so'raydi."
fi

# ------------------------------------------------------------
step "1/7 Kerakli dasturlarni tekshirish"
# ------------------------------------------------------------
command -v dotnet >/dev/null || fail "dotnet topilmadi. .NET 8 SDK o'rnating."
dotnet --list-sdks | grep -q '^8\.' || fail ".NET 8 SDK topilmadi (dotnet --list-sdks). O'rnating va qayta urinib ko'ring."
info "dotnet: $(dotnet --list-sdks | grep '^8\.' | tail -1)"

command -v node >/dev/null || fail "node topilmadi. Node.js 18+ o'rnating."
NODE_MAJOR="$(node -v | sed -E 's/^v([0-9]+).*/\1/')"
(( NODE_MAJOR >= 18 )) || fail "Node.js versiyasi $(node -v) — kamida 18 kerak."
info "node: $(node -v)"

for cmd in npm nginx certbot psql openssl python3 rsync curl git; do
    command -v "$cmd" >/dev/null || fail "'$cmd' topilmadi. O'rnating va qayta urinib ko'ring."
done
info "npm, nginx, certbot, psql, openssl, python3, rsync, curl, git — bor"

DOTNET_BIN="$(readlink -f "$(command -v dotnet)")"
info "dotnet yo'li: $DOTNET_BIN"

# ------------------------------------------------------------
step "2/7 htpasswd (apache2-utils)"
# ------------------------------------------------------------
if command -v htpasswd >/dev/null; then
    info "htpasswd bor"
else
    info "apache2-utils o'rnatilmoqda..."
    sudo apt-get update -qq
    sudo apt-get install -y -qq apache2-utils
fi

# ------------------------------------------------------------
step "3/7 Papkalar: $API_DIR, $PANEL_DIR"
# ------------------------------------------------------------
sudo mkdir -p "$API_DIR" "$PANEL_DIR"
sudo chown "$RUN_USER":"$RUN_USER" /opt/mediwell "$API_DIR"
sudo chown "$RUN_USER":www-data "$PANEL_DIR"
sudo chmod 755 "$PANEL_DIR"
info "tayyor (egasi: $RUN_USER)"

# ------------------------------------------------------------
step "4/7 Baza ($DB_NAME) va $CONFIG"
# ------------------------------------------------------------
# cd / — postgres foydalanuvchisi joriy papkaga kira olmasa ogohlantirish chiqmasin
pg_q() { (cd / && sudo -u postgres psql -v ON_ERROR_STOP=1 -tAc "$1"); }

ROLE_EXISTS="$(pg_q "SELECT 1 FROM pg_roles WHERE rolname = '$DB_USER'")"
DB_EXISTS="$(pg_q "SELECT 1 FROM pg_database WHERE datname = '$DB_NAME'")"

if [[ -f "$CONFIG" ]]; then
    info "$CONFIG mavjud — tegmayman"
    [[ "$ROLE_EXISTS" == "1" ]] || fail "Config bor, lekin '$DB_USER' roli yo'q. Qo'lda tekshiring."
else
    [[ "$ROLE_EXISTS" != "1" ]] || fail "'$DB_USER' roli bor, lekin $CONFIG yo'q (parol noma'lum).
    Variantlar: config'ni qo'lda tiklang yoki rol parolini almashtirib config'ni yarating:
      sudo -u postgres psql -c \"ALTER ROLE $DB_USER PASSWORD 'yangi_parol'\""

    # Birinchi admin (panelga kirish uchun) — interaktiv
    echo
    info "Panelning birinchi admin foydalanuvchisi (API birinchi ishga tushganda yaratiladi)."
    read -r -p "    Admin login [admin]: " ADMIN_LOGIN
    ADMIN_LOGIN="${ADMIN_LOGIN:-admin}"
    while true; do
        read -r -s -p "    Admin paroli (kamida 8 belgi): " ADMIN_PW; echo
        read -r -s -p "    Parolni takrorlang: " ADMIN_PW2; echo
        if [[ ${#ADMIN_PW} -lt 8 ]]; then warn "Parol juda qisqa."; continue; fi
        if [[ "$ADMIN_PW" != "$ADMIN_PW2" ]]; then warn "Parollar mos emas."; continue; fi
        break
    done

    DB_PASSWORD="$(openssl rand -hex 24)"
    JWT_KEY="$(openssl rand -base64 48 | tr -d '\n')"

    info "PostgreSQL roli '$DB_USER' yaratilmoqda..."
    pg_q "CREATE ROLE $DB_USER LOGIN PASSWORD '$DB_PASSWORD'" >/dev/null
    if [[ "$DB_EXISTS" != "1" ]]; then
        info "Baza '$DB_NAME' yaratilmoqda (egasi: $DB_USER)..."
        (cd / && sudo -u postgres createdb -O "$DB_USER" "$DB_NAME")
        pg_q "REVOKE CONNECT ON DATABASE $DB_NAME FROM PUBLIC" >/dev/null
    else
        warn "Baza '$DB_NAME' allaqachon bor — egasini o'zgartirmayman."
    fi

    info "$CONFIG yozilmoqda (chmod 600)..."
    umask 077
    DB_PASSWORD="$DB_PASSWORD" JWT_KEY="$JWT_KEY" ADMIN_LOGIN="$ADMIN_LOGIN" ADMIN_PW="$ADMIN_PW" \
    DB_NAME="$DB_NAME" DB_USER="$DB_USER" python3 - "$CONFIG" <<'PY'
import json, os, sys
cfg = {
    "Logging": {"LogLevel": {"Default": "Information", "Microsoft.AspNetCore": "Warning"}},
    "AllowedHosts": "mediwell.uz;www.mediwell.uz;localhost;127.0.0.1",
    "ConnectionStrings": {
        "Default": f"Host=127.0.0.1;Port=5432;Database={os.environ['DB_NAME']};"
                   f"Username={os.environ['DB_USER']};Password={os.environ['DB_PASSWORD']}"
    },
    "Auth": {
        "JwtKey": os.environ["JWT_KEY"],
        "Issuer": "mediwell",
        "TokenHours": 12,
        "BootstrapAdmin": {
            "Login": os.environ["ADMIN_LOGIN"],
            "Password": os.environ["ADMIN_PW"],
            "FullName": "Administrator",
        },
    },
}
with open(sys.argv[1], "w", encoding="utf-8") as f:
    json.dump(cfg, f, ensure_ascii=False, indent=2)
PY
    umask 022
    chmod 600 "$CONFIG"
    unset ADMIN_PW ADMIN_PW2 DB_PASSWORD JWT_KEY
    info "tayyor"
fi

# ------------------------------------------------------------
step "5/7 Basic auth: $HTPASSWD"
# ------------------------------------------------------------
if sudo test -f "$HTPASSWD"; then
    info "mavjud — tegmayman (yangi foydalanuvchi: sudo htpasswd $HTPASSWD <login>)"
else
    read -r -p "    Saytga kirish (basic auth) login: " BASIC_USER
    [[ -n "$BASIC_USER" ]] || fail "Login bo'sh bo'lmasligi kerak."
    sudo htpasswd -c "$HTPASSWD" "$BASIC_USER"
    sudo chown root:www-data "$HTPASSWD"
    sudo chmod 640 "$HTPASSWD"
    info "tayyor"
fi

# ------------------------------------------------------------
step "6/7 Nginx: $NGINX_SITE"
# ------------------------------------------------------------
if sudo test -f "$NGINX_SITE"; then
    if sudo cmp -s "$REPO_DIR/deploy/nginx/mediwell.uz.conf" "$NGINX_SITE"; then
        info "mavjud va repo bilan bir xil"
    else
        warn "mavjud va repo'dagidan farq qiladi (masalan, certbot SSL qo'shgan) — USTIDAN YOZMAYMAN."
        warn "Farqni ko'rish: diff $REPO_DIR/deploy/nginx/mediwell.uz.conf $NGINX_SITE"
    fi
else
    sudo cp "$REPO_DIR/deploy/nginx/mediwell.uz.conf" "$NGINX_SITE"
    info "ko'chirildi"
fi
sudo ln -sfn "$NGINX_SITE" /etc/nginx/sites-enabled/mediwell.uz

OTHERS="$(grep -lE 'server_name[^;]*\bmediwell\.uz' /etc/nginx/sites-enabled/* 2>/dev/null | grep -v '/mediwell.uz$' || true)"
if [[ -n "$OTHERS" ]]; then
    warn "Boshqa nginx saytlarida ham mediwell.uz bor (to'qnashishi mumkin):"
    echo "$OTHERS" | sed 's/^/        /'
fi

sudo nginx -t
sudo systemctl reload nginx
info "nginx qayta yuklandi"

# ------------------------------------------------------------
step "7/7 systemd: mediwell-api"
# ------------------------------------------------------------
sed -e "s|^User=.*|User=$RUN_USER|" \
    -e "s|/usr/bin/dotnet|$DOTNET_BIN|" \
    "$REPO_DIR/deploy/mediwell-api.service" | sudo tee "$SERVICE" >/dev/null
sudo systemctl daemon-reload
sudo systemctl enable mediwell-api >/dev/null 2>&1
info "o'rnatildi va yoqildi (hali ishga tushirilmadi — avval deploy.sh)"

echo
echo "============================================================"
echo " Server tayyor. Keyingi qadamlar:"
echo "   1) bash deploy/deploy.sh"
echo "   2) sudo certbot --nginx -d mediwell.uz -d www.mediwell.uz --redirect"
echo "   3) https://mediwell.uz va https://mediwell.uz/api/health ni tekshiring"
echo "============================================================"
