# Vazifa: Loyihani mediwell.uz ga chiqarish (deploy)

> Bu promptni **sklad moduli (docs/sklad-modul-prompt.md) tugagandan keyin** ishlat.
> Maqsad: brauzerda `https://mediwell.uz` ni ochsam — parol so'rasin, kirgandan keyin panel to'liq ishlasin.

## Hozirgi holat (tekshirilgan)
- Domen: `mediwell.uz` → Cloudflare DNS (proxy O'CHIQ, "DNS only") → server `193.180.209.245`. `www`, `api`, `bot`, `panel` A-yozuvlari ham shu IP'ga.
- Server: Webdock VPS, Ubuntu 22.04, user `asadbek` (sudo bor, SSH faqat parol bilan).
- O'rnatilgan: nginx 1.18 (faqat standart `default` sayt, 80-port), PostgreSQL 18 (faqat 127.0.0.1:5432), .NET 8 SDK, Node.js 20, certbot.
- ufw yoqilgan: 22, 80, 443 ochiq.
- Serverda boshqa servislar ishlayapti (farmbot, markir :8095, sprinthealthbot :8090, mw04bot, zeromedbot, pzdg_bot, talaba-express, ngrok) — ULARGA TEGMA. 5048 port bo'sh.
- Serverda eski `mediwell_db` va `mediwell_test` bazalari bor — ularga DbUp jurnalisiz qo'lda schema qo'yilgan. ULARGA TEGMA.
- GitHub: `https://github.com/rashidovasadbek/mediwell.git` (private bo'lishi mumkin).

## Arxitektura (o'zgartirma)
```
https://mediwell.uz (www → mediwell.uz redirect)
  Nginx 443 + Let's Encrypt SSL + basic auth (vaqtincha login)
    /       → /var/www/mediwell-panel   (Vue build, SPA: try_files ... /index.html)
    /api/   → http://127.0.0.1:5048/    (prefiks olib tashlanadi; .NET API, systemd servis)
                 └─ PostgreSQL: YANGI toza baza `mediwell_prod`, alohida user `mediwell_prod`
```
- Panel va API bitta domenda → CORS kerak emas.
- Backend auth (JWT) hali yo'q, shuning uchun butun sayt Nginx **basic auth** bilan yopiladi (login/parolni men serverda o'zim kiritaman).

## 1-qism: Koddagi o'zgarishlar
1. `Program.cs`:
   - `app.UseForwardedHeaders(new ForwardedHeadersOptions { ForwardedHeaders = XForwardedFor | XForwardedProto })` — eng boshida (KnownNetworks/KnownProxies'ni tozalab, faqat localhost proxy).
   - Production'da `UseHttpsRedirection` O'CHIRILSIN (SSL'ni Nginx qiladi, aks holda redirect aylanadi).
   - Production'da eski `wwwroot/index.html` berilmasin (`UseDefaultFiles/UseStaticFiles` faqat Development'da).
   - CORS: Development'da hozirgidek; Production'da CORS umuman kerak emas (bitta domen).
   - `GET /health` → `{ status: "ok", db: true/false }` (bazaga `SELECT 1`).
2. `appsettings.Production.json.example` yarat (parolsiz shablon). Haqiqiy `appsettings.Production.json` gitignore'da qoladi.
3. Frontend: `client.js` `baseURL = import.meta.env.VITE_API_URL || 'http://localhost:5048'`. `.env.production` → `VITE_API_URL=/api`. `vite build` xatosiz o'tsin; router `createWebHistory()` qoladi.

## 2-qism: `deploy/` papkasi (hammasi repo ichida)
1. `deploy/nginx/mediwell.uz.conf`
   - `server_name mediwell.uz www.mediwell.uz;` (avval faqat `listen 80` — SSL'ni certbot o'zi qo'shadi)
   - www → mediwell.uz 301
   - `root /var/www/mediwell-panel; location / { try_files $uri $uri/ /index.html; }`
   - `location /api/ { proxy_pass http://127.0.0.1:5048/; proxy_set_header Host, X-Real-IP, X-Forwarded-For, X-Forwarded-Proto; }`
   - `auth_basic "Mediwell"; auth_basic_user_file /etc/nginx/.htpasswd-mediwell;` (server darajasida; `/.well-known/acme-challenge/` uchun `auth_basic off`)
   - `client_max_body_size 20m;`, gzip, `/assets/` uchun uzoq kesh, `index.html` uchun no-cache.
2. `deploy/mediwell-api.service` — systemd:
   `User=asadbek`, `WorkingDirectory=/opt/mediwell/api`, `ExecStart=/usr/bin/dotnet /opt/mediwell/api/Mediwell.Api.dll --urls http://127.0.0.1:5048`, `Environment=ASPNETCORE_ENVIRONMENT=Production`, `Restart=always`, `RestartSec=5`.
3. `deploy/server-setup.sh` — **BIR MARTALIK**, idempotent (qayta ishlatsa buzilmasin), `set -euo pipefail`, har qadamda nima qilayotganini o'zbekcha chiqarsin:
   1. Tekshiruv: `dotnet --list-sdks` da 8.x bor; `node -v` ≥ 18; `nginx`, `certbot`, `psql` bor. Yo'q bo'lsa — aniq xabar bilan to'xtasin (o'zi o'rnatmasin).
   2. `apache2-utils` (htpasswd) yo'q bo'lsa o'rnatsin.
   3. Papkalar: `/opt/mediwell/api`, `/var/www/mediwell-panel` (egasi asadbek).
   4. Baza: `mediwell_prod` va user `mediwell_prod` mavjud bo'lmasa yaratsin, parolni `openssl rand` bilan generatsiya qilib `/opt/mediwell/api/appsettings.Production.json` ga yozsin (chmod 600). Mavjud bo'lsa — tegmasin. Eski bazalarga TEGMASIN.
   5. Basic auth: `/etc/nginx/.htpasswd-mediwell` yo'q bo'lsa — login so'rasin va `htpasswd -c` (parolni interaktiv kiritaman).
   6. Nginx config'ni `/etc/nginx/sites-available/` ga ko'chirib, `sites-enabled` ga symlink, `nginx -t`, reload. `default` saytga tegmasin.
   7. systemd servisni o'rnatib `enable`.
   8. Oxirida: keyingi qadam sifatida `./deploy/deploy.sh` va certbot buyrug'ini ko'rsatsin.
4. `deploy/deploy.sh` — **har yangilanishda**:
   `git pull` → `dotnet publish Mediwell.Api -c Release -o /opt/mediwell/api` (appsettings.Production.json o'chib ketmasin!) → `cd Mediwell.Web && npm ci && npm run build` → `rsync -a --delete dist/ /var/www/mediwell-panel/` → `sudo systemctl restart mediwell-api` → 10 soniyagacha `curl http://127.0.0.1:5048/health` kutib tekshirsin → muvaffaqiyat/xato xabari. Xato bo'lsa `journalctl -u mediwell-api -n 50` ni chiqarsin.
5. `deploy/README.md` — men uchun qadamma-qadam qo'llanma (o'zbekcha, oddiy):
   1. Kompyuterda: commit + `git push`
   2. Serverga kirish: `ssh asadbek@193.180.209.245`
   3. Repo'ni olish: `git clone ... ~/mediwell-app` (private bo'lsa — deploy key yoki token bilan qanday qilishni yoz; farm_bot qanday pull qilayotganini `~/farm` da `git remote -v` bilan tekshirishni ham ayt)
   4. `cd ~/mediwell-app && bash deploy/server-setup.sh`
   5. `bash deploy/deploy.sh`
   6. SSL: `sudo certbot --nginx -d mediwell.uz -d www.mediwell.uz --redirect`
   7. Tekshirish: `https://mediwell.uz` → parol so'raydi → panel ochiladi → `https://mediwell.uz/api/health` ok
   8. Ixtiyoriy: Cloudflare'da proxy (to'q sariq bulut) yoqish + SSL/TLS rejimi **Full (strict)**
   9. Muammo bo'lsa: `journalctl -u mediwell-api -f`, `sudo tail -f /var/log/nginx/error.log`
   10. Keyingi yangilanishlar: `git push` → serverda `cd ~/mediwell-app && bash deploy/deploy.sh`

## Ish tartibi
- Avval qisqa reja yoz, men tasdiqlayman.
- Serverga O'ZING ulanma va buyruq yurgizma — skriptlarni men serverda o'zim ishga tushiraman. Sen faqat repo ichidagi fayllarni tayyorla.
- Lokal tekshir: `dotnet build`, `npm run build`, `bash -n deploy/*.sh` (sintaksis), nginx config'ni ko'zdan kechir.
- Tugagach alohida commit: "Deploy: nginx, systemd, setup va deploy skriptlari".
- Oxirida menga 5-10 qatorlik "endi nima qilaman" ro'yxati ber.
