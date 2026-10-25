# ТехноМаркет — интернет-магазин на ASP.NET Core 8 + AJAX

Экзаменационный проект по дисциплине **«ASP.NET и AJAX»**: интернет-магазин по стандартным требованиям,
реализованный по принципам ASP.NET Core (DI, middleware-конвейер, конфигурация, окружения)
с акцентом на **скорость**, а также со встроенными **логированием**, **кэшированием** и **безопасностью**.

[![ASP.NET Shop (build + tests)](https://github.com/yanfi21/HOMEWORKSITTOP/actions/workflows/aspnet-shop.yml/badge.svg)](https://github.com/yanfi21/HOMEWORKSITTOP/actions/workflows/aspnet-shop.yml)

## Как запустить

Нужен только **.NET SDK 8** (https://dotnet.microsoft.com/download/dotnet/8.0). Отдельный сервер БД не требуется — используется SQLite, база `shop.db` и начальные данные создаются автоматически при первом старте.

```bash
cd AspNetShop/src/Shop.Web
dotnet run
```

Открыть: **http://localhost:5080** (профиль `http`) или `dotnet run --launch-profile https` → https://localhost:7081

Сборка и тесты:

```bash
cd AspNetShop
dotnet build -c Release
dotnet test
```

### Тестовые аккаунты

| Роль | E-mail | Пароль |
|---|---|---|
| Администратор | `admin@shop.local` | `Admin123!` |
| Покупатель | `user@shop.local` | `User123!` |

## Функциональность магазина

- **Главная**: категории и «Хиты продаж» (отдаются из `IMemoryCache`).
- **Каталог (AJAX)**: живой поиск, фильтр по категории и цене, 5 вариантов сортировки, пагинация — всё без перезагрузки страницы; сервер возвращает `PartialView`, URL обновляется через History API.
- **Живые подсказки поиска** в шапке и **быстрый просмотр** товара в модальном окне (JSON API + `fetch`, debounce 300 мс).
- **Карточка товара** с описанием, остатками и добавлением в корзину.
- **Корзина (AJAX, сессия)**: добавление с любой страницы, `+/−`, удаление, пересчёт суммы и бейджа в шапке без перезагрузки. Цены всегда берутся из БД на сервере.
- **Оформление заказа**: проверка остатков, транзакционное списание со склада, фиксация цен на момент заказа.
- **Аккаунт**: регистрация, вход, профиль, история и детали заказов (ASP.NET Core Identity).
- **Админ-панель** (`Area Admin`, роль `Admin`): дашборд со статистикой, CRUD товаров (с безопасной загрузкой картинок) и категорий, управление статусами заказов. После правок кэши каталога инвалидируются.

## Где что искать: требования задания → реализация

### 1. Встроенная система логирования
| Что | Где |
|---|---|
| Настройка `ILogger` (консоль + debug + файл), уровни в `appsettings.json` | `Program.cs`, секция `Logging` |
| Собственный файловый провайдер без сторонних библиотек → `logs/app-yyyyMMdd.log` | `Logging/FileLoggerProvider.cs` |
| Лог каждого запроса: метод, путь, статус, время; медленные > 500 мс — `Warning`; заголовок `X-Response-Time-Ms` | `Middleware/RequestLoggingMiddleware.cs` |
| Бизнес-события: входы/неудачные входы, заказы, действия админа, попадания в кэш | контроллеры и сервисы (`ILogger<T>`) |

### 2. Кэширование
| Уровень | Что кэшируем | Где |
|---|---|---|
| `IMemoryCache` | категории (10 мин), хиты главной (5 мин) | `Services/ProductService.cs` |
| `OutputCache` | ответы `GET /api/products` и подсказок (15–30 с, vary by query, тег `catalog`) | `Program.cs`, `Controllers/Api/ProductsApiController.cs` |
| `IDistributedCache` | сессии корзины (в проде заменяется на Redis одной строкой) | `Program.cs` |
| `ResponseCaching` + статика | HTTP-кэш ответов; `Cache-Control: max-age=30 дней` для CSS/JS/картинок, `asp-append-version` против устаревания | `Program.cs`, `_Layout.cshtml` |
| Инвалидация | при CRUD в админке: `InvalidateCatalogCache()` + `EvictByTagAsync("catalog")` | `Areas/Admin/Controllers/*` |

### 3. Безопасность
| Мера | Где |
|---|---|
| Identity: хэши паролей PBKDF2, роли, блокировка на 5 мин после 5 неверных паролей | `Program.cs`, `Controllers/AccountController.cs` |
| Кука входа: `HttpOnly`, `SameSite=Lax`, `Secure` в проде, sliding expiration | `Program.cs` |
| Antiforgery (CSRF) во всех формах и AJAX-POST (заголовок `RequestVerificationToken`) | `_Layout.cshtml`, `wwwroot/js/cart.js`, `[ValidateAntiForgeryToken]` |
| HTTPS + HSTS, заголовки CSP / `X-Frame-Options: DENY` / `nosniff` / `Referrer-Policy` / `Permissions-Policy` | `Middleware/SecurityHeadersMiddleware.cs` |
| Rate Limiting: вход и регистрация 10/мин, API 120/мин (429 при превышении) | `Program.cs`, `[EnableRateLimiting]` |
| Защита от IDOR (чужие заказы — 404), Open Redirect (`Url.IsLocalUrl`), overposting (копируются только разрешённые поля) | `OrderService.cs`, `AccountController.cs`, админ-контроллеры |
| Безопасная загрузка файлов: whitelist расширений, лимит 2 МБ, имя файла — Guid | `Areas/Admin/Controllers/ProductsController.cs` |
| Валидация DataAnnotations + ModelState; EF Core параметризует запросы (нет SQL-инъекций); Razor экранирует вывод (нет XSS) | ViewModels, контроллеры, представления |

### 4. Скорость (акцент задания)
- **Brotli + Gzip** сжатие ответов, в т.ч. по HTTPS (`Program.cs`).
- **OutputCache/IMemoryCache** — повторные обращения к каталогу не ходят в БД (см. выше).
- **EF Core**: пул контекстов `AddDbContextPool`, `AsNoTracking()` на чтение, `Select`-проекции в лёгкие DTO (не тянем лишние колонки), `AsSplitQuery()` для коллекций, серверная пагинация `Skip/Take`, индексы по `CategoryId`, `Name`, `Price`, `(IsActive, IsFeatured)` — `Services/ProductService.cs`, `Data/ApplicationDbContext.cs`.
- **Async/await + CancellationToken** по всему стеку — потоки Kestrel не блокируются.
- Первый рендер каталога — серверный (быстрый First Paint), дальше работает AJAX; картинки — `loading="lazy"`; статика кэшируется на 30 дней.
- Проверить скорость: заголовок ответа `X-Response-Time-Ms` и логи медленных запросов; эндпоинт здоровья — `/health`.

### 5. Принципы ASP.NET Core
- **Generic Host + WebApplicationBuilder**, минимальный `Program.cs` с явным middleware-конвейером (порядок: сжатие → security headers → лог → статика → routing → rate limiter → auth → session → кэши → endpoints).
- **DI-контейнер**: сервисы зарегистрированы как `Scoped` за интерфейсами (`IProductService`, `ICartService`, `IOrderService`) — легко подменять в тестах.
- **Конфигурация** `appsettings.json` + окружения Development/Production (разные уровни логов, HSTS и страницы ошибок только в проде).
- **MVC + Areas**: магазин и админка разделены, общий layout, частичные представления (`_ProductCard`, `_ProductGrid`), Tag Helpers, ViewModels отделены от сущностей БД.
- **Health Checks**: `/health` проверяет доступность БД (`Health/DatabaseHealthCheck.cs`).

## Структура решения

```
AspNetShop/
├── AspNetShop.sln
├── README.md                  ← этот файл
├── EXAM_NOTES.md              ← шпаргалка к защите (вопросы и ответы)
├── src/Shop.Web/
│   ├── Program.cs             ← DI, middleware, кэш, сжатие, rate limiting
│   ├── Models/                ← сущности: Product, Category, Order, ApplicationUser, CartItem
│   ├── Data/                  ← ApplicationDbContext (индексы), DbSeeder (роли, админ, 24 товара)
│   ├── Services/              ← ProductService (кэш каталога), CartService (сессия), OrderService
│   ├── Controllers/           ← Home, Products, Cart, Checkout, Orders, Account
│   ├── Controllers/Api/       ← JSON API для AJAX: products, cart
│   ├── Areas/Admin/           ← админка: Dashboard, Products, Categories, Orders
│   ├── Middleware/            ← SecurityHeaders, RequestLogging
│   ├── Logging/               ← FileLoggerProvider (встроенное логирование в файл)
│   ├── Health/                ← проверка БД для /health
│   ├── ViewModels/            ← модели форм и DTO каталога
│   ├── Views/                 ← Razor-представления и частичные представления
│   └── wwwroot/               ← css, js (catalog.js, cart.js, site.js), картинки
└── tests/Shop.Tests/          ← xUnit-тесты сервисов на InMemory БД (7 тестов)
```

## AJAX: как устроено (для ответа на экзамене)

1. **Каталог** — `fetch("/Products/Grid?...")` → сервер рендерит `PartialView _ProductGrid` → JS подменяет только блок товаров. Плюсы: переиспользуется Razor-разметка, нет дублирования шаблонов.
2. **Подсказки и быстрый просмотр** — `fetch` к JSON API `/api/products/suggestions`, `/api/products/{id}` → рендер на клиенте.
3. **Корзина** — `POST /api/cart/add|update|remove` с JSON-телом и Antiforgery-заголовком; ответ содержит полное состояние корзины, бейдж и суммы обновляются без перезагрузки.
4. Debounce ввода, `AbortController` для отмены устаревших запросов, индикатор загрузки, тост-уведомления об ошибках и успехах.

## Про базу данных и миграции

Для учебного запуска «в один клик» схема создаётся через `EnsureCreated` + `DbSeeder` при старте.
В промышленном проекте вместо этого используются **миграции EF Core**:

```bash
dotnet tool install --global dotnet-ef
dotnet ef migrations add InitialCreate --project src/Shop.Web
dotnet ef database update --project src/Shop.Web
```

и `Migrate()` вместо `EnsureCreated()`. Пакет `Microsoft.EntityFrameworkCore.Design` уже подключён.

## CI

Каждый push проверяется GitHub Actions (`.github/workflows/aspnet-shop.yml`): restore → build Release → tests на .NET 8.
