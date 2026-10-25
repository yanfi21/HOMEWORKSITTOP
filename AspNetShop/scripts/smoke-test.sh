#!/usr/bin/env bash
# Smoke-тест: запускает приложение, проверяет ключевые страницы и API, останавливает сервер.
# Используется в GitHub Actions; можно запускать локально из папки AspNetShop: bash scripts/smoke-test.sh
set -euo pipefail

export ASPNETCORE_ENVIRONMENT=Development
URL="http://127.0.0.1:5080"

dotnet run --project src/Shop.Web -c Release --no-build --urls "$URL" > smoke-app.log 2>&1 &
APP_PID=$!
trap 'kill $APP_PID 2>/dev/null || true' EXIT

echo "Waiting for app to start..."
for i in $(seq 1 40); do
  if curl -fs "$URL/health" > /dev/null 2>&1; then
    echo "App is up"
    break
  fi
  sleep 2
  if ! kill -0 $APP_PID 2>/dev/null; then
    echo "App exited early. Log:"
    cat smoke-app.log
    exit 1
  fi
  if [ "$i" -eq 40 ]; then
    echo "App did not start in time. Log:"
    cat smoke-app.log
    exit 1
  fi
done

check() {
  local path="$1" needle="$2"
  echo "GET $path (expect: $needle)"
  # Пишем в файл, а не в pipe: иначе grep -q закрывает поток и curl падает с кодом 23 (SIGPIPE + pipefail).
  curl -fs "$URL$path" -o /tmp/smoke-page.out
  grep -q "$needle" /tmp/smoke-page.out
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
