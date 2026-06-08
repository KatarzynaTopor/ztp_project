#!/bin/bash
# Scenariusze demo — QuickBite API
# Uruchomienie: bash demo.sh
# Wymaganie: docker compose up --build -d

BASE="http://localhost:5080"

sep() { echo; echo "══════════════════════════════════════════"; echo "  $1"; echo "══════════════════════════════════════════"; }
ok()  { echo "▶ $1"; }
err() { echo "✗ OCZEKIWANY BŁĄD — $1"; }
pause() { echo; read -p "[ ENTER — następny krok ]"; echo; }

# ──────────────────────────────────────────────────────────────────────────────
sep "SCENARIUSZ 1 — Rejestracja i logowanie"
# ──────────────────────────────────────────────────────────────────────────────

ok "Rejestracja klienta"
curl -s -X POST $BASE/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{"email":"klient@demo.pl","password":"Klient123","fullName":"Anna Nowak","role":0}' \
  | python3 -m json.tool
pause

ok "Rejestracja właściciela restauracji"
curl -s -X POST $BASE/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{"email":"restauracja@demo.pl","password":"Resto123","fullName":"Mario Rossi","role":1}' \
  | python3 -m json.tool
pause

ok "Rejestracja kuriera"
curl -s -X POST $BASE/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{"email":"kurier@demo.pl","password":"Kurier123","fullName":"Piotr Kowalski","role":2}' \
  | python3 -m json.tool
pause

err "Rejestracja — hasło bez cyfry → 400 BadRequest"
curl -s -X POST $BASE/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{"email":"nowy@demo.pl","password":"bezCyfry","fullName":"Test","role":0}' \
  | python3 -m json.tool
pause

err "Rejestracja — ten sam email → 400 BadRequest"
curl -s -X POST $BASE/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{"email":"klient@demo.pl","password":"Klient123","fullName":"Anna Nowak","role":0}' \
  | python3 -m json.tool
pause

err "Logowanie — złe hasło → 401 Unauthorized"
curl -s -X POST $BASE/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"klient@demo.pl","password":"ZleHaslo1"}' \
  | python3 -m json.tool
pause

ok "Logowanie — poprawne, zapis tokenów do zmiennych"
TOKEN_KLIENT=$(curl -s -X POST $BASE/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"klient@demo.pl","password":"Klient123"}' \
  | python3 -c "import sys,json; print(json.load(sys.stdin)['token'])")

TOKEN_RESTO=$(curl -s -X POST $BASE/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"restauracja@demo.pl","password":"Resto123"}' \
  | python3 -c "import sys,json; print(json.load(sys.stdin)['token'])")

TOKEN_KURIER=$(curl -s -X POST $BASE/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"kurier@demo.pl","password":"Kurier123"}' \
  | python3 -c "import sys,json; print(json.load(sys.stdin)['token'])")

echo "Token klienta (fragment): ${TOKEN_KLIENT:0:30}..."
pause

ok "Sprawdź swój profil (GET /api/auth/me)"
curl -s $BASE/api/auth/me \
  -H "Authorization: Bearer $TOKEN_KLIENT" \
  | python3 -m json.tool
pause

err "Brak tokena → 401 Unauthorized"
curl -s $BASE/api/auth/me | python3 -m json.tool
pause

# ──────────────────────────────────────────────────────────────────────────────
sep "SCENARIUSZ 2 — Restauracja i menu"
# ──────────────────────────────────────────────────────────────────────────────

ok "Utwórz restaurację (właściciel)"
RESTO=$(curl -s -X POST $BASE/api/restaurants \
  -H "Authorization: Bearer $TOKEN_RESTO" \
  -H "Content-Type: application/json" \
  -d '{"name":"Pizza Roma","address":"ul. Testowa 1","deliveryFee":5.00,"minOrderAmount":20.00}')
echo $RESTO | python3 -m json.tool
RESTO_ID=$(echo $RESTO | python3 -c "import sys,json; print(json.load(sys.stdin)['id'])")
pause

ok "Dodaj pozycje menu"
ITEM1=$(curl -s -X POST $BASE/api/restaurants/$RESTO_ID/menu \
  -H "Authorization: Bearer $TOKEN_RESTO" \
  -H "Content-Type: application/json" \
  -d '{"name":"Margherita","price":25.00,"category":"Pizza","isAvailable":true}')
echo $ITEM1 | python3 -m json.tool
ITEM1_ID=$(echo $ITEM1 | python3 -c "import sys,json; print(json.load(sys.stdin)['id'])")

ITEM2=$(curl -s -X POST $BASE/api/restaurants/$RESTO_ID/menu \
  -H "Authorization: Bearer $TOKEN_RESTO" \
  -H "Content-Type: application/json" \
  -d '{"name":"Pepperoni","price":30.00,"category":"Pizza","isAvailable":true}')
echo $ITEM2 | python3 -m json.tool
ITEM2_ID=$(echo $ITEM2 | python3 -c "import sys,json; print(json.load(sys.stdin)['id'])")
pause

ok "Odczytaj menu (publiczne — bez tokena)"
curl -s $BASE/api/restaurants/$RESTO_ID/menu | python3 -m json.tool
pause

err "Klient próbuje dodać pozycję menu → 403 Forbidden"
curl -s -X POST $BASE/api/restaurants/$RESTO_ID/menu \
  -H "Authorization: Bearer $TOKEN_KLIENT" \
  -H "Content-Type: application/json" \
  -d '{"name":"Hawajska","price":28.00,"category":"Pizza","isAvailable":true}' \
  | python3 -m json.tool
pause

# ──────────────────────────────────────────────────────────────────────────────
sep "SCENARIUSZ 3 — Wzorzec Strategii (kalkulacje cen)"
# ──────────────────────────────────────────────────────────────────────────────

ITEMS='[{"menuItemId":'$ITEM1_ID',"quantity":1},{"menuItemId":'$ITEM2_ID',"quantity":2}]'
# Margherita x1 = 25 zł, Pepperoni x2 = 60 zł, suma = 85 zł, dostawa = 5 zł

ok "Strategia Regular — normalna cena (85 + 5 = 90 zł)"
curl -s -X POST $BASE/api/pricing/calculate \
  -H "Authorization: Bearer $TOKEN_KLIENT" \
  -H "Content-Type: application/json" \
  -d '{"restaurantId":'$RESTO_ID',"items":'$ITEMS',"strategy":0}' \
  | python3 -m json.tool
pause

ok "Strategia Discount 20% — rabat na pozycje (85*0.8 + 5 = 73 zł)"
curl -s -X POST $BASE/api/pricing/calculate \
  -H "Authorization: Bearer $TOKEN_KLIENT" \
  -H "Content-Type: application/json" \
  -d '{"restaurantId":'$RESTO_ID',"items":'$ITEMS',"strategy":1,"discountPercent":20}' \
  | python3 -m json.tool
pause

ok "Strategia PromoCode VIP30 — 30% rabat (85*0.7 + 5 = 64.50 zł)"
curl -s -X POST $BASE/api/pricing/calculate \
  -H "Authorization: Bearer $TOKEN_KLIENT" \
  -H "Content-Type: application/json" \
  -d '{"restaurantId":'$RESTO_ID',"items":'$ITEMS',"strategy":2,"promoCode":"VIP30"}' \
  | python3 -m json.tool
pause

err "Zły kod promocyjny → 400 BadRequest"
curl -s -X POST $BASE/api/pricing/calculate \
  -H "Authorization: Bearer $TOKEN_KLIENT" \
  -H "Content-Type: application/json" \
  -d '{"restaurantId":'$RESTO_ID',"items":'$ITEMS',"strategy":2,"promoCode":"FAKE99"}' \
  | python3 -m json.tool
pause

err "Discount bez podania procentu → 400 BadRequest"
curl -s -X POST $BASE/api/pricing/calculate \
  -H "Authorization: Bearer $TOKEN_KLIENT" \
  -H "Content-Type: application/json" \
  -d '{"restaurantId":'$RESTO_ID',"items":'$ITEMS',"strategy":1}' \
  | python3 -m json.tool
pause

# ──────────────────────────────────────────────────────────────────────────────
sep "SCENARIUSZ 4 — Maszyna stanów zamówień"
# ──────────────────────────────────────────────────────────────────────────────

ok "Pobierz szczegóły zamówienia nr 1"
curl -s $BASE/api/orders/1 \
  -H "Authorization: Bearer $TOKEN_KLIENT" \
  | python3 -m json.tool
pause

ok "Sprawdź dozwolone przejścia statusu"
curl -s $BASE/api/orders/1/allowed-transitions \
  -H "Authorization: Bearer $TOKEN_RESTO" \
  | python3 -m json.tool
pause

err "Klient próbuje zaakceptować zamówienie (tylko restauracja) → 403"
curl -s -X PUT $BASE/api/orders/1/status \
  -H "Authorization: Bearer $TOKEN_KLIENT" \
  -H "Content-Type: application/json" \
  -d '"Accepted"' \
  | python3 -m json.tool
pause

err "Nieprawidłowy skok statusu (Pending → Delivered) → 400"
curl -s -X PUT $BASE/api/orders/1/status \
  -H "Authorization: Bearer $TOKEN_KURIER" \
  -H "Content-Type: application/json" \
  -d '"Delivered"' \
  | python3 -m json.tool
pause

ok "Restauracja akceptuje zamówienie → 200 OK"
curl -s -X PUT $BASE/api/orders/1/status \
  -H "Authorization: Bearer $TOKEN_RESTO" \
  -H "Content-Type: application/json" \
  -d '"Accepted"' \
  | python3 -m json.tool
pause

ok "Restauracja rozpoczyna przygotowanie"
curl -s -X PUT $BASE/api/orders/1/status \
  -H "Authorization: Bearer $TOKEN_RESTO" \
  -H "Content-Type: application/json" \
  -d '"InPreparation"' \
  | python3 -m json.tool
pause

# ──────────────────────────────────────────────────────────────────────────────
sep "SCENARIUSZ 5 — Wygaśnięcie tokena (2 minuty)"
# ──────────────────────────────────────────────────────────────────────────────

ok "Teraz token działa"
curl -s $BASE/api/auth/me \
  -H "Authorization: Bearer $TOKEN_KLIENT" \
  | python3 -m json.tool

echo
echo "⏳ Poczekaj 2 minuty i uruchom ponownie to samo polecenie..."
echo "   Otrzymasz: 401 Unauthorized"
echo
echo "   curl -s $BASE/api/auth/me -H 'Authorization: Bearer \$TOKEN_KLIENT'"

sep "KONIEC DEMO"
