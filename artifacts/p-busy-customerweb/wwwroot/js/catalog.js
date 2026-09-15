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

(() => {
    const overlay = document.querySelector("[data-cd-cal]");
    const openBtn = document.querySelector("[data-cd-schedule]");
    if (!overlay || !openBtn) return;

    const monthLabel = overlay.querySelector("[data-cd-cal-month]");
    const grid = overlay.querySelector("[data-cd-cal-grid]");
    const list = overlay.querySelector("[data-cd-cal-list]");
    const status = overlay.querySelector("[data-cd-cal-status]");
    const url = overlay.getAttribute("data-cd-busy-url") || "";
    let year = new Date().getFullYear();
    let month = new Date().getMonth();
    let loading = false;

    const pad = (n) => String(n).padStart(2, "0");
    const isoLocal = (d) =>
        `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}:${pad(d.getSeconds())}`;

    const startOf = (value) => {
        const d = new Date(value);
        return Number.isNaN(d.getTime()) ? null : d;
    };

    const overlapsDay = (period, dayStart) => {
        const start = startOf(period.startDate ?? period.StartDate);
        const end = startOf(period.endDate ?? period.EndDate);
        if (!start || !end) return false;
        const dayEnd = new Date(dayStart);
        dayEnd.setDate(dayEnd.getDate() + 1);
        return start < dayEnd && end > dayStart;
    };

    const fmt = (value) => {
        const d = startOf(value);
        if (!d) return "";
        return `${pad(d.getDate())}/${pad(d.getMonth() + 1)} ${pad(d.getHours())}:${pad(d.getMinutes())}`;
    };

    const close = () => overlay.setAttribute("hidden", "");
    const open = () => {
        overlay.removeAttribute("hidden");
        loadMonth();
    };

    const loadMonth = async () => {
        if (!url || loading) return;
        loading = true;
        const from = new Date(year, month, 1, 0, 0, 0);
        const to = new Date(year, month + 1, 1, 0, 0, 0);
        if (monthLabel) monthLabel.textContent = `Tháng ${month + 1} ${year}`;
        if (status) status.textContent = "Đang tải lịch xe…";
        if (list) list.innerHTML = "";
        if (grid) grid.innerHTML = "";

        const endpoint = new URL(url, window.location.origin);
        endpoint.searchParams.set("from", isoLocal(from));
        endpoint.searchParams.set("to", isoLocal(to));

        let periods = [];
        try {
            const response = await fetch(endpoint.toString(), { headers: { Accept: "application/json" } });
            if (!response.ok) throw new Error("busy");
            const data = await response.json();
            periods = Array.isArray(data) ? data : [];
        } catch {
            loading = false;
            if (status) status.textContent = "Không tải được lịch xe. Vui lòng thử lại.";
            return;
        }

        const firstWeekday = (from.getDay() + 6) % 7;
        const daysInMonth = new Date(year, month + 1, 0).getDate();
        const cells = [];
        for (let i = 0; i < firstWeekday; i += 1) {
            const blank = document.createElement("button");
            blank.type = "button";
            blank.className = "is-out";
            blank.disabled = true;
            blank.textContent = "";
            cells.push(blank);
        }
        for (let day = 1; day <= daysInMonth; day += 1) {
            const cell = document.createElement("button");
            cell.type = "button";
            cell.textContent = String(day);
            const dayStart = new Date(year, month, day, 0, 0, 0);
            if (periods.some((p) => overlapsDay(p, dayStart))) {
                cell.classList.add("is-busy");
                cell.title = "Xe đang bận";
            }
            cells.push(cell);
        }
        grid?.replaceChildren(...cells);

        if (periods.length === 0) {
            if (status) status.textContent = "Xe chưa có lịch bận trong khoảng thời gian này.";
        } else {
            if (status) status.textContent = "";
            periods.forEach((period) => {
                const item = document.createElement("li");
                const when = document.createElement("strong");
                when.textContent = `${fmt(period.startDate ?? period.StartDate)} → ${fmt(period.endDate ?? period.EndDate)}`;
                const label = document.createElement("span");
                label.textContent = "Đang bận";
                item.append(when, label);
                list?.append(item);
            });
        }
        loading = false;
    };

    openBtn.addEventListener("click", open);
    overlay.querySelector("[data-cd-cal-close]")?.addEventListener("click", close);
    overlay.addEventListener("click", (event) => {
        if (event.target === overlay) close();
    });
    overlay.querySelector("[data-cd-cal-prev]")?.addEventListener("click", () => {
        month -= 1;
        if (month < 0) { month = 11; year -= 1; }
        loadMonth();
    });
    overlay.querySelector("[data-cd-cal-next]")?.addEventListener("click", () => {
        month += 1;
        if (month > 11) { month = 0; year += 1; }
        loadMonth();
    });
    document.addEventListener("keydown", (event) => {
        if (event.key === "Escape" && !overlay.hasAttribute("hidden")) close();
    });
})();
