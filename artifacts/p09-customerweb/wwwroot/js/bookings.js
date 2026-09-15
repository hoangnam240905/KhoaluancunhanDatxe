(() => {
    const root = document.querySelector("[data-bk-page]");
    if (!root) return;

    const events = JSON.parse(root.querySelector("[data-bk-events]")?.textContent || "[]");
    const grid = root.querySelector("[data-bk-grid]");
    const title = root.querySelector("[data-bk-month-title]");
    const pop = root.querySelector("[data-bk-pop]");
    const months = ["Tháng 1", "Tháng 2", "Tháng 3", "Tháng 4", "Tháng 5", "Tháng 6", "Tháng 7", "Tháng 8", "Tháng 9", "Tháng 10", "Tháng 11", "Tháng 12"];
    let year = Number(root.getAttribute("data-bk-year")) || new Date().getFullYear();
    let month = Number(root.getAttribute("data-bk-month")) || (new Date().getMonth() + 1);
    let filter = "all";
    let query = "";
    let monthKey = "";

    const isoDay = (d) => `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-${String(d.getDate()).padStart(2, "0")}`;

    const setMode = (mode) => {
        root.setAttribute("data-bk-mode", mode);
        root.querySelectorAll("[data-bk-mode-btn]").forEach((btn) => {
            btn.classList.toggle("is-on", btn.getAttribute("data-bk-mode-btn") === mode);
        });
        root.querySelector("[data-bk-list]")?.classList.toggle("is-off", mode !== "list");
        root.querySelector("[data-bk-cal]")?.classList.toggle("is-off", mode !== "calendar");
    };

    const applyList = () => {
        let visible = 0;
        root.querySelectorAll("[data-bk-card]").forEach((card) => {
            const status = card.getAttribute("data-bk-status") || "";
            const code = (card.getAttribute("data-bk-code") || "").toLowerCase();
            const m = card.getAttribute("data-bk-month") || "";
            const okFilter = filter === "all" || status === filter;
            const okQuery = !query || code.includes(query);
            const okMonth = !monthKey || m === monthKey;
            const on = okFilter && okQuery && okMonth;
            card.classList.toggle("is-off", !on);
            if (on) visible += 1;
        });
        root.querySelector("[data-bk-empty-filter]")?.classList.toggle("is-off", visible > 0);
    };

    const renderCal = () => {
        if (!grid || !title) return;
        title.textContent = `${months[month - 1]} ${year}`;
        const first = new Date(year, month - 1, 1);
        const start = (first.getDay() + 6) % 7;
        const count = new Date(year, month, 0).getDate();
        const prevCount = new Date(year, month - 1, 0).getDate();
        const cells = [];
        for (let i = 0; i < 42; i += 1) {
            let d;
            let out = false;
            if (i < start) {
                d = new Date(year, month - 2, prevCount - start + i + 1);
                out = true;
            } else if (i >= start + count) {
                d = new Date(year, month, i - start - count + 1);
                out = true;
            } else {
                d = new Date(year, month - 1, i - start + 1);
            }
            const key = isoDay(d);
            const dayEvents = events.filter((ev) => {
                const a = isoDay(new Date(ev.start));
                const b = isoDay(new Date(ev.end));
                return key >= a && key <= b;
            });
            cells.push(`<div class="bk-day${out ? " is-out" : ""}"><strong>${d.getDate()}</strong>${
                dayEvents.slice(0, 2).map((ev) =>
                    `<button type="button" class="bk-pill ${ev.status}" data-bk-event="${ev.id}">${ev.name}</button>`
                ).join("")
            }</div>`);
        }
        grid.innerHTML = cells.join("");
        grid.querySelectorAll("[data-bk-event]").forEach((btn) => {
            btn.addEventListener("click", () => openPop(btn.getAttribute("data-bk-event")));
        });
    };

    const openPop = (id) => {
        const ev = events.find((x) => String(x.id) === String(id));
        if (!ev || !pop) return;
        pop.querySelector("[data-bk-pop-name]").textContent = ev.name;
        pop.querySelector("[data-bk-pop-pickup]").textContent = ev.pickup;
        pop.querySelector("[data-bk-pop-return]").textContent = ev.returnAt;
        pop.querySelector("[data-bk-pop-mode]").textContent = ev.mode;
        const badge = pop.querySelector("[data-bk-pop-status]");
        badge.textContent = ev.statusLabel;
        badge.className = `bk-badge bk-badge-${ev.status}`;
        pop.querySelector("[data-bk-pop-link]").setAttribute("href", ev.href);
        pop.classList.add("is-on");
    };

    root.querySelectorAll("[data-bk-mode-btn]").forEach((btn) => {
        btn.addEventListener("click", () => setMode(btn.getAttribute("data-bk-mode-btn")));
    });
    root.querySelectorAll("[data-bk-filter]").forEach((btn) => {
        btn.addEventListener("click", () => {
            filter = btn.getAttribute("data-bk-filter") || "all";
            root.querySelectorAll("[data-bk-filter]").forEach((el) => el.classList.remove("is-on"));
            btn.classList.add("is-on");
            applyList();
        });
    });
    root.querySelector("[data-bk-search]")?.addEventListener("input", (e) => {
        query = (e.target.value || "").trim().toLowerCase();
        applyList();
    });
    root.querySelector("[data-bk-month]")?.addEventListener("change", (e) => {
        monthKey = e.target.value || "";
        applyList();
    });
    root.querySelector("[data-bk-prev]")?.addEventListener("click", () => {
        month -= 1;
        if (month < 1) { month = 12; year -= 1; }
        renderCal();
    });
    root.querySelector("[data-bk-next]")?.addEventListener("click", () => {
        month += 1;
        if (month > 12) { month = 1; year += 1; }
        renderCal();
    });
    pop?.addEventListener("click", (e) => {
        if (e.target === pop || e.target.hasAttribute("data-bk-pop-close")) pop.classList.remove("is-on");
    });

    setMode(root.getAttribute("data-bk-mode") || "list");
    applyList();
    renderCal();
})();
