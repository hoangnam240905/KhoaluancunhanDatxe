(() => {
    const mode = document.querySelector("[data-cc-mode]");
    mode?.querySelectorAll("span").forEach((item) => {
        item.addEventListener("click", () => {
            mode.querySelectorAll("span").forEach((el) => el.classList.remove("is-on"));
            item.classList.add("is-on");
        });
    });

    const grid = document.querySelector("[data-cc-grid]");
    document.querySelectorAll("[data-cc-view]").forEach((btn) => {
        btn.addEventListener("click", () => {
            document.querySelectorAll("[data-cc-view]").forEach((el) => el.classList.remove("is-on"));
            btn.classList.add("is-on");
            grid?.classList.toggle("is-list", btn.getAttribute("data-cc-view") === "list");
        });
    });

    document.querySelectorAll("[data-cc-fav]").forEach((btn) => {
        btn.addEventListener("click", () => {
            btn.classList.toggle("is-on");
            const icon = btn.querySelector("i");
            if (!icon) return;
            icon.classList.toggle("bi-heart");
            icon.classList.toggle("bi-heart-fill");
        });
    });

    const main = document.querySelector("[data-cd-main]");
    document.querySelectorAll("[data-cd-thumb]").forEach((btn) => {
        btn.addEventListener("click", () => {
            document.querySelectorAll("[data-cd-thumb]").forEach((el) => el.classList.remove("is-on"));
            btn.classList.add("is-on");
            if (main) main.setAttribute("src", btn.getAttribute("data-cd-thumb") || "");
        });
    });

    const panels = document.querySelectorAll("[data-cd-panel]");
    document.querySelectorAll("[data-cd-tab]").forEach((btn) => {
        btn.addEventListener("click", () => {
            const id = btn.getAttribute("data-cd-tab");
            document.querySelectorAll("[data-cd-tab]").forEach((el) => el.classList.remove("is-on"));
            btn.classList.add("is-on");
            panels.forEach((panel) => panel.classList.toggle("is-on", panel.getAttribute("data-cd-panel") === id));
        });
    });
})();
