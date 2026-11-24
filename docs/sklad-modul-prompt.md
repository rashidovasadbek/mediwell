# Vazifa: "Sklad hisobi" moduli (3 guruh: MEDIWELL, ZEROMED, MW04JF)

## Kontekst
Men hozir 3 ta guruh (MEDIWELL, ZEROMED, MW04JF) sklad qoldig'ini 3 ta alohida Excel (.xlsm) faylda yuritaman. Shuni shu loyihaga (Mediwell.Api + Mediwell.Web) ko'chirmoqchiman, keyin mediwell.uz serverga chiqadi.

Excel'dagi mantiq (har guruhda bir xil):
- **Препараты** — dori nomi + Переч narxi
- **Приход** — Тип: Приход (omborga qo'shiladi), Перевод (нал ⇄ mediwell ko'chirish: «Куда» omboriga +, ikkinchisidan −), Списание (omboridan −)
- **Переч** — mreja sotuvi: sana, kontragent, region, dori, son, narx. Bitta faktura bir necha qatordan iborat bo'lishi mumkin (bir kontragentga bir nechta dori). **mediwell** omboridan kamayadi
- **Нал** — naqd sotuv: sana, region, РМ (agent), dori, son. **нал** omboridan kamayadi
- **остатка** — har dori bo'yicha: нал | mediwell | Жами

Bu modul **markirovka kodlarsiz**, oddiy son bilan ishlaydi. Loyihadagi mavjud markirovka jadvallari (product, receipt, aggregate, unit_code, code_event) va ularning endpoint/sahifalariga **TEGMA** — bu alohida modul.

## Qarorlar (o'zgartirma)
- Boshlang'ich qoldiq: tarix import qilinmaydi. Har guruh uchun ma'lum sanadagi qoldiq "Boshlang'ich qoldiq" harakati sifatida kiritiladi.
- Pul ustunlari (Сумма фактуры, Оплата, Сальдо, Возврат) hozir YO'Q — faqat son va narx (narx Переч uchun). Debitorka keyingi bosqich.
- Dorilarni men o'zim UI orqali qo'shaman — seed qilinmaydi. Faqat 3 ta guruh seed qilinadi.
- Qoldiq jadvalda SAQLANMAYDI — har doim harakatlar jurnalidan hisoblanadi.
- Qoldiq minusga tushadigan harakat rad etiladi (409, xabarda hozirgi qoldiq ko'rsatiladi).

## Loyiha qoidalari (mavjud kodga moslash)
- Backend: .NET 8 minimal API, Dapper, DbUp (Migrations/*.sql embedded), PostgreSQL. Papka nomlari mavjudidek: `Models/`, `Repasitories/` (imlo shunday qolsin), `Endpoints/`, `Data/`.
- Endpoint'lar `Endpoints/XxxEndpoints.cs` da `MapXxxEndpoints(this WebApplication app)` uslubida, `Program.cs` da ro'yxatga olinadi.
- So'rov modellari `Validate()` metodi bilan (CreateReceiptRequest kabi) — xatolar 400 `{ errors: [...] }`.
- Yozish amallari tranzaksiyada. UNIQUE buzilishi → 409.
- DB nomlari INGLIZCHA, snake_case. UI matnlari o'zbekcha (lotin).
- Frontend: Vue 3 + Element Plus + vue-router, `src/api/client.js` (axios), `AdminLayout.vue` menyusi.

## 1-bosqich: Migratsiya `Migrations/06_stock_ledger.sql`
```
product_group     id, code TEXT UNIQUE NOT NULL, name TEXT NOT NULL, sort_order INT, active BOOL DEFAULT true, created_at
                  seed: ('MEDIWELL','Mediwell'), ('ZEROMED','Zeromed Pharma'), ('MW04JF','MW-04 (JF)')
stock_product     id, group_id FK NOT NULL, name TEXT NOT NULL, perech_price NUMERIC(14,2), sort_order INT, active BOOL DEFAULT true, created_at
                  UNIQUE(group_id, name)   -- bir dori bir nechta guruhda bo'lishi mumkin (masalan Зероксен)
stock_movement    id, movement_date DATE NOT NULL, group_id FK, product_id FK stock_product NOT NULL,
                  movement_type TEXT CHECK IN ('opening','receipt','transfer','write_off','sale_perech','sale_nal'),
                  from_warehouse TEXT NULL CHECK IN ('nal','mediwell'),
                  to_warehouse   TEXT NULL CHECK IN ('nal','mediwell'),
                  qty INTEGER NOT NULL CHECK (qty > 0),
                  price NUMERIC(14,2) NULL,          -- sale_perech uchun
                  counterparty TEXT, region TEXT, agent TEXT, -- kontragent / region / РМ
                  doc_no TEXT NULL,                  -- bir faktura qatorlarini guruhlash uchun (batch)
                  note TEXT, created_by BIGINT NULL REFERENCES app_user(id),
                  created_at, updated_at, deleted_at TIMESTAMPTZ NULL   -- soft delete
```
CHECK qoidalari (turga qarab):
- opening, receipt: to_warehouse NOT NULL, from_warehouse NULL
- transfer: ikkalasi NOT NULL va har xil
- write_off: from_warehouse NOT NULL, to NULL
- sale_perech: from = 'mediwell', to NULL
- sale_nal: from = 'nal', to NULL

Qo'shimcha: `stock_product.group_id` = `stock_movement.group_id` bo'lishini backend tekshiradi.
Indexlar: (group_id, product_id), movement_date, movement_type, WHERE deleted_at IS NULL.

View `v_stock_balance`: har (group, product) uchun
`nal = SUM(qty WHERE to='nal') − SUM(qty WHERE from='nal')`, `mediwell` xuddi shunday, `total = nal + mediwell`; faqat `deleted_at IS NULL`. Harakati yo'q aktiv dorilar ham 0 bilan chiqsin (LEFT JOIN).

## 2-bosqich: Backend API
```
GET    /stock-groups
POST   /stock-groups                 PUT /stock-groups/{id}
GET    /stock-products?groupId=&includeInactive=
POST   /stock-products               PUT /stock-products/{id}
DELETE /stock-products/{id}          -- harakati bo'lsa o'chirmaydi, active=false qiladi (javobda aytadi)
GET    /stock-balance?groupId=&asOf=YYYY-MM-DD    -- asOf ixtiyoriy (o'sha kungacha qoldiq)
GET    /stock-balance/export?groupId=&asOf=        -- .xlsx (ClosedXML): Препарат | нал | mediwell | Жами + ЖАМИ qatori
GET    /stock-movements?groupId=&productId=&type=&from=&to=&search=&page=&pageSize=
POST   /stock-movements              -- bitta harakat
POST   /stock-movements/batch        -- bitta hujjat: umumiy (sana, guruh, tur, kontragent/agent, region) + qatorlar[(productId, qty, price)]; doc_no avtomatik; hammasi bitta tranzaksiyada
POST   /stock-movements/opening      -- guruh + sana + [(productId, nal, mediwell)] → 0 bo'lmaganlari uchun opening qatorlar
PUT    /stock-movements/{id}
DELETE /stock-movements/{id}         -- soft delete
GET    /stock-movements/suggestions?field=counterparty|region|agent&groupId=   -- avtoto'ldirish uchun distinct qiymatlar
```
- Minus tekshiruvi: yozishdan oldin tranzaksiya ichida o'sha (group, product, warehouse) qoldig'ini `SELECT ... FOR UPDATE` yoki advisory lock bilan hisoblab, minusga tushsa 409: `"Пеноксидант 100 мл: нал omborida 120 ta bor, 200 ta chiqarib bo'lmaydi"`. PUT va DELETE ham qoldiqni minusga tushirmasligi kerak.
- sale_perech'da price bo'sh bo'lsa stock_product.perech_price olinadi.
- Har endpoint uchun `Mediwell.Api.http` ga namuna so'rov qo'sh.

## 3-bosqich: Frontend (AdminLayout menyusiga "Sklad hisobi" bo'limi)
Yuqorida **guruh tanlagich** (MEDIWELL / ZEROMED / MW04JF) — barcha sahifalarga ta'sir qiladi, localStorage'da eslab qolinadi.

1. **Qoldiq** `/admin/sklad/qoldiq` — jadval: Препарат | нал | mediwell | Жами, pastda ЖАМИ. Sana tanlagich (asOf). "Excel'ga yuklash" tugmasi. Qidiruv. 0 qoldiqlarni yashirish checkbox.
2. **Harakat qo'shish** `/admin/sklad/kiritish` — tablar:
   - Kirim (ombor tanlanadi), Ko'chirish (qayerdan → qayerga), Spisanie
   - Переч sotuv — hujjat ko'rinishida: sana, kontragent (avtoto'ldirish), region (avtoto'ldirish) + qatorlar jadvali (dori, son, narx avtomatik, summa); "+ qator"; jami summa
   - Нал sotuv — sana, region, РМ (avtoto'ldirish) + qatorlar (dori, son)
   - Har dori tanlanganda yonida hozirgi qoldiq (нал / mediwell) ko'rinsin
   - Saqlagandan keyin sana va guruh saqlanib qoladi, qatorlar tozalanadi (tez ketma-ket kiritish uchun). Sana default = bugun.
3. **Jurnal** `/admin/sklad/jurnal` — barcha harakatlar: sana oralig'i, tur, dori, qidiruv filtrlari; tahrirlash (dialog) va o'chirish ("Rostdan o'chirmoqchimisiz?" tasdig'i bilan).
4. **Dorilar** `/admin/sklad/dorilar` — tanlangan guruh dorilari: qo'shish, nom/narx/tartibni tahrirlash, faolsizlantirish. O'chirishda har doim tasdiq so'ralsin.
5. **Boshlang'ich qoldiq** `/admin/sklad/boshlangich` — guruhning hamma dorilari ro'yxati, har biriga нал va mediwell input, sana; bitta "Saqlash". Agar guruhda allaqachon opening bo'lsa ogohlantirsin.

UI talablari: hamma raqamlar ming ajratgich bilan (12 089), jadval ustunlari markazdan tekislangan, xatolar ElMessage bilan o'zbekcha.

Shu bilan birga: `src/api/client.js` dagi `baseURL` ni `import.meta.env.VITE_API_URL || 'http://localhost:5048'` ga o'tkaz, `.env.example` qo'sh.

## Ish tartibi
1. Avval shu hujjat bo'yicha qisqa reja va savollaringni yoz, men tasdiqlayman — keyin kod.
2. Bosqichma-bosqich: 1 → 2 → 3. Har bosqichdan keyin: `dotnet build` xatosiz, API'ni `.http` orqali sinab ko'r, men tekshiraman, keyin **alohida git commit** (o'zbekcha xabar).
3. Mavjud migratsiya fayllarini (01, 02, 05) O'ZGARTIRMA — faqat yangi 06.
4. Noaniq joy bo'lsa — taxmin qilma, so'ra.
5. Oxirida: qisqa qo'llanma — qanday ishga tushiriladi va qaysi sahifada nima qilinadi.

## Keyingi bosqich (hozir QILMA, faqat yodda tut)
- Backend auth (JWT, app_user'ga login + parol xeshi, rollar) — serverga chiqishdan oldin majburiy
- Переч uchun to'lov/saldo/vazvrat (debitorka)
- Sklad hisobini markirovka moduli bilan bog'lash
