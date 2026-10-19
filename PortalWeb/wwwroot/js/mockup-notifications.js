// Header bell (every dispatcher page) + Notifications page. Data: GET /api/notifications via page handlers
// (window.__NOTIFY_HANDLERS__); live updates: "carrental:realtime" DOM events from realtime.js.
(function () {
    const PAGE = 10;
    const TYPE = {
        booking: { label: "Đơn thuê", icon: "📋" },
        driver: { label: "Tài xế", icon: "👨‍✈️" },
        alert: { label: "Cảnh báo", icon: "⚠️" }
    };
    const PRI = {
        high: { label: "Cao", cls: "nt-pri-high", mark: "🔴" },
        medium: { label: "Trung bình", cls: "nt-pri-medium", mark: "🟡" },
        normal: { label: "Thông thường", cls: "nt-pri-normal", mark: "🔵" }
    };
    // Priority is derived from the type (the API has no priority field).
    const PRI_OF_TYPE = { alert: "high", driver: "medium", booking: "normal" };
    const GROUP = { today: "Hôm nay", yesterday: "Hôm qua", earlier: "Trước đó" };
    const handlers = window.__NOTIFY_HANDLERS__ || {};

    let items = [];
    let loaded = false;
    let loadError = null;
    let tab = "all";
    let query = "";
    let shown = PAGE;
    let menuId = null;
    let selectedId = null;

    const els = {
        btn: document.getElementById("mkNotifyBtn"),
        drop: document.getElementById("mkNotifyDrop"),
        dropList: document.getElementById("mkNotifyDropList"),
        dropMeta: document.getElementById("mkNotifyDropMeta"),
        dot: document.getElementById("mkNotifyDot"),
        toast: document.getElementById("mkGlobalToast") || document.getElementById("mkToast"),
        kpis: document.getElementById("ntKpis"),
        tabs: document.getElementById("ntTabs"),
        search: document.getElementById("ntSearch"),
        list: document.getElementById("ntList"),
        empty: document.getElementById("ntEmpty"),
        meta: document.getElementById("ntMeta"),
        more: document.getElementById("ntMore"),
        drawer: document.getElementById("ntDrawer"),
        markAll: document.getElementById("ntMarkAll")
    };

    function toast(msg) {
        if (!els.toast || !msg) return;
        els.toast.textContent = msg;
        els.toast.classList.add("is-on");
        clearTimeout(toast._t);
        toast._t = setTimeout(() => els.toast.classList.remove("is-on"), 3200);
    }

    // ---------- time (Backend stores CreatedAt in UTC; display in Vietnam time) ----------
    const pad = (n) => String(n).padStart(2, "0");
    function parseUtc(iso) {
        if (!iso) return null;
        const s = /[zZ]|[+-]\d{2}:\d{2}$/.test(iso) ? iso : iso + "Z";
        const d = new Date(s);
        return Number.isNaN(d.getTime()) ? null : d;
    }
    // Shift to Vietnam wall clock and read with getUTC* (independent of the browser time zone).
    const toVn = (d) => new Date(d.getTime() + 7 * 3600 * 1000);
    const vnDayKey = (d) => `${d.getUTCFullYear()}-${d.getUTCMonth()}-${d.getUTCDate()}`;
    const hhmm = (d) => `${pad(d.getUTCHours())}:${pad(d.getUTCMinutes())}`;
    const fullVn = (d) => `${pad(d.getUTCDate())}/${pad(d.getUTCMonth() + 1)}/${d.getUTCFullYear()} ${hhmm(d)}`;

    function timeInfo(createdAt) {
        const at = parseUtc(createdAt);
        if (!at) return { group: "earlier", time: "—", full: "—" };
        const now = new Date();
        const vn = toVn(at), vnNow = toVn(now);
        const vnYesterday = toVn(new Date(now.getTime() - 24 * 3600 * 1000));
        const mins = Math.floor((now - at) / 60000);
        if (vnDayKey(vn) === vnDayKey(vnNow)) {
            return {
                group: "today",
                time: mins < 1 ? "Vừa xong" : mins < 60 ? `${mins} phút trước` : hhmm(vn),
                full: fullVn(vn)
            };
        }
        if (vnDayKey(vn) === vnDayKey(vnYesterday))
            return { group: "yesterday", time: `Hôm qua ${hhmm(vn)}`, full: fullVn(vn) };
        return { group: "earlier", time: fullVn(vn), full: fullVn(vn) };
    }

    function mapItem(n) {
        const type = TYPE[n.type] ? n.type : "booking";
        const t = timeInfo(n.createdAt);
        const toTrips = type !== "booking";
        return {
            id: n.notificationId,
            unread: !n.isRead,
            type,
            pri: PRI_OF_TYPE[type],
            group: t.group,
            time: t.time,
            when: t.full,
            title: n.title,
            body: n.message,
            bookingId: n.bookingId ?? null,
            booking: n.bookingId ? `#${n.bookingId}` : "—",
            href: toTrips ? "/Mockup/Trips" : "/Mockup/Hub",
            action: toTrips ? "Xem chuyến đang chạy" : "Xem đơn thuê",
            createdAt: n.createdAt
        };
    }

    // ---------- API (page handlers) ----------
    function antiforgery() {
        const input = document.querySelector("#mkAntiForgery input[name='__RequestVerificationToken']");
        return input ? input.value : "";
    }

    async function load() {
        if (!handlers.list) return;
        try {
            const res = await fetch(handlers.list, { credentials: "same-origin", headers: { Accept: "application/json" } });
            const data = await res.json().catch(() => null);
            if (!res.ok || !data || !data.ok) throw new Error((data && data.error) || `HTTP ${res.status}`);
            items = (data.items || []).map(mapItem);
            loadError = null;
        } catch (err) {
            loadError = "Không tải được thông báo.";
            console.warn("[Notifications]", err);
        }
        loaded = true;
        renderPage();
        if (selectedId != null && items.some((x) => x.id === selectedId)) openDrawer(selectedId);
    }

    async function post(url, fields) {
        const body = new URLSearchParams(fields || {});
        body.set("__RequestVerificationToken", antiforgery());
        const res = await fetch(url, {
            method: "POST",
            credentials: "same-origin",
            headers: { "Content-Type": "application/x-www-form-urlencoded", RequestVerificationToken: antiforgery() },
            body: body.toString()
        });
        const data = await res.json().catch(() => null);
        return res.ok && data && data.ok;
    }

    // ---------- rendering ----------
    function unreadCount() { return items.filter((x) => x.unread).length; }

    function visible() {
        return items.filter((n) => {
            if (tab === "unread" && !n.unread) return false;
            if (tab === "read" && n.unread) return false;
            if (tab === "alert" && n.type !== "alert") return false;
            if (tab === "booking" && n.type !== "booking") return false;
            if (tab === "driver" && n.type !== "driver") return false;
            if (query) {
                const q = query.toLowerCase();
                const blob = `${n.title} ${n.body} ${n.booking}`.toLowerCase();
                if (!blob.includes(q)) return false;
            }
            return true;
        });
    }

    function badgePri(p) { return `<span class="mk-badge ${PRI[p].cls}">${PRI[p].label}</span>`; }
    function badgeType(t) { return `<span class="mk-badge mk-badge-gray">${TYPE[t].label}</span>`; }

    function actionHref(n) {
        if (n.bookingId > 0 && n.href === "/Mockup/Hub") return "/Mockup/Hub?id=" + n.bookingId;
        return n.href;
    }

    function syncBell() {
        const unread = unreadCount();
        if (els.dot) {
            els.dot.textContent = unread > 99 ? "99+" : String(unread);
            els.dot.hidden = unread === 0;
        }
        if (els.dropMeta) {
            els.dropMeta.textContent = !loaded ? "Đang tải..."
                : loadError ? loadError
                    : unread ? `${unread} thông báo chưa đọc` : "Không có thông báo chưa đọc";
        }
        if (els.dropList) {
            const newest = items.filter((x) => x.unread).slice(0, 4);
            els.dropList.innerHTML = newest.length
                ? newest.map((n) => `
                    <button type="button" class="nt-drop-item is-unread" data-drop-open="${n.id}">
                        <span>${TYPE[n.type].icon}</span>
                        <span><strong>${n.title}</strong><small>${n.body}<br>${n.time}</small></span>
                    </button>`).join("")
                : `<p class="nt-drop-empty">${loaded ? "Không có thông báo chưa đọc." : "Đang tải..."}</p>`;
        }
    }

    function kpis() {
        if (!els.kpis) return;
        const unread = unreadCount();
        const count = (type) => items.filter((x) => x.unread && x.type === type).length;
        const cards = [
            { key: "unread", icon: "🔵", label: "Thông báo mới", value: unread, hint: "Chưa đọc" },
            { key: "booking", icon: "📋", label: "Đơn thuê", value: count("booking"), hint: "Đơn mới / hủy / hoàn thành" },
            { key: "alert", icon: "⚠️", label: "Cảnh báo", value: count("alert"), hint: "Sự cố — ưu tiên xử lý" },
            { key: "driver", icon: "👨‍✈️", label: "Tài xế", value: count("driver"), hint: "Nhận chuyến / bắt đầu" }
        ];
        els.kpis.innerHTML = cards.map((c) => `
            <button type="button" class="mk-metric nt-kpi ${tab === c.key ? "is-on" : ""}" data-tab="${c.key}">
                <span>${c.icon} ${c.label}</span>
                <strong>${c.value}</strong>
                <em>${c.hint}</em>
            </button>`).join("");
    }

    function tabs() {
        if (!els.tabs) return;
        const all = items.length;
        const unread = unreadCount();
        const n = (type) => items.filter((x) => x.type === type).length;
        const defs = [
            { key: "all", label: `Tất cả (${all})` },
            { key: "unread", label: `Chưa đọc (${unread})` },
            { key: "read", label: `Đã đọc (${all - unread})` },
            { key: "alert", label: `Cảnh báo (${n("alert")})` },
            { key: "booking", label: `Đơn thuê (${n("booking")})` },
            { key: "driver", label: `Tài xế (${n("driver")})` }
        ];
        els.tabs.innerHTML = defs.map((d) =>
            `<button type="button" class="mk-pill ${tab === d.key ? "is-on" : ""}" data-tab="${d.key}">${d.label}</button>`
        ).join("");
    }

    function itemHtml(n) {
        return `<div class="nt-item ${n.unread ? "is-unread" : ""} ${selectedId === n.id ? "is-on" : ""}" data-open="${n.id}">
            <span class="nt-ico">${TYPE[n.type].icon}</span>
            <span>
                <span class="nt-title">${PRI[n.pri].mark} ${n.title}</span>
                <span class="nt-body">${n.body}</span>
                <span class="nt-meta">${badgeType(n.type)} ${badgePri(n.pri)} <span class="mk-badge ${n.unread ? "mk-badge-confirmed" : "mk-badge-gray"}">${n.unread ? "Chưa đọc" : "Đã đọc"}</span> <span class="nt-time">${n.time}</span></span>
            </span>
            <span class="nt-dot" title="Chưa đọc"></span>
            <span class="nt-menu-wrap">
                ${n.unread ? `<button type="button" class="nt-menu-btn" data-menu="${n.id}" aria-label="Thao tác">⋯</button>` : ""}
                ${menuId === n.id && n.unread ? `<div class="nt-menu">
                    <button type="button" data-read="${n.id}">Đánh dấu đã đọc</button>
                </div>` : ""}
            </span>
        </div>`;
    }

    function renderPage() {
        syncBell();
        if (!els.list) return;
        kpis();
        tabs();
        const all = visible();
        if (shown > all.length) shown = Math.max(PAGE, all.length);
        const slice = all.slice(0, shown);
        let html = "";
        ["today", "yesterday", "earlier"].forEach((g) => {
            const rows = slice.filter((x) => x.group === g);
            if (!rows.length) return;
            html += `<div class="nt-group">${GROUP[g]}</div>` + rows.map(itemHtml).join("");
        });
        els.list.innerHTML = loadError ? `<p class="nt-drop-empty">${loadError}</p>` : html;
        els.empty.hidden = !loaded || !!loadError || slice.length > 0;
        els.meta.textContent = !loaded ? "Đang tải..."
            : all.length ? `Hiển thị 1–${slice.length} trong ${all.length} thông báo` : "Không có thông báo phù hợp";
        if (els.more) els.more.hidden = !all.length || slice.length >= all.length;
    }

    function openDrawer(id) {
        const n = items.find((x) => x.id === Number(id));
        if (!n || !els.drawer) return;
        selectedId = n.id;
        els.drawer.classList.add("is-open");
        els.drawer.setAttribute("aria-hidden", "false");
        const readBtn = n.unread
            ? `<button type="button" class="mk-btn mk-btn-ghost" data-read="${n.id}">Đánh dấu đã đọc</button>`
            : "";
        els.drawer.innerHTML = `
            <div class="mk-drawer-head">
                <div>
                    <h2 class="h5 mb-0">Chi tiết thông báo</h2>
                    <p class="mk-muted mb-0">${n.time}</p>
                </div>
                <button type="button" class="mk-btn mk-btn-ghost" data-close>Đóng</button>
            </div>
            <div class="mk-drawer-body">
                <h3 class="h6">${PRI[n.pri].mark} ${n.title}</h3>
                <dl class="mk-dl mk-block">
                    <div><dt>Mã đơn</dt><dd>${n.booking}</dd></div>
                    <div><dt>Thời gian</dt><dd>${n.when}</dd></div>
                    <div><dt>Mức độ</dt><dd>${badgePri(n.pri)}</dd></div>
                    <div><dt>Trạng thái</dt><dd><span class="mk-badge ${n.unread ? "mk-badge-confirmed" : "mk-badge-gray"}">${n.unread ? "Chưa đọc" : "Đã đọc"}</span></dd></div>
                    <div><dt>Loại</dt><dd>${badgeType(n.type)}</dd></div>
                </dl>
                <div class="mk-block">
                    <h3>Nội dung</h3>
                    <p>${n.body}</p>
                </div>
                <div class="nt-actions">
                    <a class="mk-btn mk-btn-primary" href="${actionHref(n)}">${n.action}</a>
                    ${readBtn}
                </div>
            </div>`;
        renderPage();
    }

    async function markRead(id) {
        const n = items.find((x) => x.id === Number(id));
        if (!n || !n.unread || !handlers.read) return true;
        const sep = handlers.read.includes("?") ? "&" : "?";
        const ok = await post(handlers.read + sep + "id=" + n.id);
        if (!ok) {
            toast("Không đánh dấu được thông báo. Thử lại sau.");
            return false;
        }
        n.unread = false;
        renderPage();
        if (selectedId === n.id) openDrawer(n.id);
        return true;
    }

    function goDetail(id) {
        if (els.list) openDrawer(id);
        else window.location.href = "/Mockup/Notifications?mo=" + id;
    }

    // ---------- realtime ----------
    // Toast once per (booking, status) per page view: e.g. a deposit payment re-publishes "Pending".
    const toasted = new Set();
    function realtimeText(evt) {
        const p = evt.payload || {};
        const id = evt.bookingId;
        if (id == null) return null;
        switch (evt.eventType) {
            case "BookingStatusChanged":
                return {
                    Pending: `Đơn #${id} mới vừa được tạo — cần xác nhận.`,
                    Cancelled: `Đơn #${id} vừa bị hủy.`,
                    InProgress: `Chuyến #${id} đã bắt đầu.`,
                    Completed: `Chuyến #${id} đã hoàn thành.`
                }[p.status] || null;
            case "TripStatusChanged":
                return p.tripStatus === "Accepted" ? `Tài xế đã nhận chuyến #${id}.` : null;
            case "IncidentReported":
                return `⚠️ Sự cố trên chuyến #${id}${p.incidentType ? ": " + p.incidentType : ""}.`;
            default:
                return null;
        }
    }

    let reloadTimer = null;
    function scheduleReload() {
        // The inbox row is written in the background after the business transaction commits.
        clearTimeout(reloadTimer);
        reloadTimer = setTimeout(load, 1200);
    }

    window.addEventListener("carrental:realtime", (e) => {
        const evt = e.detail || {};
        const text = realtimeText(evt);
        if (!text) return;
        const key = `${evt.eventType}:${evt.bookingId}:${(evt.payload || {}).status || (evt.payload || {}).tripStatus || ""}`;
        if (!toasted.has(key)) {
            toasted.add(key);
            toast(text);
        }
        scheduleReload();
    });
    window.addEventListener("carrental:realtime-reconnected", scheduleReload);

    // ---------- events ----------
    els.btn?.addEventListener("click", (e) => {
        e.stopPropagation();
        const open = els.drop.hidden;
        els.drop.hidden = !open;
        els.btn.setAttribute("aria-expanded", open ? "true" : "false");
        if (open) load();
    });

    els.search?.addEventListener("input", () => {
        query = els.search.value.trim();
        shown = PAGE;
        renderPage();
    });

    els.more?.addEventListener("click", () => {
        shown += PAGE;
        renderPage();
    });

    els.markAll?.addEventListener("click", async () => {
        if (!unreadCount()) { toast("Không có thông báo chưa đọc."); return; }
        if (!handlers.readAll || !(await post(handlers.readAll))) {
            toast("Không đánh dấu được. Thử lại sau.");
            return;
        }
        items.forEach((x) => { x.unread = false; });
        toast("Đã đánh dấu tất cả thông báo là đã đọc.");
        renderPage();
        if (selectedId != null) openDrawer(selectedId);
    });

    document.addEventListener("click", async (e) => {
        if (els.drop && !els.drop.hidden && !e.target.closest(".nt-bell-wrap")) {
            els.drop.hidden = true;
            els.btn?.setAttribute("aria-expanded", "false");
        }
        if (menuId && !e.target.closest(".nt-menu-wrap")) {
            menuId = null;
            renderPage();
        }
        const dropOpen = e.target.closest("[data-drop-open]");
        if (dropOpen) {
            els.drop.hidden = true;
            await markRead(dropOpen.dataset.dropOpen);
            goDetail(dropOpen.dataset.dropOpen);
            return;
        }
        if (e.target.closest("[data-close]") && els.drawer?.contains(e.target)) {
            els.drawer.classList.remove("is-open");
            selectedId = null;
            renderPage();
            return;
        }
        const t = e.target.closest("[data-tab]");
        if (t && (els.tabs?.contains(t) || els.kpis?.contains(t))) {
            tab = t.dataset.tab;
            shown = PAGE;
            renderPage();
            return;
        }
        const menu = e.target.closest("[data-menu]");
        if (menu) {
            e.stopPropagation();
            menuId = menuId === Number(menu.dataset.menu) ? null : Number(menu.dataset.menu);
            renderPage();
            return;
        }
        const read = e.target.closest("[data-read]");
        if (read) {
            e.stopPropagation();
            menuId = null;
            if (await markRead(read.dataset.read)) toast("Đã đánh dấu thông báo là đã đọc.");
            return;
        }
        const open = e.target.closest("[data-open]");
        if (open && els.list?.contains(open) && !e.target.closest(".nt-menu-wrap")) openDrawer(open.dataset.open);
    });

    renderPage();
    load().then(() => {
        const mo = new URLSearchParams(location.search).get("mo");
        if (mo && els.list) openDrawer(mo);
    });
})();
