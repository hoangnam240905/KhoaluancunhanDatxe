(() => {
    const mode = document.querySelector("[data-cs-mode]");
    mode?.querySelectorAll("span").forEach((item) => {
        item.addEventListener("click", () => {
            mode.querySelectorAll("span").forEach((el) => el.classList.remove("is-on"));
            item.classList.add("is-on");
        });
    });

    const grid = document.querySelector("[data-cs-grid]");
    document.querySelectorAll("[data-cs-view]").forEach((btn) => {
        btn.addEventListener("click", () => {
            document.querySelectorAll("[data-cs-view]").forEach((el) => el.classList.remove("is-on"));
            btn.classList.add("is-on");
            grid?.classList.toggle("is-list", btn.getAttribute("data-cs-view") === "list");
        });
    });

    document.querySelectorAll("[data-cs-clear]").forEach((btn) => {
        btn.addEventListener("click", () => {
            document.querySelectorAll("[data-cs-filters] input[type='checkbox']").forEach((el) => { el.checked = false; });
            document.querySelectorAll("[data-cs-filters] input[type='range']").forEach((el) => {
                el.value = el.getAttribute("value") || el.max;
                el.dispatchEvent(new Event("input"));
            });
        });
    });

    const format = (n) => Number(n).toLocaleString("vi-VN") + " VNĐ";
    const syncPrice = (input, writeTarget) => {
        const label = input.closest("[data-cs-filters]")?.querySelector("[data-cs-price-label]");
        if (label) label.textContent = format(input.value);
        if (!writeTarget) return;
        const targetId = input.getAttribute("data-cs-price-target");
        const target = targetId ? document.getElementById(targetId) : null;
        if (target) target.value = input.value;
    };
    document.querySelectorAll("[data-cs-price]").forEach((price) => {
        price.addEventListener("input", () => syncPrice(price, true));
        syncPrice(price, false);
    });
    document.querySelectorAll("#csFilters [data-cs-filter]").forEach((src) => {
        src.addEventListener("change", () => {
            const name = src.getAttribute("data-cs-filter");
            const named = document.querySelector(
                `input[form="customer-search"][name="${name}"][value="${src.value}"]`);
            if (named) named.checked = src.checked;
        });
    });
})();
