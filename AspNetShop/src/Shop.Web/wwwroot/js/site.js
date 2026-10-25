// Общие helpers: тосты, формат цены, antiforgery-токен для AJAX, живой поиск и быстрый просмотр.
(function () {
    "use strict";

    window.shop = {
        antiforgeryToken: function () {
            var input = document.querySelector('input[name="__RequestVerificationToken"]');
            return input ? input.value : "";
        },
        money: function (value) {
            return new Intl.NumberFormat("ru-RU").format(Math.round(Number(value) || 0)) + " ₽";
        },
        toast: function (message, isError) {
            var container = document.getElementById("toastContainer");
            if (!container) return;
            var el = document.createElement("div");
            el.className = "toast" + (isError ? " error" : "");
            el.textContent = message;
            container.appendChild(el);
            setTimeout(function () { el.remove(); }, 3600);
        },
        debounce: function (fn, ms) {
            var timer = null;
            return function () {
                var args = arguments, self = this;
                clearTimeout(timer);
                timer = setTimeout(function () { fn.apply(self, args); }, ms);
            };
        },
        setCartBadge: function (count) {
            var badge = document.getElementById("cartBadge");
            if (!badge) return;
            badge.textContent = count;
            badge.classList.remove("bump");
            void badge.offsetWidth;
            badge.classList.add("bump");
        }
    };

    // Сообщение после обычного (не AJX) действия, переданное через TempData.
    document.querySelectorAll("[data-toast]").forEach(function (el) {
        var msg = el.getAttribute("data-toast");
        if (msg) window.shop.toast(msg);
    });

    // Мобильное меню
    var burger = document.getElementById("burgerBtn");
    var nav = document.getElementById("mainNav");
    if (burger && nav) {
        burger.addEventListener("click", function () { nav.classList.toggle("open"); });
    }

    // --- Живой поиск в шапке: подсказки из JSON API, debounce 300 мс ---
    var searchInput = document.getElementById("globalSearch");
    var suggestions = document.getElementById("searchSuggestions");
    if (searchInput && suggestions) {
        var loadSuggestions = window.shop.debounce(function () {
            var q = searchInput.value.trim();
            if (q.length < 2) {
                suggestions.classList.remove("open");
                suggestions.innerHTML = "";
                return;
            }
            fetch("/api/products/suggestions?q=" + encodeURIComponent(q), { headers: { "Accept": "application/json" } })
                .then(function (r) { return r.ok ? r.json() : []; })
                .then(function (items) {
                    if (!items.length) {
                        suggestions.innerHTML = "<a href=\"#\">Ничего не найдено</a>";
                    } else {
                        suggestions.innerHTML = items.map(function (p) {
                            return "<a href=\"/Products/Details/" + p.id + "\"><span>" + escapeHtml(p.name) +
                                " <small>(" + escapeHtml(p.categoryName || "") + ")</small></span><span class=\"price\">" +
                                window.shop.money(p.price) + "</span></a>";
                        }).join("") +
                            "<a href=\"/Products?search=" + encodeURIComponent(q) + "\"><strong>Показать все результаты →</strong></a>";
                    }
                    suggestions.classList.add("open");
                })
                .catch(function () { suggestions.classList.remove("open"); });
        }, 300);

        searchInput.addEventListener("input", loadSuggestions);
        searchInput.addEventListener("keydown", function (e) {
            if (e.key === "Enter") {
                e.preventDefault();
                window.location.href = "/Products?search=" + encodeURIComponent(searchInput.value.trim());
            }
        });
        document.addEventListener("click", function (e) {
            if (!e.target.closest(".search-box")) suggestions.classList.remove("open");
        });
    }

    // --- Быстрый просмотр товара в модальном окне (JSON API) ---
    var modal = document.getElementById("quickViewModal");
    var modalContent = document.getElementById("quickViewContent");
    var modalClose = document.getElementById("quickViewClose");
    if (modal && modalContent) {
        document.addEventListener("click", function (e) {
            var btn = e.target.closest("[data-quick-view]");
            if (!btn) return;
            var id = btn.getAttribute("data-quick-view");
            modal.hidden = false;
            modalContent.innerHTML = "Загрузка…";
            fetch("/api/products/" + id, { headers: { "Accept": "application/json" } })
                .then(function (r) { if (!r.ok) throw new Error("not ok"); return r.json(); })
                .then(function (p) {
                    modalContent.innerHTML =
                        "<div class=\"quick-view\">" +
                        "<img src=\"" + p.imageUrl + "\" alt=\"\" />" +
                        "<div><p class=\"product-category\">" + escapeHtml(p.category || "") + "</p>" +
                        "<h2>" + escapeHtml(p.name) + "</h2>" +
                        "<p>" + escapeHtml(p.description || "") + "</p>" +
                        "<p class=\"price-now\">" + window.shop.money(p.price) + "</p>" +
                        "<p class=\"product-stock\">В наличии: " + p.stock + "</p>" +
                        "<button type=\"button\" class=\"btn\" data-add-to-cart=\"" + p.id + "\">В корзину</button> " +
                        "<a class=\"btn btn-ghost\" href=\"/Products/Details/" + p.id + "\">Страница товара</a>" +
                        "</div></div>";
                })
                .catch(function () { modalContent.innerHTML = "Не удалось загрузить товар."; });
        });
        if (modalClose) modalClose.addEventListener("click", function () { modal.hidden = true; });
        modal.addEventListener("click", function (e) { if (e.target === modal) modal.hidden = true; });
    }

    function escapeHtml(text) {
        var div = document.createElement("div");
        div.textContent = text == null ? "" : String(text);
        return div.innerHTML;
    }
})();
