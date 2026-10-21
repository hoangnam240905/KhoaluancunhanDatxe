// Customer header bell: notifications from /api/notifications (via page handlers in window.__CUSTOMER_NOTIFY__)
// + live toasts from realtime.js ("carrental:realtime"). No demo data.
(function () {
    const cfg = window.__CUSTOMER_NOTIFY__;
    const root = document.getElementById("cnBell");
    if (!cfg || !root) return;

    const MAX_ITEMS = 8;
    const els = {
        count: root.querySelector("[data-cn-count]"),
        list: root.querySelector("[data-cn-list]"),
        readAll: root.querySelector("[data-cn-read-all]"),
        toggle: root.querySelector("[data-cn-toggle]"),
        toast: document.querySelector("[data-cn-toast]")
    };
    let items = [];

    const esc = (s) => String(s ?? "").replace(/[&<>"']/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]));
    const ICON = { booking: "bi-receipt", driver: "bi-person-check", alert: "bi-exclamation-triangle" };

    // CreatedAt is UTC (may come without "Z"): show relative / Vietnam time.
    function when(iso) {
        if (!iso) return "";
        const at = new Date(/[zZ]|[+-]\d{2}:\d{2}$/.test(iso) ? iso : iso + "Z");
        if (Number.isNaN(at.getTime())) return "";
        const mins = Math.floor((Date.now() - at.getTime()) / 60000);
        if (mins < 1) return "Vừa xong";
        if (mins < 60) return `${mins} phút trước`;
        if (mins < 24 * 60) return `${Math.floor(mins / 60)} giờ trước`;
        if (mins < 48 * 60) return "Hôm qua";
        return at.toLocaleDateString("vi-VN", { timeZone: "Asia/Ho_Chi_Minh" });
    }

    function toast(text) {
        if (!els.toast || !text) return;
        els.toast.textContent = text;
        els.toast.classList.add("is-on");
        clearTimeout(toast._t);
        toast._t = setTimeout(() => els.toast.classList.remove("is-on"), 4000);
    }

    function antiforgery() {
        const input = document.querySelector("#cnAntiForgery input[name='__RequestVerificationToken']");
        return input ? input.value : "";
    }

    async function post(url) {
        const res = await fetch(url, {
            method: "POST",
            credentials: "same-origin",
            headers: { "Content-Type": "application/x-www-form-urlencoded", RequestVerificationToken: antiforgery() },
            body: "__RequestVerificationToken=" + encodeURIComponent(antiforgery())
        });
        const data = await res.json().catch(() => null);
        return res.ok && data && data.ok;
    }

    function render() {
        const unread = items.filter((n) => !n.isRead).length;
        els.count.hidden = unread === 0;
        els.count.textContent = unread > 99 ? "99+" : String(unread);
        els.readAll.hidden = unread === 0;
        if (!items.length) {
            els.list.innerHTML = `<p class="cn-empty">Chưa có thông báo nào.</p>`;
            return;
        }
        els.list.innerHTML = items.slice(0, MAX_ITEMS).map((n) => `
            <button type="button" class="ch-notify-item ${n.isRead ? "" : "is-unread"}" data-cn-open="${n.notificationId}">
                <span class="ch-notify-ico"><i class="bi ${ICON[n.type] || ICON.booking}"></i></span>
                <span>
                    <strong>${esc(n.title)}</strong>
                    <span class="ch-notify-desc">${esc(n.message)}</span>
                    <time>${esc(when(n.createdAt))}</time>
                </span>
            </button>`).join("");
    }

    async function load() {
        try {
            const res = await fetch(cfg.list, { credentials: "same-origin", headers: { Accept: "application/json" } });
            const data = await res.json().catch(() => null);
            if (!res.ok || !data || !data.ok) throw new Error((data && data.error) || `HTTP ${res.status}`);
            items = data.items || [];
            render();
        } catch (err) {
            els.list.innerHTML = `<p class="cn-empty">Không tải được thông báo.</p>`;
            console.warn("[CustomerNotify]", err);
        }
    }

    root.addEventListener("click", async (e) => {
        const open = e.target.closest("[data-cn-open]");
        if (open) {
            const n = items.find((x) => x.notificationId === Number(open.dataset.cnOpen));
            if (!n) return;
            if (!n.isRead) {
                const sep = cfg.read.includes("?") ? "&" : "?";
                if (await post(cfg.read + sep + "id=" + n.notificationId)) n.isRead = true;
                render();
            }
            if (n.bookingId) window.location.href = cfg.bookingUrl.replace(/\/0$/, "/" + n.bookingId);
            return;
        }
        if (e.target.closest("[data-cn-read-all]")) {
            if (await post(cfg.readAll)) {
                items.forEach((n) => { n.isRead = true; });
                render();
            }
        }
    });
    els.toggle?.addEventListener("click", load);

    // ---------- realtime (events for this customer's own bookings) ----------
    const shown = new Set();
    function realtimeText(evt) {
        const p = evt.payload || {};
        const id = evt.bookingId;
        if (id == null) return null;
        if (evt.eventType === "TripStatusChanged")
            return p.tripStatus === "Accepted" ? `Tài xế đã nhận chuyến #${id} của bạn.` : null;
        if (evt.eventType !== "BookingStatusChanged") return null;
        return {
            Confirmed: `Đơn #${id} đã được điều phối viên xác nhận.`,
            Assigned: `Đơn #${id} đã được phân công xe.`,
            InProgress: `Chuyến #${id} đã bắt đầu.`,
            Completed: `Chuyến #${id} đã hoàn thành. Hãy đánh giá chuyến đi nhé!`,
            Cancelled: `Đơn #${id} đã được hủy.`
        }[p.status] || null;
    }

    let reloadTimer = null;
    window.addEventListener("carrental:realtime", (e) => {
        const evt = e.detail || {};
        const text = realtimeText(evt);
        if (!text) return;
        const key = `${evt.eventType}:${evt.bookingId}:${(evt.payload || {}).status || (evt.payload || {}).tripStatus || ""}`;
        if (!shown.has(key)) {
            shown.add(key);
            toast(text);
        }
        // The inbox row is written in the background after the business transaction commits.
        clearTimeout(reloadTimer);
        reloadTimer = setTimeout(load, 1200);
    });
    window.addEventListener("carrental:realtime-reconnected", load);

    load();
})();
