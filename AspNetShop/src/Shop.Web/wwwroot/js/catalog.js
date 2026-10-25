// AJAX-каталог: фильтры, живой поиск, сортировка и пагинация без перезагрузки страницы.
// Сервер возвращает PartialView (_ProductGrid), мы подменяем только блок с товарами
// и обновляем URL через History API — ссылку на отфильтрованный каталог можно скопировать.
(function () {
    "use strict";

    var grid = document.getElementById("catalogGrid");
    var loading = document.getElementById("catalogLoading");
    var search = document.getElementById("filterSearch");
    var category = document.getElementById("filterCategory");
    var min = document.getElementById("filterMin");
    var max = document.getElementById("filterMax");
    var sort = document.getElementById("filterSort");
    var reset = document.getElementById("resetFilters");
    if (!grid) return;

    var page = 1;
    var abort = null;

    function params() {
        var p = new URLSearchParams();
        if (search && search.value.trim()) p.set("search", search.value.trim());
        if (category && category.value) p.set("categoryId", category.value);
        if (min && min.value) p.set("minPrice", min.value);
        if (max && max.value) p.set("maxPrice", max.value);
        if (sort && sort.value) p.set("sort", sort.value);
        p.set("page", String(page));
        p.set("pageSize", "9");
        return p;
    }

    function load(pushState) {
        var qs = params().toString();
        if (abort) abort.abort();
        abort = new AbortController();
        if (loading) loading.hidden = false;
        grid.style.opacity = "0.55";

        fetch("/Products/Grid?" + qs, { headers: { "X-Requested-With": "XMLHttpRequest" }, signal: abort.signal })
            .then(function (r) { if (!r.ok) throw new Error("Ошибка загрузки каталога"); return r.text(); })
            .then(function (html) {
                grid.innerHTML = html;
                if (pushState !== false) {
                    history.pushState(null, "", "/Products?" + qs);
                }
            })
            .catch(function (err) {
                if (err.name !== "AbortError") window.shop.toast("Не удалось загрузить товары", true);
            })
            .finally(function () {
                if (loading) loading.hidden = true;
                grid.style.opacity = "1";
            });
    }

    var reload = window.shop.debounce(function () { page = 1; load(true); }, 300);

    if (search) search.addEventListener("input", reload);
    if (min) min.addEventListener("input", reload);
    if (max) max.addEventListener("input", reload);
    if (category) category.addEventListener("change", function () { page = 1; load(true); });
    if (sort) sort.addEventListener("change", function () { page = 1; load(true); });

    if (reset) {
        reset.addEventListener("click", function () {
            if (search) search.value = "";
            if (category) category.value = "";
            if (min) min.value = "";
            if (max) max.value = "";
            if (sort) sort.value = "new";
            page = 1;
            load(true);
        });
    }

    // Пагинация отрисовывается внутри PartialView — клики ловим делегированием.
    grid.addEventListener("click", function (e) {
        var btn = e.target.closest("[data-page]");
        if (!btn) return;
        page = Number(btn.getAttribute("data-page")) || 1;
        load(true);
        window.scrollTo({ top: 0, behavior: "smooth" });
    });

    // Кнопки браузера Назад/Вперёд: восстанавливаем фильтры из URL.
    window.addEventListener("popstate", function () {
        var p = new URLSearchParams(window.location.search);
        if (search) search.value = p.get("search") || "";
        if (category) category.value = p.get("categoryId") || "";
        if (min) min.value = p.get("minPrice") || "";
        if (max) max.value = p.get("maxPrice") || "";
        if (sort) sort.value = p.get("sort") || "new";
        page = Number(p.get("page")) || 1;
        load(false);
    });
})();
