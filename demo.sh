#!/bin/bash
# Scenariusze demo — QuickBite API
# Uruchomienie: bash demo.sh
# Wymaganie: docker compose up --build -d

BASE="http://localhost:5080"

sep()   { echo; echo "══════════════════════════════════════════"; echo "  $1"; echo "══════════════════════════════════════════"; }
ok()    { echo "▶ $1"; }
err()   { echo "✗ OCZEKIWANY BŁĄD — $1"; }
pause() { echo; read -p "[ ENTER — następny krok ]"; echo; }
http()  { curl -s -o /tmp/demo_body -w "%{http_code}" "$@"; }

show() {
    local code=$1
    echo "HTTP $code"
    cat /tmp/demo_body | python3 -m json.tool 2>/dev/null || cat /tmp/demo_body
    echo
}

# ──────────────────────────────────────────────────────────────────────────────
sep "SCENARIUSZ 1 — Rejestracja i logowanie"
# ──────────────────────────────────────────────────────────────────────────────

err "Rejestracja — hasło bez cyfry → 400 BadRequest"
CODE=$(http -X POST $BASE/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{"email":"demo_klient@test.pl","password":"bezCyfry","fullName":"Anna Nowak","role":0}')
show $CODE
pause

ok "Rejestracja klienta → 200 OK"
CODE=$(http -X POST $BASE/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{"email":"demo_klient@test.pl","password":"Klient123","fullName":"Anna Nowak","role":0}')
show $CODE
pause

ok "Rejestracja właściciela restauracji → 200 OK"
CODE=$(http -X POST $BASE/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{"email":"demo_resto@test.pl","password":"Resto123","fullName":"Mario Rossi","role":1}')
show $CODE
pause

ok "Rejestracja kuriera → 200 OK"
CODE=$(http -X POST $BASE/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{"email":"demo_kurier@test.pl","password":"Kurier123","fullName":"Piotr Kowalski","role":2}')
show $CODE
pause

err "Rejestracja — ten sam email ponownie → 400 BadRequest"
CODE=$(http -X POST $BASE/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{"email":"demo_klient@test.pl","password":"Klient123","fullName":"Anna Nowak","role":0}')
show $CODE
pause

err "Logowanie — złe hasło → 401 Unauthorized"
CODE=$(http -X POST $BASE/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"demo_klient@test.pl","password":"ZleHaslo1"}')
echo "HTTP $CODE — $(cat /tmp/demo_body)"
echo
pause

ok "Logowanie — poprawne, zapis tokenów do zmiennych"
TOKEN_KLIENT=$(curl -s -X POST $BASE/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"demo_klient@test.pl","password":"Klient123"}' \
  | python3 -c "import sys,json; print(json.load(sys.stdin)['token'])")

TOKEN_RESTO=$(curl -s -X POST $BASE/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"demo_resto@test.pl","password":"Resto123"}' \
  | python3 -c "import sys,json; print(json.load(sys.stdin)['token'])")

TOKEN_KURIER=$(curl -s -X POST $BASE/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"demo_kurier@test.pl","password":"Kurier123"}' \
  | python3 -c "import sys,json; print(json.load(sys.stdin)['token'])")

echo "Token klienta zapisany: ${TOKEN_KLIENT:0:30}..."
pause

ok "Sprawdź swój profil (GET /api/auth/me) → 200 OK"
CODE=$(http $BASE/api/auth/me -H "Authorization: Bearer $TOKEN_KLIENT")
show $CODE
pause

err "Brak tokena → 401 Unauthorized"
CODE=$(http $BASE/api/auth/me)
echo "HTTP $CODE"
echo
pause

# ──────────────────────────────────────────────────────────────────────────────
sep "SCENARIUSZ 2 — Restauracja i menu"
# ──────────────────────────────────────────────────────────────────────────────

ok "Utwórz restaurację (właściciel) → 201 Created"
RESTO=$(curl -s -X POST $BASE/api/restaurants \
  -H "Authorization: Bearer $TOKEN_RESTO" \
  -H "Content-Type: application/json" \
  -d '{"name":"Pizza Roma","address":"ul. Testowa 1","deliveryFee":5.00,"minOrderAmount":20.00}')
echo $RESTO | python3 -m json.tool
RESTO_ID=$(echo $RESTO | python3 -c "import sys,json; print(json.load(sys.stdin)['id'])")
echo "Restauracja ID: $RESTO_ID"
pause

ok "Dodaj pozycje menu"
ITEM1=$(curl -s -X POST $BASE/api/restaurants/$RESTO_ID/menu \
  -H "Authorization: Bearer $TOKEN_RESTO" \
  -H "Content-Type: application/json" \
  -d '{"name":"Margherita","price":25.00,"category":"Pizza","isAvailable":true}')
ITEM1_ID=$(echo $ITEM1 | python3 -c "import sys,json; print(json.load(sys.stdin)['id'])")
echo "Margherita ID: $ITEM1_ID"

ITEM2=$(curl -s -X POST $BASE/api/restaurants/$RESTO_ID/menu \
  -H "Authorization: Bearer $TOKEN_RESTO" \
  -H "Content-Type: application/json" \
  -d '{"name":"Pepperoni","price":30.00,"category":"Pizza","isAvailable":true}')
ITEM2_ID=$(echo $ITEM2 | python3 -c "import sys,json; print(json.load(sys.stdin)['id'])")
echo "Pepperoni ID: $ITEM2_ID"
pause

ok "Odczytaj menu (publiczne — bez tokena)"
curl -s $BASE/api/restaurants/$RESTO_ID/menu | python3 -m json.tool
pause

err "Klient próbuje dodać pozycję menu → 403 Forbidden"
CODE=$(http -X POST $BASE/api/restaurants/$RESTO_ID/menu \
  -H "Authorization: Bearer $TOKEN_KLIENT" \
  -H "Content-Type: application/json" \
  -d '{"name":"Hawajska","price":28.00,"category":"Pizza","isAvailable":true}')
echo "HTTP $CODE"
echo
pause

# ──────────────────────────────────────────────────────────────────────────────
sep "SCENARIUSZ 3 — Wzorzec Strategii (kalkulacje cen)"
# ──────────────────────────────────────────────────────────────────────────────

ITEMS='[{"menuItemId":'$ITEM1_ID',"quantity":1},{"menuItemId":'$ITEM2_ID',"quantity":2}]'
# Margherita x1=25, Pepperoni x2=60 → suma=85, dostawa=5

ok "Strategia Regular — normalna cena (85 + 5 = 90 zł)"
curl -s -X POST $BASE/api/pricing/calculate \
  -H "Authorization: Bearer $TOKEN_KLIENT" \
  -H "Content-Type: application/json" \
  -d '{"restaurantId":'$RESTO_ID',"items":'$ITEMS',"strategy":"Regular"}' \
  | python3 -m json.tool
pause

ok "Strategia Discount 20% — rabat na pozycje (85×0.8 + 5 = 73 zł)"
curl -s -X POST $BASE/api/pricing/calculate \
  -H "Authorization: Bearer $TOKEN_KLIENT" \
  -H "Content-Type: application/json" \
  -d '{"restaurantId":'$RESTO_ID',"items":'$ITEMS',"strategy":"Discount","discountPercent":20}' \
  | python3 -m json.tool
pause

ok "Strategia PromoCode VIP30 — 30% rabat (85×0.7 + 5 = 64.50 zł)"
curl -s -X POST $BASE/api/pricing/calculate \
  -H "Authorization: Bearer $TOKEN_KLIENT" \
  -H "Content-Type: application/json" \
  -d '{"restaurantId":'$RESTO_ID',"items":'$ITEMS',"strategy":"PromoCode","promoCode":"VIP30"}' \
  | python3 -m json.tool
pause

err "Zły kod promocyjny → 400 BadRequest"
curl -s -X POST $BASE/api/pricing/calculate \
  -H "Authorization: Bearer $TOKEN_KLIENT" \
  -H "Content-Type: application/json" \
  -d '{"restaurantId":'$RESTO_ID',"items":'$ITEMS',"strategy":"PromoCode","promoCode":"FAKE99"}' \
  | python3 -m json.tool
pause

err "Discount bez podania procentu → 400 BadRequest"
curl -s -X POST $BASE/api/pricing/calculate \
  -H "Authorization: Bearer $TOKEN_KLIENT" \
  -H "Content-Type: application/json" \
  -d '{"restaurantId":'$RESTO_ID',"items":'$ITEMS',"strategy":"Discount"}' \
  | python3 -m json.tool
pause

# ──────────────────────────────────────────────────────────────────────────────
sep "SCENARIUSZ 4 — Składanie zamówienia i maszyna stanów"
# ──────────────────────────────────────────────────────────────────────────────

ok "Klient składa zamówienie z kodem VIP30 → 201 Created"
ORDER=$(curl -s -X POST $BASE/api/orders \
  -H "Authorization: Bearer $TOKEN_KLIENT" \
  -H "Content-Type: application/json" \
  -d '{
    "restaurantId":'$RESTO_ID',
    "items":[{"menuItemId":'$ITEM1_ID',"quantity":1},{"menuItemId":'$ITEM2_ID',"quantity":2}],
    "strategy":"PromoCode",
    "promoCode":"VIP30",
    "notes":"Bez cebuli"
  }')
echo $ORDER | python3 -m json.tool
ORDER_ID=$(echo $ORDER | python3 -c "import sys,json; print(json.load(sys.stdin)['id'])")
echo "Zamówienie ID: $ORDER_ID"
pause

ok "Sprawdź dozwolone przejścia statusu"
curl -s $BASE/api/orders/$ORDER_ID/allowed-transitions \
  -H "Authorization: Bearer $TOKEN_RESTO" \
  | python3 -m json.tool
pause

err "Klient próbuje zaakceptować (tylko restauracja może) → 403 Forbidden"
CODE=$(http -X PUT $BASE/api/orders/$ORDER_ID/status \
  -H "Authorization: Bearer $TOKEN_KLIENT" \
  -H "Content-Type: application/json" \
  -d '"Accepted"')
show $CODE
pause

err "Nieprawidłowy skok statusu Pending → Delivered → 400 BadRequest"
CODE=$(http -X PUT $BASE/api/orders/$ORDER_ID/status \
  -H "Authorization: Bearer $TOKEN_KURIER" \
  -H "Content-Type: application/json" \
  -d '"Delivered"')
show $CODE
pause

ok "Restauracja akceptuje zamówienie → 200 OK"
CODE=$(http -X PUT $BASE/api/orders/$ORDER_ID/status \
  -H "Authorization: Bearer $TOKEN_RESTO" \
  -H "Content-Type: application/json" \
  -d '"Accepted"')
show $CODE
pause

ok "Restauracja rozpoczyna przygotowanie → 200 OK"
CODE=$(http -X PUT $BASE/api/orders/$ORDER_ID/status \
  -H "Authorization: Bearer $TOKEN_RESTO" \
  -H "Content-Type: application/json" \
  -d '"InPreparation"')
show $CODE
pause

ok "Restauracja: gotowe do odbioru → 200 OK"
CODE=$(http -X PUT $BASE/api/orders/$ORDER_ID/status \
  -H "Authorization: Bearer $TOKEN_RESTO" \
  -H "Content-Type: application/json" \
  -d '"ReadyForPickup"')
show $CODE
pause

ok "Kurier odbiera zamówienie → 200 OK"
CODE=$(http -X PUT $BASE/api/orders/$ORDER_ID/status \
  -H "Authorization: Bearer $TOKEN_KURIER" \
  -H "Content-Type: application/json" \
  -d '"OutForDelivery"')
show $CODE
pause

ok "Kurier dostarcza zamówienie → 200 OK"
CODE=$(http -X PUT $BASE/api/orders/$ORDER_ID/status \
  -H "Authorization: Bearer $TOKEN_KURIER" \
  -H "Content-Type: application/json" \
  -d '"Delivered"')
show $CODE
pause

ok "Finalne szczegóły zamówienia"
curl -s $BASE/api/orders/$ORDER_ID \
  -H "Authorization: Bearer $TOKEN_KLIENT" \
  | python3 -m json.tool
pause

# ──────────────────────────────────────────────────────────────────────────────
sep "SCENARIUSZ 5 — Wygaśnięcie tokena (2 minuty)"
# ──────────────────────────────────────────────────────────────────────────────

ok "Teraz token działa → 200 OK"
curl -s $BASE/api/auth/me -H "Authorization: Bearer $TOKEN_KLIENT" | python3 -m json.tool

echo
echo "⏳ Poczekaj 2 minuty i wklej:"
echo "   curl -s $BASE/api/auth/me -H 'Authorization: Bearer \$TOKEN_KLIENT'"
echo "   Otrzymasz: 401 Unauthorized"

sep "KONIEC DEMO"
