// AJAX-корзина: добавление с любой страницы, изменение количества и удаление без перезагрузки.
// Каждый POST отправляет Antiforgery-токен в заголовке RequestVerificationToken.
(function () {
    "use strict";

    function postJson(url, body) {
        return fetch(url, {
            method: "POST",
            headers: {
                "Content-Type": "application/json",
                "Accept": "application/json",
                "RequestVerificationToken": window.shop.antiforgeryToken()
            },
            body: JSON.stringify(body)
        }).then(function (r) {
            return r.json().then(function (data) {
                if (!r.ok) throw new Error(data.message || "Ошибка запроса");
                return data;
            });
        });
    }

    function applyCartState(cart) {
        if (!cart) return;
        window.shop.setCartBadge(cart.totalQuantity);

        var totalEl = document.getElementById("cartTotal");
        if (totalEl) totalEl.textContent = window.shop.money(cart.total);

        // Обновляем суммы по строкам и убираем удалённые позиции на странице корзины.
        var byId = {};
        (cart.items || []).forEach(function (i) { byId[String(i.productId)] = i; });

        document.querySelectorAll("[data-cart-row]").forEach(function (row) {
            var id = row.getAttribute("data-cart-row");
            var item = byId[id];
            if (!item) {
                row.remove();
                return;
            }
            var line = row.querySelector("[data-cart-line]");
            if (line) line.textContent = window.shop.money(item.lineTotal);
            var qty = row.querySelector("[data-cart-qty]");
            if (qty && document.activeElement !== qty) qty.value = item.quantity;
        });

        if ((cart.items || []).length === 0 && document.getElementById("cartRoot")) {
            window.location.reload();
        }
    }

    // Добавление: кнопки в карточках, на странице товара и в быстром просмотре.
    document.addEventListener("click", function (e) {
        var btn = e.target.closest("[data-add-to-cart]");
        if (btn) {
            var productId = Number(btn.getAttribute("data-add-to-cart"));
            var qty = 1;
            var qtyInputId = btn.getAttribute("data-qty-input");
            if (qtyInputId) {
                var input = document.getElementById(qtyInputId);
                if (input) qty = Math.max(1, Number(input.value) || 1);
            }
            btn.disabled = true;
            postJson("/api/cart/add", { productId: productId, quantity: qty })
                .then(function (data) {
                    applyCartState(data.cart);
                    window.shop.toast(data.message || "Товар добавлен в корзину");
                })
                .catch(function (err) { window.shop.toast(err.message, true); })
                .finally(function () { btn.disabled = false; });
            return;
        }

        var inc = e.target.closest("[data-cart-inc]");
        var dec = e.target.closest("[data-cart-dec]");
        var rem = e.target.closest("[data-cart-remove]");

        if (inc || dec) {
            var pid = Number((inc || dec).getAttribute(inc ? "data-cart-inc" : "data-cart-dec"));
            var rowInput = document.querySelector('[data-cart-qty="' + pid + '"]');
            var current = rowInput ? Number(rowInput.value) || 0 : 0;
            updateQuantity(pid, current + (inc ? 1 : -1));
        }

        if (rem) {
            var rid = Number(rem.getAttribute("data-cart-remove"));
            postJson("/api/cart/remove", { productId: rid })
                .then(function (data) {
                    applyCartState(data.cart);
                    window.shop.toast("Товар удалён из корзины");
                })
                .catch(function (err) { window.shop.toast(err.message, true); });
        }
    });

    var debouncedUpdate = window.shop.debounce(function (productId, qty) {
        updateQuantity(productId, qty);
    }, 350);

    document.addEventListener("change", function (e) {
        var input = e.target.closest("[data-cart-qty]");
        if (!input) return;
        debouncedUpdate(Number(input.getAttribute("data-cart-qty")), Number(input.value) || 0);
    });

    function updateQuantity(productId, quantity) {
        postJson("/api/cart/update", { productId: productId, quantity: quantity })
            .then(function (data) { applyCartState(data.cart); })
            .catch(function (err) { window.shop.toast(err.message, true); });
    }
})();
