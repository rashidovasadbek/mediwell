# Mediwell — serverga chiqarish (mediwell.uz)

```
https://mediwell.uz  (www → mediwell.uz)
  Nginx 443 + Let's Encrypt
    /      → /var/www/mediwell-panel   (Vue panel)   — basic auth (1-parol)
    /api/  → http://127.0.0.1:5048/    (.NET API)    — JWT, panel login (2-parol)
                └─ PostgreSQL: mediwell_prod (user: mediwell_prod)
```

Saytga kirish **ikki bosqichli**:
1. Brauzer oynasi login/parol so'raydi — bu nginx **basic auth** (`server-setup.sh` da kiritasiz).
2. Panelning o'z login sahifasi — **admin** foydalanuvchi (`server-setup.sh` da kiritasiz).

Serverdagi boshqa servislar (farmbot, markir, botlar...), eski `mediwell_db` / `mediwell_test` bazalari
va nginx `default` sayti bu skriptlarga **tegilmaydi**.

---

## Birinchi marta

### 1. Kompyuterda
```bash
git add -A && git commit -m "..." && git push
```

### 2. Serverga kirish
```bash
ssh asadbek@193.180.209.245
```

### 3. Repo'ni olish
Avval boshqa loyiha qanday pull qilayotganini ko'ring:
```bash
cd ~/farm && git remote -v
```
- `git@github.com:...` ko'rinishida bo'lsa — SSH kalit ishlatilgan.
- `https://...` bo'lsa — token ishlatilgan.

**Repo public bo'lsa:**
```bash
git clone https://github.com/rashidovasadbek/mediwell.git ~/mediwell-app
```

**Repo private bo'lsa — deploy key (tavsiya, faqat o'qish huquqi):**
```bash
ssh-keygen -t ed25519 -f ~/.ssh/mediwell_deploy -N "" -C "mediwell-server"
cat ~/.ssh/mediwell_deploy.pub
```
Chiqqan matnni GitHub → repo → **Settings → Deploy keys → Add deploy key** ga qo'shing
("Allow write access" belgilanmasin). Keyin:
```bash
cat >> ~/.ssh/config <<'EOF'
Host github-mediwell
    HostName github.com
    User git
    IdentityFile ~/.ssh/mediwell_deploy
    IdentitiesOnly yes
EOF
git clone git@github-mediwell:rashidovasadbek/mediwell.git ~/mediwell-app
```

**Yoki token bilan:** GitHub → Settings → Developer settings → Fine-grained token
(faqat shu repo, *Contents: Read-only*), keyin:
```bash
git clone https://github.com/rashidovasadbek/mediwell.git ~/mediwell-app
# Username: rashidovasadbek, Password: <token>
cd ~/mediwell-app && git config credential.helper store   # keyingi pull'larda so'ramasin
```

### 4. Serverni tayyorlash (bir marta)
```bash
cd ~/mediwell-app && bash deploy/server-setup.sh
```
Skript so'raydi:
- **Panel admin** login va parol (kamida 8 belgi)
- **Basic auth** login va parol

U quyidagilarni qiladi: dasturlarni tekshiradi, `mediwell_prod` bazasi va foydalanuvchisini yaratadi
(parol avtomatik), `/opt/mediwell/api/appsettings.Production.json` ni yozadi, nginx saytini va
`mediwell-api` servisini o'rnatadi. Qayta ishga tushirsangiz — mavjud narsalarga tegmaydi.

### 5. Birinchi deploy
```bash
bash deploy/deploy.sh
```
Oxirida `MUVAFFAQIYATLI` va `{"status":"ok","db":true}` chiqishi kerak.

### 6. SSL
```bash
sudo certbot --nginx -d mediwell.uz -d www.mediwell.uz --redirect
```

### 7. Tekshirish
- `https://mediwell.uz` → basic auth so'raydi → panel login → admin bilan kiring
- `https://mediwell.uz/api/health` → `{"status":"ok","db":true}`
- `http://mediwell.uz` va `https://www.mediwell.uz` → `https://mediwell.uz` ga o'tadi

### 8. Kirgandan keyin (muhim)
1. Admin parolini almashtiring (hozircha API orqali):
   ```bash
   # Kompyuteringizda yoki serverda; TOKEN — login javobidagi "token"
   curl -s -X POST https://mediwell.uz/api/auth/login -H 'Content-Type: application/json' \
        -d '{"login":"admin","password":"..."}'
   curl -s -X POST https://mediwell.uz/api/auth/change-password \
        -H "Authorization: Bearer TOKEN" -H 'Content-Type: application/json' \
        -d '{"currentPassword":"...","newPassword":"..."}'
   ```
2. Config'dan boshlang'ich admin parolini olib tashlang (`"BootstrapAdmin"` bo'limini o'chiring):
   ```bash
   nano /opt/mediwell/api/appsettings.Production.json
   sudo systemctl restart mediwell-api
   ```
3. Skladchi kerak bo'lsa (`role`: `warehouse`):
   ```bash
   curl -s -X POST https://mediwell.uz/api/users -H "Authorization: Bearer TOKEN" \
        -H 'Content-Type: application/json' \
        -d '{"login":"sklad","fullName":"Skladchi","role":"warehouse","active":true,"password":"..."}'
   ```

### 9. Ixtiyoriy: Cloudflare proxy
SSL ishlagandan keyin Cloudflare'da `mediwell.uz` va `www` yozuvlarida proxy'ni yoqing (to'q sariq bulut)
va **SSL/TLS → Full (strict)** rejimini tanlang. (Flexible tanlamang — redirect aylanib qoladi.)

---

## Keyingi yangilanishlar
```bash
# kompyuterda
git push
# serverda
cd ~/mediwell-app && bash deploy/deploy.sh
```

## Muammo bo'lsa
```bash
sudo systemctl status mediwell-api
journalctl -u mediwell-api -f                 # API loglari
sudo tail -f /var/log/nginx/error.log         # nginx xatolari
curl -s http://127.0.0.1:5048/health          # API to'g'ridan-to'g'ri
sudo nginx -t                                 # nginx config tekshiruvi
```

| Belgi | Sabab / yechim |
|---|---|
| `502 Bad Gateway` | API ishlamayapti → `journalctl -u mediwell-api -n 50` |
| `/health` → `"db":false` | Baza ulanmayapti → `appsettings.Production.json` dagi connection string |
| `MIGRATION XATO` logda | Migratsiya xatosi → log matnini yuboring |
| `Auth:JwtKey sozlanmagan` | `appsettings.Production.json` da `Auth:JwtKey` yo'q yoki qisqa |
| Panel ochiladi, lekin hamma so'rov 401 | Token eskirgan → qayta login qiling |
| Basic auth parolini unutdingiz | `sudo htpasswd /etc/nginx/.htpasswd-mediwell <login>` |

## Fayllar
| Fayl | Serverda |
|---|---|
| `deploy/nginx/mediwell.uz.conf` | `/etc/nginx/sites-available/mediwell.uz` (certbot keyin SSL qo'shadi) |
| `deploy/mediwell-api.service` | `/etc/systemd/system/mediwell-api.service` |
| `Mediwell.Api/appsettings.Production.json.example` | namuna; haqiqiysi `/opt/mediwell/api/appsettings.Production.json` (git'da yo'q) |
| `deploy/server-setup.sh` | bir martalik tayyorlash |
| `deploy/deploy.sh` | har yangilanishda |
