#!/usr/bin/env bash
# Smoke-тест: запускает приложение, проверяет ключевые страницы и API, останавливает сервер.
# Используется в GitHub Actions; можно запускать локально из папки AspNetShop: bash scripts/smoke-test.sh
set -uo pipefail

export ASPNETCORE_ENVIRONMENT=Development
URL="http://127.0.0.1:5080"

dotnet run --project src/Shop.Web -c Release --no-build --no-launch-profile --urls "$URL" > smoke-app.log 2>&1 &
APP_PID=$!
trap 'kill $APP_PID 2>/dev/null || true' EXIT

fail_with_log() {
  echo "SMOKE FAILED: $1"
  # Печатаем диагностику и в обычный лог, и в аннотации GitHub Actions (видны через API).
  python3 - <<'PY' || true
import pathlib

def ann(title, text):
    enc = text.replace("%", "%25").replace("\r", "%0D").replace("\n", "%0A")
    print(f"::error title={title}::{enc[:3500]}")

body = pathlib.Path("/tmp/smoke-page.out")
if body.exists():
    ann("smoke-body", body.read_bytes()[:2000].decode("utf-8", "replace"))

log = pathlib.Path("smoke-app.log")
if log.exists():
    lines = log.read_text("utf-8", "replace").splitlines()
    hits = [l for l in lines if "exception" in l.lower() or "fail:" in l.lower()][:30]
    ann("smoke-app-log", "\n".join(hits + ["--- tail ---"] + lines[-15:]))
PY
  echo "----- response body (tail) -----"
  tail -c 1500 /tmp/smoke-page.out 2>/dev/null || true
  echo "----- app log (tail) -----"
  tail -n 80 smoke-app.log 2>/dev/null || true
  exit 1
}

echo "Waiting for app to start..."
UP=0
for i in $(seq 1 40); do
  code=$(curl -s -o /tmp/smoke-page.out -w "%{http_code}" "$URL/health" || echo "000")
  if [ "$code" = "200" ]; then
    echo "App is up"
    UP=1
    break
  fi
  if ! kill -0 $APP_PID 2>/dev/null; then
    fail_with_log "app exited early (health code=$code)"
  fi
  sleep 2
done
[ "$UP" = "1" ] || fail_with_log "app did not start in time"

check() {
  local path="$1" needle="$2"
  echo "GET $path (expect 200 + '$needle')"
  code=$(curl -s -o /tmp/smoke-page.out -w "%{http_code}" "$URL$path" || echo "000")
  if [ "$code" != "200" ]; then
    fail_with_log "GET $path returned HTTP $code"
  fi
  if ! grep -q "$needle" /tmp/smoke-page.out; then
    fail_with_log "GET $path does not contain '$needle'"
  fi
}

check "/" "Техно"
check "/Products" "Каталог"
check "/Products?search=Aurora" "Aurora"
check "/Products/Grid?sort=price-asc" "product-card"
check "/api/products?pageSize=3" "totalCount"
check "/api/products/suggestions?q=Aurora" "Aurora"
check "/Account/Login" "admin@shop.local"
check "/health" "Healthy"

echo "Smoke test OK"
