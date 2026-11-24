#!/usr/bin/env bash
# ============================================================
#  Mediwell — har yangilanishda: kodni olish, yig'ish, ishga tushirish
#  Ishlatish:  cd ~/mediwell-app && bash deploy/deploy.sh
#  Avval bir marta: bash deploy/server-setup.sh
# ============================================================
set -euo pipefail

# Hammasi funksiya ichida: "git pull" shu faylni o'zgartirsa ham bash eski nusxani oxirigacha bajaradi.
main() {
    local REPO_DIR API_DIR PANEL_DIR CONFIG HEALTH
    REPO_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
    API_DIR=/opt/mediwell/api
    PANEL_DIR=/var/www/mediwell-panel
    CONFIG="$API_DIR/appsettings.Production.json"
    HEALTH=http://127.0.0.1:5048/health

    step() { echo; echo "==> $*"; }
    fail() { echo; echo "XATO: $*" >&2; exit 1; }

    [[ -f "$CONFIG" ]] || fail "$CONFIG topilmadi. Avval: bash deploy/server-setup.sh"
    cd "$REPO_DIR"

    step "1/5 git pull"
    git pull --ff-only
    echo "    commit: $(git log --oneline -1)"

    step "2/5 API: dotnet publish -> $API_DIR"
    # appsettings.Production.json publish'da yo'q (csproj: CopyToPublishDirectory=Never) — serverdagisi saqlanadi
    dotnet publish Mediwell.Api/Mediwell.Api.csproj -c Release -o "$API_DIR" --nologo -v quiet
    [[ -f "$CONFIG" ]] || fail "$CONFIG yo'qolib qoldi!"

    step "3/5 Panel: npm ci && npm run build"
    (
        cd Mediwell.Web
        npm ci --no-audit --no-fund
        npm run build
    )

    step "4/5 Panel fayllari -> $PANEL_DIR"
    rsync -a --delete Mediwell.Web/dist/ "$PANEL_DIR/"

    step "5/5 mediwell-api qayta ishga tushirilmoqda"
    sudo systemctl restart mediwell-api

    echo -n "    /health kutilmoqda"
    local i
    for i in $(seq 1 10); do
        if curl -fsS "$HEALTH" >/dev/null 2>&1; then
            echo
            echo "    $(curl -fsS "$HEALTH")"
            echo
            echo "MUVAFFAQIYATLI: https://mediwell.uz yangilandi."
            if python3 -c "import json,sys; sys.exit(0 if json.load(open(sys.argv[1]))['Auth'].get('BootstrapAdmin',{}).get('Password') else 1)" "$CONFIG" 2>/dev/null; then
                echo
                echo "[!] Panelga kirib admin parolini almashtirgandan keyin $CONFIG dan"
                echo "    \"BootstrapAdmin\" bo'limini olib tashlang:  nano $CONFIG"
            fi
            return 0
        fi
        echo -n "."
        sleep 1
    done

    echo
    echo "XATO: API 10 soniyada javob bermadi ($HEALTH). Oxirgi loglar:" >&2
    sudo journalctl -u mediwell-api -n 50 --no-pager >&2 || true
    exit 1
}

main "$@"
exit 0
