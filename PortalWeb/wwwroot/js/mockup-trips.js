(function () {
    const boot = window.__TRIPS_BOOTSTRAP__;
    const handlers = window.__TRIPS_HANDLERS__ || {};
    const ready = window.__TRIPS_READY__ === true;

    if (!boot || typeof boot !== "object" || !Array.isArray(boot.trips) || !ready) {
        const queue = document.getElementById("opQueue");
        if (queue) {
            queue.innerHTML = `<p class="mk-empty">Trips chưa nhận bootstrap API. Rebuild/restart PortalWeb (tránh DLL cũ + wwwroot mới).</p>`;
        }
        console.error("[Trips] Missing __TRIPS_BOOTSTRAP__ — aborting.");
        return;
    }

    const STATUS = {
        waitingHandover: "Chờ giao xe",
        handedOver: "Đã giao xe",
        accepted: "Đã nhận chuyến",
        inProgress: "Đang thực hiện",
        returning: "Đang trả xe",
        completed: "Hoàn thành",
        incident: "Có sự cố"
    };
    const MODE_LABEL = { SelfDrive: "Tự lái", WithDriver: "Có tài xế" };
    const FLOW = ["Tạo đơn thuê", "Xác nhận", "Phân công", "Giao xe", "Nhận chuyến", "Đang thực hiện", "Trả xe", "Quyết toán", "Hoàn thành"];
    const FLOW_AT = {
        waitingHandover: 3,
        handedOver: 4,
        accepted: 5,
        inProgress: 5,
        returning: 6,
        completed: 8,
        incident: 5
    };
    const PAGE = 5;

    let trips = boot.trips.slice();

    // metric: active metric-card filter (AND with the toolbar filters), "" = none.
    const state = { q: "", status: "", mode: "", dstatus: "", vehicle: "", metric: "", selectedId: null, page: 1, modal: null };
    const els = {
        form: document.getElementById("opFilter"),
        vehicle: document.getElementById("opVehicleFilter"),
        table: document.getElementById("opTableBody"),
        pager: document.getElementById("opPager"),
        meta: document.getElementById("opTableMeta"),
        queue: document.getElementById("opQueue"),
        queueCount: document.getElementById("opQueueCount"),
        timeline: document.getElementById("opTimeline"),
        timelineDate: document.getElementById("opTimelineDate"),
        metrics: document.getElementById("opMetrics"),
        drawer: document.getElementById("opDrawer"),
        modal: document.getElementById("opModal"),
        modalBody: document.getElementById("opModalBody"),
        toast: document.getElementById("opToast"),
        metricToday: document.getElementById("opMetricToday"),
        metricWaiting: document.getElementById("opMetricWaiting"),
        metricInProgress: document.getElementById("opMetricInProgress"),
        metricIncidents: document.getElementById("opMetricIncidents"),
        metricSettlement: document.getElementById("opMetricSettlement")
    };

    const money = (n) => Number(n || 0).toLocaleString("vi-VN") + " ₫";
    const trip = (id) => trips.find((t) => t.id === Number(id));
    const badge = (st) => `<span class="mk-badge mk-badge-${String(st || "").toLowerCase()}">${STATUS[st] || st}</span>`;
    const settleBadge = (s) => s === "settled"
        ? `<span class="mk-badge mk-badge-settled">Đã quyết toán</span>`
        : s === "pending"
            ? `<span class="mk-badge mk-badge-pending">Chờ quyết toán</span>`
            : `<span class="mk-badge mk-badge-gray">—</span>`;
    // ---------- Quy ước dùng chung (plan1.md mục 1b) ----------
    // Trips still running: an open incident does not end the trip, it still has a return time.
    const RUNNING = ["handedOver", "accepted", "inProgress", "incident"];
    const hasIncident = (t) => t.incident != null;
    const MIN = 60000;
    const SOON_MIN = 120;          // "sắp tới" window
    const HANDOVER_GRACE_MIN = 30; // handover counts as late after start + 30'
    const QUEUE_MAX = 8;
    const WEEKDAYS = ["Chủ Nhật", "Thứ Hai", "Thứ Ba", "Thứ Tư", "Thứ Năm", "Thứ Sáu", "Thứ Bảy"];
    const pad = (n) => String(n).padStart(2, "0");

    // startIso/endIso are Vietnam wall-clock without offset (Booking.StartDate). Both they and
    // nowVn() live in the same "UTC fields = Vietnam wall clock" frame, so the browser time zone never matters.
    const VN_CLOCK = new Intl.DateTimeFormat("en-CA", {
        timeZone: "Asia/Ho_Chi_Minh", hourCycle: "h23",
        year: "numeric", month: "2-digit", day: "2-digit", hour: "2-digit", minute: "2-digit", second: "2-digit"
    });
    function nowVn() {
        const p = Object.fromEntries(VN_CLOCK.formatToParts(new Date()).map((x) => [x.type, x.value]));
        return new Date(Date.UTC(+p.year, +p.month - 1, +p.day, +p.hour, +p.minute, +p.second));
    }
    function parseNaive(iso) {
        const m = /^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2})(?::(\d{2}))?/.exec(iso || "");
        return m ? new Date(Date.UTC(+m[1], +m[2] - 1, +m[3], +m[4], +m[5], +(m[6] || 0))) : null;
    }
    const sameDay = (a, b) => !!a && !!b && a.getUTCFullYear() === b.getUTCFullYear()
        && a.getUTCMonth() === b.getUTCMonth() && a.getUTCDate() === b.getUTCDate();
    const hhmm = (d) => d ? `${pad(d.getUTCHours())}:${pad(d.getUTCMinutes())}` : "—";
    function fmtDur(min) {
        if (min < 60) return `${min} phút`;
        const h = Math.floor(min / 60), m = min % 60;
        return m ? `${h} giờ ${m} phút` : `${h} giờ`;
    }
    const soonText = (min) => min > 0 ? `còn ${fmtDur(min)}` : "đã đến giờ";
    function dayText(d, now) {
        if (!d) return "—";
        if (sameDay(d, now)) return hhmm(d);
        const yesterday = new Date(now.getTime() - 24 * 60 * MIN);
        return sameDay(d, yesterday) ? "Hôm qua" : `${pad(d.getUTCDate())}/${pad(d.getUTCMonth() + 1)}`;
    }
    // Incident time comes pre-formatted "dd/MM/yyyy HH:mm": keep only HH:mm when it is today.
    function incidentTime(text, now) {
        const m = /^(\d{2})\/(\d{2})\/(\d{4}) (\d{2}:\d{2})/.exec(text || "");
        if (!m) return text || "";
        return +m[1] === now.getUTCDate() && +m[2] === now.getUTCMonth() + 1 && +m[3] === now.getUTCFullYear()
            ? m[4] : `${m[1]}/${m[2]} ${m[4]}`;
    }
    function tableTime(iso, now) {
        const d = parseNaive(iso);
        if (!d) return "—";
        return sameDay(d, now) ? hhmm(d) : `${pad(d.getUTCDate())}/${pad(d.getUTCMonth() + 1)} ${hhmm(d)}`;
    }

    /** One time state per trip, shared by the queue, the timeline and the table badge. */
    function timeState(t, now) {
        if (t.status === "waitingHandover") {
            const at = parseNaive(t.startIso);
            if (!at) return null;
            const after = (now - at) / MIN;
            if (after > HANDOVER_GRACE_MIN) return { kind: "handoverLate", at, minutes: Math.round(after) };
            if (after >= -SOON_MIN) return { kind: "handoverSoon", at, minutes: Math.max(0, Math.round(-after)) };
            return null;
        }
        if (RUNNING.includes(t.status)) {
            const at = parseNaive(t.endIso);
            if (!at) return null;
            const after = (now - at) / MIN;
            if (after > 0) return { kind: "returnLate", at, minutes: Math.max(1, Math.round(after)) };
            if (after >= -SOON_MIN) return { kind: "returnSoon", at, minutes: Math.round(-after) };
        }
        return null;
    }

    // Metric card = one predicate, used for both its count and its filter (count == rows when clicked).
    const METRIC_FILTER = {
        today: (t, now) => sameDay(parseNaive(t.startIso), now),
        waiting: (t) => t.status === "waitingHandover",
        running: (t) => RUNNING.includes(t.status),
        incident: (t) => hasIncident(t),
        settle: (t) => t.settlement === "pending"
    };


    function esc(s) {
        return String(s ?? "").replace(/[&<>"']/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]));
    }

    function antiforgery() {
        const input = document.querySelector("#opAntiForgery input[name='__RequestVerificationToken']");
        return input ? input.value : "";
    }

    async function postForm(url, fields) {
        const body = new URLSearchParams();
        Object.keys(fields || {}).forEach((k) => {
            if (fields[k] != null && fields[k] !== "") body.set(k, String(fields[k]));
        });
        body.set("__RequestVerificationToken", antiforgery());
        const res = await fetch(url, {
            method: "POST",
            headers: {
                "Content-Type": "application/x-www-form-urlencoded",
                RequestVerificationToken: antiforgery()
            },
            credentials: "same-origin",
            body: body.toString()
        });
        let data = null;
        try { data = await res.json(); } catch { /* ignore */ }
        return { ok: res.ok && data && data.ok !== false, data };
    }

    async function getJson(url) {
        const res = await fetch(url, { credentials: "same-origin" });
        let data = null;
        try { data = await res.json(); } catch { /* ignore */ }
        return { ok: res.ok && data && data.ok !== false, data };
    }

    // Server metrics only seed the first paint; counts are recomputed here with METRIC_FILTER.
    function renderMetrics(now) {
        const count = (key) => trips.filter((t) => METRIC_FILTER[key](t, now)).length;
        els.metricToday.textContent = count("today");
        els.metricWaiting.textContent = count("waiting");
        els.metricInProgress.textContent = count("running");
        els.metricIncidents.textContent = count("incident");
        els.metricSettlement.textContent = count("settle");
        els.metrics.querySelectorAll("[data-metric]").forEach((b) => {
            const on = b.dataset.metric === state.metric;
            b.classList.toggle("is-active", on);
            b.setAttribute("aria-pressed", on ? "true" : "false");
        });
    }

    function upsertTrip(row) {
        if (!row || !row.id) return;
        const i = trips.findIndex((t) => t.id === row.id);
        if (i >= 0) trips[i] = { ...trips[i], ...row };
        else trips.unshift(row);
    }

    async function reloadFromServer() {
        if (!handlers.reload) return;
        const { ok, data } = await getJson(handlers.reload);
        if (!ok || !data || !data.data) return;
        trips = Array.isArray(data.data.trips) ? data.data.trips : trips;
        fillVehicles();
    }

    function toast(msg) {
        els.toast.textContent = msg;
        els.toast.classList.add("is-on");
        clearTimeout(toast._t);
        toast._t = setTimeout(() => els.toast.classList.remove("is-on"), 2800);
    }

    function visible(now) {
        return trips.filter((t) => {
            if (state.metric && !METRIC_FILTER[state.metric](t, now)) return false;
            if (state.status && t.status !== state.status) return false;
            if (state.mode && t.mode !== state.mode) return false;
            if (state.vehicle && t.vehicle !== state.vehicle) return false;
            if (state.dstatus === "Busy" && !t.driverBusy && t.mode === "WithDriver") return false;
            if (state.dstatus === "Available" && t.driverBusy) return false;
            if (state.dstatus === "Available" && t.mode === "SelfDrive") return false;
            if (state.q) {
                const q = state.q.toLowerCase();
                const blob = `#${t.id} ${t.customer} ${t.vehicle} ${t.plate} ${t.driver || ""} ${t.mode}`.toLowerCase();
                if (!blob.includes(q)) return false;
            }
            return true;
        });
    }

    function fillVehicles() {
        const names = [...new Set(trips.map((t) => t.vehicle).filter(Boolean))];
        els.vehicle.innerHTML = `<option value="">Tất cả</option>` + names.map((n) => `<option>${n}</option>`).join("");
    }

    // ---------- Ca trực: hàng đợi hành động + timeline (plan1.md) ----------

    function itemHead(t) {
        return `<span>#${t.id} · ${t.vehicle}${t.plate && t.plate !== "—" ? ` (${t.plate})` : ""}</span>`;
    }

    // One item per trip: rules are tried in a fixed priority order, the first match wins.
    function queueItem(t, now) {
        const ts = timeState(t, now);
        const self = t.mode === "SelfDrive";
        const where = `<span class="mk-muted">KH: ${t.customer} · Điểm hẹn: ${t.pickup || "—"}</span>`;
        const btn = (cls, attr, label) => `<button type="button" class="mk-btn ${cls}" ${attr}="${t.id}">${label}</button>`;

        if (t.dispute) {
            return {
                sev: 0, rank: 0, at: parseNaive(t.startIso), icon: "bi-exclamation-triangle-fill",
                title: `Tài xế báo sai lệch biên bản #${t.id}`,
                sub: `<span>${esc(t.dispute.note)}</span><span class="mk-muted">${esc(t.dispute.by)}</span>`,
                time: hhmm(parseNaive(t.startIso)), note: "",
                action: btn("mk-btn-primary", "data-precheck", "Xử lý sai lệch")
            };
        }
        if (hasIncident(t)) {
            return {
                sev: 0, rank: 1, at: parseNaive(t.startIso), icon: "bi-exclamation-triangle-fill",
                title: `Sự cố chuyến #${t.id}`,
                sub: `<span>${t.incident.summary || t.incident.type || "Có sự cố đang mở"}</span><span class="mk-muted">KH: ${t.customer} · ${STATUS[t.status] || t.status}</span>`,
                time: incidentTime(t.incident.time, now), note: "",
                action: btn("mk-btn-primary", "data-incident", "Xem chi tiết")
            };
        }
        if (ts && ts.kind === "handoverLate") {
            return {
                sev: 0, rank: 2, at: ts.at, icon: "bi-exclamation-triangle-fill",
                title: self ? `Chờ giao xe quá ${fmtDur(ts.minutes)}` : `Tài xế chưa nhận chuyến, quá ${fmtDur(ts.minutes)}`,
                sub: itemHead(t) + where, time: hhmm(ts.at), note: `quá ${fmtDur(ts.minutes)}`,
                action: self
                    ? btn("mk-btn-primary", "data-handover", `<i class="bi bi-key"></i> Giao xe ngay`)
                    : btn("mk-btn-primary", "data-precheck", t.pre
                        ? `<i class="bi bi-hourglass-split"></i> Xem biên bản trước giao`
                        : `<i class="bi bi-clipboard-check"></i> Kiểm tra trước giao`)
            };
        }
        if (ts && ts.kind === "returnLate") {
            return {
                sev: 0, rank: 2, at: ts.at, icon: "bi-exclamation-triangle-fill",
                title: self ? `Quá hạn trả xe ${fmtDur(ts.minutes)}` : `Quá giờ kết thúc chuyến ${fmtDur(ts.minutes)}`,
                sub: itemHead(t) + `<span class="mk-muted">KH: ${t.customer} · Điểm hẹn: ${t.dropoff || "—"}</span>`,
                time: hhmm(ts.at), note: `quá ${fmtDur(ts.minutes)}`,
                action: self
                    ? btn("mk-btn-primary", "data-return", `<i class="bi bi-arrow-counterclockwise"></i> Tiếp nhận trả xe`)
                    : btn("mk-btn-outline", "data-contact", "Liên hệ tài xế")
            };
        }
        if (ts && ts.kind === "handoverSoon") {
            return {
                sev: 1, rank: 3, at: ts.at, icon: "bi-clock",
                title: self ? "Sắp đến giờ giao xe" : "Sắp đến giờ đón khách",
                sub: itemHead(t) + where, time: hhmm(ts.at), note: soonText(ts.minutes),
                action: btn("mk-btn-outline", "data-open", "Chi tiết")
            };
        }
        if (ts && ts.kind === "returnSoon") {
            return {
                sev: 1, rank: 3, at: ts.at, icon: "bi-clock",
                title: "Sắp đến hạn trả xe",
                sub: itemHead(t) + `<span class="mk-muted">KH: ${t.customer} · Điểm hẹn: ${t.dropoff || "—"}</span>`,
                time: hhmm(ts.at), note: soonText(ts.minutes),
                action: btn("mk-btn-outline", "data-open", "Chuẩn bị")
            };
        }
        if (t.settlement === "pending") {
            const end = parseNaive(t.endIso);
            return {
                sev: 1, rank: 4, at: end, icon: "bi-receipt",
                title: "Chờ quyết toán",
                sub: itemHead(t) + `<span class="mk-muted">KH: ${t.customer}</span>`,
                time: dayText(end, now), note: "",
                action: btn("mk-btn-outline", "data-open", "Xem chi tiết")
            };
        }
        return null;
    }

    function renderQueue(now) {
        const items = trips
            .map((t) => ({ t, q: queueItem(t, now) }))
            .filter((x) => x.q)
            .sort((a, b) => a.q.sev - b.q.sev || a.q.rank - b.q.rank || (a.q.at?.getTime() ?? 0) - (b.q.at?.getTime() ?? 0));
        els.queueCount.hidden = items.length === 0;
        els.queueCount.textContent = String(items.length);
        if (!items.length) {
            els.queue.innerHTML = `<p class="op-empty">Không có việc cần xử lý ngay.</p>`;
            return;
        }
        els.queue.innerHTML = items.slice(0, QUEUE_MAX).map(({ t, q }) => `
            <div class="op-queue-item ${q.sev === 0 ? "is-red" : "is-amber"}" data-open="${t.id}">
                <i class="op-queue-icon bi ${q.icon}"></i>
                <div class="op-queue-main"><strong>${q.title}</strong>${q.sub}</div>
                <div class="op-queue-time"><strong>${q.time}</strong>${q.note ? `<span>${q.note}</span>` : ""}</div>
                <div class="op-queue-action">${q.action}</div>
            </div>`).join("")
            + (items.length > QUEUE_MAX ? `<p class="mk-muted op-queue-more">+${items.length - QUEUE_MAX} việc khác — xem trong bảng.</p>` : "");
    }

    // Timeline ignores the table filters: it is the whole shift.
    function renderTimeline(now) {
        els.timelineDate.textContent = `${WEEKDAYS[now.getUTCDay()]}, ${pad(now.getUTCDate())}/${pad(now.getUTCMonth() + 1)}/${now.getUTCFullYear()}`;
        const events = [];
        trips.forEach((t) => {
            const ts = timeState(t, now);
            const self = t.mode === "SelfDrive";
            if (t.status === "waitingHandover") {
                const at = parseNaive(t.startIso);
                if (at && (sameDay(at, now) || ts?.kind === "handoverLate")) {
                    events.push({ t, at, ts, kind: "handover", title: self ? `Giao xe #${t.id}` : `Tài xế nhận chuyến #${t.id}`, place: t.pickup });
                }
            } else if (RUNNING.includes(t.status)) {
                const at = parseNaive(t.endIso);
                if (at && (sameDay(at, now) || ts?.kind === "returnLate")) {
                    events.push({ t, at, ts, kind: "return", title: self ? `Nhận trả xe #${t.id}` : `Kết thúc chuyến #${t.id}`, place: t.dropoff });
                }
            }
        });
        events.sort((a, b) => a.at - b.at);
        if (!events.length) {
            els.timeline.innerHTML = `<p class="op-empty">Hôm nay không có lịch giao/nhận xe.</p>`;
            return;
        }
        els.timeline.innerHTML = events.map((e) => {
            const late = e.ts && (e.ts.kind === "handoverLate" || e.ts.kind === "returnLate");
            const soon = e.ts && (e.ts.kind === "handoverSoon" || e.ts.kind === "returnSoon");
            const badgeHtml = late
                ? `<span class="op-tl-badge is-red">Quá hạn ${fmtDur(e.ts.minutes)}</span>`
                : soon
                    ? `<span class="op-tl-badge is-blue">${e.kind === "handover" ? "Sắp đến giờ" : "Sắp đến hạn"}</span><small>${soonText(e.ts.minutes)}</small>`
                    : "";
            return `
            <div class="op-tl-item ${late ? "is-overdue" : soon ? "is-soon" : ""}" data-open="${e.t.id}">
                <span class="op-tl-time">${sameDay(e.at, now) ? hhmm(e.at) : `${pad(e.at.getUTCDate())}/${pad(e.at.getUTCMonth() + 1)}<br>${hhmm(e.at)}`}</span>
                <span class="op-tl-dot"></span>
                <span class="op-tl-icon"><i class="bi ${e.kind === "handover" ? "bi-car-front-fill" : "bi-arrow-counterclockwise"}"></i></span>
                <div class="op-tl-body">
                    <strong>${e.title}</strong>
                    <span>${e.t.vehicle}${e.t.plate && e.t.plate !== "—" ? ` (${e.t.plate})` : ""}</span>
                    <span>KH: ${e.t.customer}</span>
                    <span>Điểm hẹn: ${e.place || "—"}</span>
                </div>
                <div class="op-tl-state">${badgeHtml}</div>
                <i class="bi bi-chevron-right op-tl-chev"></i>
            </div>`;
        }).join("");
    }

    function driverCell(t) {
        if (t.mode === "SelfDrive") return `<span class="mk-muted">Tự lái</span>`;
        if (!t.driver) return "—";
        const st = String(t.driverStatus || "").toLowerCase();
        const chip = st === "busy"
            ? `<span class="op-chip op-chip-busy">Bận</span>`
            : st === "available"
                ? `<span class="op-chip op-chip-free">Rảnh</span>`
                : ""; // no fleet data -> no chip
        return `${t.driver} ${chip}`;
    }

    function statusCell(t, now) {
        if (hasIncident(t)) return `<span class="mk-badge mk-badge-incident">Có sự cố</span>`;
        const ts = timeState(t, now);
        if (ts?.kind === "returnLate") return `<span class="mk-badge mk-badge-incident">Quá hạn trả xe</span>`;
        if (ts?.kind === "handoverLate") return `<span class="mk-badge mk-badge-incident">Quá hạn giao xe</span>`;
        if (ts?.kind === "handoverSoon") return `<span class="mk-badge mk-badge-handedover">Sắp giao xe</span>`;
        return badge(t.status);
    }

    function renderTable(now) {
        const list = visible(now);
        const pages = Math.max(1, Math.ceil(list.length / PAGE));
        if (state.page > pages) state.page = pages;
        const slice = list.slice((state.page - 1) * PAGE, state.page * PAGE);
        els.meta.textContent = `${list.length} chuyến từ Backend · trang ${state.page}/${pages}`;
        els.table.innerHTML = slice.map((t) => `
            <tr data-open="${t.id}">
                <td><strong>#${t.id}</strong></td>
                <td>${t.customer}</td>
                <td>${t.vehicle}<br><span class="mk-muted">${t.plate}</span></td>
                <td>${driverCell(t)}</td>
                <td>${t.pickup || "—"}</td>
                <td>${t.dropoff || "—"}</td>
                <td>${tableTime(t.startIso, now)}</td>
                <td>${tableTime(t.endIso, now)}</td>
                <td>${statusCell(t, now)}</td>
                <td>${settleBadge(t.settlement)}</td>
                <td><button type="button" class="mk-btn mk-btn-outline" data-open="${t.id}">Xem</button></td>
            </tr>`).join("") || `<tr><td colspan="11">Không có chuyến khớp bộ lọc.</td></tr>`;
        els.pager.innerHTML = Array.from({ length: pages }, (_, i) =>
            `<button type="button" class="mk-btn mk-btn-ghost ${state.page === i + 1 ? "is-on" : ""}" data-page="${i + 1}">${i + 1}</button>`
        ).join("");
    }

    function condBlock(ret, handover, mode) {
        if (!ret) return "";
        const km = handover && mode === "SelfDrive" ? (ret.odo - handover.odo) : null;
        const warnEx = ret.exterior === "Cần kiểm tra";
        const warnTech = ret.tech === "Cần kiểm tra";
        return `<div class="op-cond">
            <div><span>🛞 Odometer</span><strong>${Number(ret.odo || 0).toLocaleString("vi-VN")} km</strong></div>
            <div><span>⛽ Fuel</span><strong>${ret.fuel}%</strong></div>
            <div class="${warnEx ? "warn" : ""}"><span>🚗 Exterior</span><strong>${ret.exterior}${warnEx ? " ⚠️" : ""}</strong></div>
            <div class="${warnTech ? "warn" : ""}"><span>🔧 Technical</span><strong>${ret.tech}${warnTech ? " ⚠️" : ""}</strong></div>
        </div>${ret.by ? `<p class="mk-note">Người lập: ${esc(ret.by)} · ${ret.at || ""}</p>` : ""}${km !== null ? `<p class="mk-note">Km thực tế: ${km.toLocaleString("vi-VN")} km = trả − giao (từ biên bản API).</p>` : ""}`;
    }

    function quickActions(t) {
        const a = [`<button type="button" class="mk-btn mk-btn-ghost" data-contact="${t.id}">📞 Liên hệ</button>`];
        if (t.bookingStatus === "Assigned" || t.bookingStatus === "InProgress") {
            a.push(`<button type="button" class="mk-btn mk-btn-danger" data-report="${t.id}">⚠️ Báo sự cố</button>`);
        }
        a.push(`<button type="button" class="mk-btn mk-btn-ghost" data-inspect="${t.id}">📋 Inspection</button>`);
        if (t.canHandoverApi || t.status === "waitingHandover") {
            a.push(t.mode === "SelfDrive"
                ? `<button type="button" class="mk-btn mk-btn-primary" data-handover="${t.id}">🚗 Giao xe</button>`
                : `<button type="button" class="mk-btn mk-btn-primary" data-precheck="${t.id}">${t.pre ? "✏️ Sửa biên bản trước giao" : "📝 Kiểm tra trước giao"}</button>`);
        }
        if (t.canCompleteApi || t.status === "handedOver" || t.status === "inProgress" || t.status === "returning" || t.status === "waitingHandover" || t.status === "accepted") {
            a.push(`<button type="button" class="mk-btn mk-btn-primary" data-return="${t.id}">↩ Trả xe</button>`);
        }
        if (t.bookingStatus === "Assigned" || t.bookingStatus === "InProgress") {
            a.push(`<button type="button" class="mk-btn mk-btn-ghost" data-vehicle-change="${t.id}">🔁 Đổi xe</button>`);
        }
        if (t.settlement === "pending" || t.status === "completed") {
            a.push(`<button type="button" class="mk-btn mk-btn-ghost" data-settle="${t.id}">💰 Quyết toán</button>`);
        }
        return a.join("");
    }

    function renderDrawer(t) {
        const step = FLOW_AT[t.status] ?? 0;
        const flow = FLOW.map((name, i) => {
            let cls = i < step ? "done" : i === step ? "now" : "";
            if (t.status === "incident" && name === "Đang thực hiện") cls = "warn";
            return `<li class="${cls}">${name}</li>`;
        }).join("");
        els.drawer.classList.add("is-open");
        els.drawer.setAttribute("aria-hidden", "false");
        els.drawer.innerHTML = `
            <div class="mk-drawer-head">
                <div>
                    <h2 class="h5 mb-2">Đơn #${t.id}</h2>
                    ${badge(t.status)} ${t.mode === "WithDriver" ? '<span class="mk-badge mk-badge-assigned">Có tài xế</span>' : '<span class="mk-badge mk-badge-gray">Tự lái</span>'}
                    <span class="mk-badge mk-badge-ok">API</span>
                </div>
                <button type="button" class="mk-btn mk-btn-ghost" data-close>Đóng</button>
            </div>
            <div class="mk-drawer-body">
                <section class="mk-block">
                    <h3>Thao tác nhanh</h3>
                    <div class="mk-actions">${quickActions(t)}</div>
                </section>
                <section class="mk-block">
                    <h3>Chi tiết</h3>
                    <dl class="mk-dl">
                        <div><dt>Khách hàng</dt><dd>${t.customer}</dd></div>
                        <div><dt>Xe</dt><dd>${t.vehicle}<br>${t.plate}</dd></div>
                        <div><dt>Tài xế</dt><dd>${t.driver || "—"}</dd></div>
                        <div><dt>Điểm nhận</dt><dd>${t.pickup}</dd></div>
                        <div><dt>Điểm trả</dt><dd>${t.dropoff}</dd></div>
                        <div><dt>Bắt đầu</dt><dd>${t.start}</dd></div>
                        <div><dt>Dự kiến trả</dt><dd>${t.expectedReturn}</dd></div>
                        <div><dt>Trạng thái hiện tại</dt><dd>${STATUS[t.status] || t.status} <span class="mk-muted">(${t.bookingStatus || ""})</span></dd></div>
                        <div><dt>Tình trạng xe</dt><dd>${t.vehicleState}</dd></div>
                    </dl>
                    ${t.mode === "WithDriver" ? '<p class="mk-note">Có tài xế: điều phối ghi biên bản trước giao, tài xế xác nhận trên app để bắt đầu chuyến và gửi phiếu trả khi kết thúc.</p>' : ""}
                </section>
                <section class="mk-block">
                    <h3>Luồng vận hành</h3>
                    <ul class="op-flow">${flow}</ul>
                </section>
                <section class="mk-block">
                    <h3>Nhật ký vận hành</h3>
                    <ul class="mk-timeline">${(t.ops || []).map((o) => `<li class="${o.cls || ""}"><strong>${o.t}</strong> ${o.text}</li>`).join("") || "<li class='mk-muted'>Chưa có sự kiện</li>"}</ul>
                </section>
                ${t.ret ? `<section class="mk-block"><h3>Tình trạng xe (trả)</h3>${condBlock(t.ret, t.handover, t.mode)}</section>` : ""}
                ${t.pre && !t.handover ? `<section class="mk-block"><h3>Biên bản kiểm tra trước giao</h3>${condBlock(t.pre, null, t.mode)}${t.pre.edited ? `<p class="mk-note">Người sửa: ${esc(t.pre.edited)}</p>` : ""}<p class="mk-note">${t.dispute ? "⚠️ Tài xế báo sai lệch: " + esc(t.dispute.note) : "Chờ tài xế kiểm tra xe thật và xác nhận để bắt đầu chuyến."}</p></section>` : ""}
                ${t.handover && !t.ret ? `<section class="mk-block"><h3>Biên bản giao xe</h3>${condBlock(t.handover, null, t.mode)}</section>` : ""}
                ${t.incident ? `<p class="mk-note">⚠️ ${t.incident.summary} · ${t.incident.time}</p>` : ""}
            </div>`;
    }

    function closeModal() {
        els.modal.hidden = true;
        els.modalBody.innerHTML = "";
        state.modal = null;
    }

    function openModal(html) {
        els.modal.hidden = false;
        els.modalBody.innerHTML = html;
    }

    /** Present numeric check — treats 0 as valid (do NOT use !value). */
    function isPresentNumber(v) {
        return v !== null && v !== undefined && v !== "" && Number.isFinite(Number(v));
    }
    function isValidKm(v) {
        return isPresentNumber(v) && Number(v) >= 0;
    }
    function isValidFuelPercent(v) {
        return isPresentNumber(v) && Number(v) >= 0 && Number(v) <= 100;
    }

    function parseHandoverFuel(raw) {
        if (raw === null || raw === undefined) return null;
        const v = String(raw).trim();
        if (v === "") return null;
        const n = Number(v);
        if (!Number.isFinite(n) || n < 0 || n > 100) return null;
        return n; // 0 is valid
    }

    function syncTripsHandoverConfirm() {
        const btn = document.querySelector("[data-handover-submit]");
        if (!btn || !state.handoverPayload) return;
        // Fuel is always typed (pre-filled with the last known level); KM stays from the vehicle record.
        const input = document.querySelector("[data-handover-fuel-input]");
        const fuel = parseHandoverFuel(input?.value);
        state.handoverPayload.fuelLevel = fuel;
        btn.disabled = fuel === null || !isValidKm(state.handoverPayload.odometerKm);
    }

    // plan1.md Phần E: the customer's declared licence + the "original checked" tick required to hand over.
    function licenceCheckBlock(l) {
        if (!l) return "";
        const status = { Approved: "Đã duyệt", Pending: "Chờ duyệt", Rejected: "Chưa được chấp nhận", None: "Chưa khai" }[l.status] || l.status;
        return `<div class="mk-block" style="border-left:3px solid #2563eb;padding-left:.6rem;margin-top:.6rem">
            <h3>Bằng lái của khách</h3>
            <dl class="mk-dl">
                <div><dt>Số bằng</dt><dd>${esc(l.number || "—")}</dd></div>
                <div><dt>Hạng</dt><dd>${esc(l.cls || "—")}</dd></div>
                <div><dt>Hết hạn</dt><dd>${esc(l.expiry || "—")}${l.expired ? " (đã hết hạn)" : ""}</dd></div>
                <div><dt>Duyệt</dt><dd>${esc(status)}</dd></div>
            </dl>
            <label style="display:flex;gap:.5rem;align-items:flex-start;font-weight:600">
                <input type="checkbox" data-handover-licence-check style="margin-top:.25rem" />
                <span>Đã kiểm tra bằng lái gốc: khớp số bằng, hạng và ảnh đã duyệt, còn hạn.</span>
            </label>
            <p class="mk-note">Không khớp hoặc hết hạn thì không giao xe; chuyển đơn để điều phối xử lý (hủy, hoàn cọc theo quy định). Hệ thống lưu tên bạn là người kiểm tra.</p>
        </div>`;
    }
    async function handoverForm(t) {
        if (!t.canHandoverApi) {
            toast("Chỉ giao xe khi đơn đã được gán xe.");
            return;
        }
        openModal(`
            <h2>Xác nhận giao xe</h2>
            <p class="mk-note">Đang tải dữ liệu xe...</p>
            <div class="mk-actions">
                <button type="button" class="mk-btn mk-btn-ghost" data-close-modal>Hủy</button>
                <button type="button" class="mk-btn mk-btn-success" data-handover-submit disabled>Xác nhận giao xe</button>
            </div>`);
        state.modal = "handover";
        state.handoverPayload = null;

        try {
            const url = new URL(handlers.handoverInfo, location.origin);
            url.searchParams.set("id", String(t.id));
            const res = await fetch(url.toString(), { headers: { Accept: "application/json" }, credentials: "same-origin" });
            const data = await res.json().catch(() => ({ ok: false, error: "Phản hồi không hợp lệ." }));
            if (!data.ok) {
                openModal(`
                    <h2>Xác nhận giao xe</h2>
                    <p class="mk-note" style="color:#b91c1c">${data.error || "Không thể tải dữ liệu giao xe."}</p>
                    <div class="mk-actions"><button type="button" class="mk-btn mk-btn-ghost" data-close-modal>Đóng</button></div>`);
                return;
            }
            const kmOk = isValidKm(data.currentKm) && !data.blockReason;
            const requiresFuelInput = !!data.requiresFuelInput;
            const knownFuelOk = !requiresFuelInput && isValidFuelPercent(data.fuelLevel);

            state.handoverPayload = kmOk
                ? {
                    id: t.id,
                    odometerKm: Number(data.currentKm),
                    fuelLevel: knownFuelOk ? Number(data.fuelLevel) : null,
                    requiresFuelInput
                }
                : null;

            const kmText = kmOk ? `${Number(data.currentKm).toLocaleString("vi-VN")} km` : "—";
            let fuelBlock;
            if (!kmOk) {
                fuelBlock = `<div><dt>Mức nhiên liệu</dt><dd>—</dd></div>`;
            } else {
                fuelBlock = `<div><dt>Mức nhiên liệu khi giao xe (%)</dt><dd>
                    <input type="number" min="0" max="100" step="1" placeholder="Nhập %" value="${knownFuelOk ? Number(data.fuelLevel) : ""}"
                           inputmode="numeric" data-handover-fuel-input class="mk-input" style="max-width:8rem" />
                </dd></div>`;
            }

            const canConfirmNow = kmOk && knownFuelOk;
            openModal(`
                <h2>Xác nhận giao xe</h2>
                <dl class="mk-dl">
                    <div><dt>Xe</dt><dd>${data.vehicle || t.vehicle || "—"}</dd></div>
                    <div><dt>Biển số</dt><dd>${data.plate || t.plate || "—"}</dd></div>
                    <div><dt>KM hiện tại</dt><dd><strong>${kmText}</strong></dd></div>
                    ${fuelBlock}
                </dl>
                ${!kmOk ? `<p class="mk-note" style="color:#b91c1c">${data.blockReason || "Xe chưa có số KM hiện tại trong hệ thống."}</p>` : ""}
                ${kmOk && !knownFuelOk ? `<p class="mk-note">Xe chưa có dữ liệu nhiên liệu, vui lòng nhập mức nhiên liệu thực tế.</p>` : ""}
                ${kmOk && knownFuelOk ? `<p class="mk-note">Nhiên liệu điền sẵn từ lần kiểm xe gần nhất (${Number(data.fuelLevel).toLocaleString("vi-VN")}%) — sửa lại nếu thực tế khác.</p>` : ""}
                ${kmOk ? `<p class="mk-note">Số km lấy từ hồ sơ xe trên hệ thống — không thể chỉnh sửa tại đây.</p>` : ""}
                ${licenceCheckBlock(data.licence)}
                <div class="mk-actions">
                    <button type="button" class="mk-btn mk-btn-ghost" data-close-modal>Hủy</button>
                    <button type="button" class="mk-btn mk-btn-success" data-handover-submit ${canConfirmNow ? "" : "disabled"}>Xác nhận giao xe</button>
                </div>`);
            state.modal = "handover";
            if (kmOk) {
                const input = document.querySelector("[data-handover-fuel-input]");
                input?.addEventListener("input", syncTripsHandoverConfirm);
                input?.addEventListener("change", syncTripsHandoverConfirm);
            }
        } catch (_) {
            openModal(`
                <h2>Xác nhận giao xe</h2>
                <p class="mk-note" style="color:#b91c1c">Lỗi mạng khi tải dữ liệu xe.</p>
                <div class="mk-actions"><button type="button" class="mk-btn mk-btn-ghost" data-close-modal>Đóng</button></div>`);
        }
    }

    // With-driver trip (plan1.md Phần A): the dispatcher records the car before handover; the driver
    // confirms it in the app (that starts the trip) or reports a mismatch, which shows up here.
    const COND_OPTIONS = ["Tốt", "Vấn đề nhỏ", "Cần kiểm tra", "Vấn đề nghiêm trọng"];
    function condSelect(name, current) {
        const opts = COND_OPTIONS.map((o) => `<option${o === current ? " selected" : ""}>${o}</option>`).join("");
        const extra = current && !COND_OPTIONS.includes(current) ? `<option selected>${esc(current)}</option>` : "";
        return `<select name="${name}" required><option value="">— Chọn —</option>${extra}${opts}</select>`;
    }

    async function precheckForm(t) {
        if (!t) return;
        if (!t.canHandoverApi) {
            toast("Chỉ ghi kiểm tra trước giao khi đơn đã phân công tài xế và chuyến chưa bắt đầu.");
            return;
        }
        let info = null;
        try {
            const url = new URL(handlers.handoverInfo, location.origin);
            url.searchParams.set("id", String(t.id));
            const res = await fetch(url.toString(), { headers: { Accept: "application/json" }, credentials: "same-origin" });
            info = await res.json().catch(() => null);
        } catch { /* show the form anyway */ }
        const pre = t.pre;
        const km = pre ? pre.odo : (info && isValidKm(info.currentKm) ? Number(info.currentKm) : "");
        const fuel = pre ? pre.fuel : (info && isValidFuelPercent(info.fuelLevel) ? Number(info.fuelLevel) : "");
        const disputeBox = t.dispute ? `
            <div class="mk-block" style="border-left:3px solid #b91c1c;padding-left:.6rem">
                <strong>⚠️ Tài xế báo sai lệch</strong>
                <p style="margin:.3rem 0">${esc(t.dispute.note)}</p>
                <p class="mk-note">${esc(t.dispute.by)}</p>
                <form id="opDisputeForm" data-id="${t.id}" data-dispute-id="${t.dispute.id}">
                    <label class="mk-field"><span>Ghi chú xử lý (không bắt buộc)</span>
                        <input name="note" maxlength="500" placeholder="Ví dụ: đã ghi lại vết trầy cửa sau" /></label>
                    <p class="mk-note">Sửa biên bản bên dưới (nếu cần) rồi bấm "Đã giải quyết" để báo tài xế kiểm tra lại.</p>
                    <div class="mk-actions"><button type="submit" class="mk-btn mk-btn-success">Đã giải quyết</button></div>
                </form>
            </div>` : "";
        openModal(`
            <h2>${pre ? "Sửa biên bản kiểm tra trước giao" : "Kiểm tra xe trước giao"}</h2>
            <dl class="mk-dl">
                <div><dt>Đơn</dt><dd>#${t.id} · Có tài xế</dd></div>
                <div><dt>Xe</dt><dd>${esc((info && info.vehicle) || t.vehicle || "—")} · ${esc((info && info.plate) || t.plate || "—")}</dd></div>
                <div><dt>Tài xế</dt><dd>${esc(t.driver || "—")}</dd></div>
                ${pre ? `<div><dt>Người lập</dt><dd>${esc(pre.by)} · ${pre.createdAt || pre.at}</dd></div>` : ""}
                ${pre && pre.edited ? `<div><dt>Người sửa</dt><dd>${esc(pre.edited)}</dd></div>` : ""}
            </dl>
            ${disputeBox}
            <form id="opPrecheckForm" data-id="${t.id}" class="op-form-grid">
                <label class="mk-field"><span>Số KM *</span>
                    <input name="odo" type="text" inputmode="decimal" value="${km}" autocomplete="off" /></label>
                <label class="mk-field"><span>Mức nhiên liệu (%) *</span>
                    <input name="fuel" type="text" inputmode="decimal" value="${fuel}" autocomplete="off" /></label>
                <label class="mk-field"><span>Ngoại thất *</span>${condSelect("exterior", pre && pre.exterior !== "—" ? pre.exterior : "")}</label>
                <label class="mk-field"><span>Kỹ thuật *</span>${condSelect("tech", pre && pre.tech !== "—" ? pre.tech : "")}</label>
                <label class="mk-field" style="grid-column:1/-1"><span>Ghi chú</span>
                    <input name="note" maxlength="500" value="${esc(pre ? pre.note : "")}" /></label>
            </form>
            <p class="mk-note">Chuyến chưa bắt đầu khi lưu. Tài xế kiểm tra xe thật trên app: đúng thì bắt đầu chuyến, sai thì báo sai lệch về đây. Hệ thống lưu tên bạn là người lập / người sửa.</p>
            <p class="mk-note" id="opPrecheckError" style="color:#b91c1c"></p>
            <div class="mk-actions">
                <button type="button" class="mk-btn mk-btn-ghost" data-close-modal>Đóng</button>
                <button type="submit" form="opPrecheckForm" class="mk-btn mk-btn-success">${pre ? "Lưu thay đổi" : "Lưu biên bản trước giao"}</button>
            </div>`);
        state.modal = "precheck";
    }

    // ---- Đổi xe (plan1.md Phần I) ----
    function vehicleOption(v) {
        const diff = Number(v.priceDifference || 0);
        const diffText = diff > 0 ? ` · +${money(diff)}` : diff < 0 ? ` · ${money(diff)}` : "";
        return `<option value="${v.vehicleId}" data-diff="${diff}">${esc(v.brand)} ${esc(v.model)} · ${esc(v.licensePlate)} (${esc(v.typeName)}, ${v.currentKm} km)${diffText}</option>`;
    }

    async function vehicleChangeForm(t) {
        if (!t) return;
        const url = handlers.vehicleChangeOptions + (handlers.vehicleChangeOptions.includes("?") ? "&" : "?") + "id=" + t.id;
        const { ok, data } = await getJson(url);
        if (!ok) { toast((data && data.error) || "Không tải được danh sách xe thay thế."); return; }
        const o = data.options;
        const list = o.sameType.length ? o.sameType : o.otherTypes;
        const pickBox = list.length
            ? `<label class="mk-field" style="grid-column:1/-1"><span>Xe thay thế *</span>
                    <select name="vehicle" data-vc-vehicle><option value="">— Chọn xe —</option>${list.map(vehicleOption).join("")}</select></label>`
            : `<p class="mk-note" style="grid-column:1/-1;color:#b91c1c">Không còn xe nào trống lịch cho thời gian thuê còn lại.</p>`;
        const typeNote = o.sameType.length
            ? `<p class="mk-note">Xe cùng loại <strong>${esc(o.currentTypeName)}</strong> còn trống lịch ${o.remainingDays} ngày còn lại: không phát sinh chênh lệch giá.</p>`
            : (o.otherTypes.length ? `<p class="mk-note">⚠️ Hết xe cùng loại ${esc(o.currentTypeName)}. Chỉ chọn xe khác loại khi khách đồng ý; chênh lệch tính cho ${o.remainingDays} ngày còn lại.</p>` : "");
        const readings = o.duringTrip ? `
                <label class="mk-field"><span>Km xe cũ lúc đổi *</span><input name="oldOdo" inputmode="decimal" autocomplete="off" placeholder="${o.segmentStartKm != null ? "≥ " + o.segmentStartKm : ""}" /></label>
                <label class="mk-field"><span>Nhiên liệu xe cũ (%) *</span><input name="oldFuel" inputmode="decimal" autocomplete="off" /></label>
                <label class="mk-field"><span>Km xe mới lúc giao *</span><input name="newOdo" inputmode="decimal" autocomplete="off" /></label>
                <label class="mk-field"><span>Nhiên liệu xe mới (%) *</span><input name="newFuel" inputmode="decimal" autocomplete="off" /></label>` : "";
        const agree = o.sameType.length ? "" : `
                <label class="mk-field" style="grid-column:1/-1">
                    <span><input type="checkbox" name="agreed" /> Khách đã đồng ý chênh lệch giá (đã gọi điện xác nhận)</span></label>`;
        openModal(`
            <h2>Đổi xe · Đơn #${t.id}</h2>
            <dl class="mk-dl">
                <div><dt>Xe hiện tại</dt><dd>${esc(o.currentVehicle)} (${esc(o.currentTypeName)})</dd></div>
                <div><dt>Thời điểm</dt><dd>${o.duringTrip ? "Giữa chuyến (đang thuê)" : "Trước khi giao xe"}</dd></div>
            </dl>
            ${typeNote}
            <form id="opVehicleChangeForm" data-id="${t.id}" class="op-form-grid">
                ${pickBox}
                <label class="mk-field"><span>Lý do *</span>
                    <select name="reason"><option value="">— Chọn —</option><option value="Breakdown">Xe gặp sự cố kỹ thuật</option><option value="NeedChange">Khách thay đổi nhu cầu</option></select></label>
                <label class="mk-field"><span>Ghi chú</span><input name="note" maxlength="500" /></label>
                ${readings}
                ${agree}
            </form>
            <p class="mk-note">${o.duringTrip ? "Đổi giữa chuyến: km chuyến = km chặng xe cũ + km chặng xe mới." : (t.mode === "WithDriver" ? "Biên bản trước giao của xe cũ (nếu có) sẽ bị bỏ, cần ghi lại cho xe mới." : "")} Lý do "sự cố" chuyển xe cũ sang bảo dưỡng. Hệ thống lưu tên bạn là người duyệt và báo cho khách${t.mode === "WithDriver" ? " và tài xế" : ""}.</p>
            <p class="mk-note" id="opVehicleChangeError" style="color:#b91c1c"></p>
            <div class="mk-actions">
                <button type="button" class="mk-btn mk-btn-ghost" data-close-modal>Đóng</button>
                <button type="submit" form="opVehicleChangeForm" class="mk-btn mk-btn-success" ${list.length ? "" : "disabled"}>Xác nhận đổi xe</button>
            </div>`);
        state.modal = "vehicleChange";
    }

    async function returnForm(t) {
        if (t.mode !== "SelfDrive") {
            toast("WithDriver complete: thiếu API dispatcher — không ghi Backend. (Mock UI only.)");
            return;
        }
        openModal(`
            <h2>Xác nhận tiếp nhận trả xe</h2>
            <p class="mk-note">Đang tải dữ liệu xe...</p>
            <div class="mk-actions">
                <button type="button" class="mk-btn mk-btn-ghost" data-close-modal>Hủy</button>
                <button type="button" class="mk-btn mk-btn-success" data-return-submit disabled>Xác nhận trả xe</button>
            </div>`);
        state.modal = "return";
        state.returnPayload = null;
        try {
            const url = new URL(handlers.returnInfo, location.origin);
            url.searchParams.set("id", String(t.id));
            const res = await fetch(url.toString(), { headers: { Accept: "application/json" }, credentials: "same-origin" });
            const data = await res.json().catch(() => ({ ok: false, error: "Phản hồi không hợp lệ." }));
            if (!data.ok) {
                openModal(`
                    <h2>Xác nhận tiếp nhận trả xe</h2>
                    <p class="mk-note" style="color:#b91c1c">${data.error || "Không thể tải dữ liệu trả xe."}</p>
                    <div class="mk-actions"><button type="button" class="mk-btn mk-btn-ghost" data-close-modal>Đóng</button></div>`);
                return;
            }
            const handoverNote = data.handoverOdometerKm != null
                ? `<p class="mk-note">KM lúc giao: <strong>${Number(data.handoverOdometerKm).toLocaleString("vi-VN")} km</strong>. Số KM khi trả phải không nhỏ hơn mức này.</p>`
                : "";
            openModal(`
                <h2>Xác nhận tiếp nhận trả xe</h2>
                <dl class="mk-dl">
                    <div><dt>Xe</dt><dd>${data.vehicle || t.vehicle || "—"}</dd></div>
                    <div><dt>Biển số</dt><dd>${data.plate || t.plate || "—"}</dd></div>
                </dl>
                ${handoverNote}
                <form id="opReturnForm" data-id="${t.id}" class="op-form-grid">
                    <label class="mk-field"><span>Số KM khi trả xe *</span>
                        <input name="odo" type="text" inputmode="decimal" placeholder="Nhập số km" autocomplete="off" />
                    </label>
                    <label class="mk-field"><span>Mức nhiên liệu khi trả xe (%)</span>
                        <input name="fuel" type="text" inputmode="decimal" placeholder="Nhập %" autocomplete="off" />
                    </label>
                    <label class="mk-field"><span>Ngoại thất</span>
                        <select name="exterior"><option value="">—</option><option>Tốt</option><option>Cần kiểm tra</option></select>
                    </label>
                    <label class="mk-field"><span>Tình trạng kỹ thuật</span>
                        <select name="tech"><option value="">—</option><option>Tốt</option><option>Cần kiểm tra</option></select>
                    </label>
                    <label class="mk-field" style="grid-column:1/-1"><span>Ghi chú</span><input name="note" value="" /></label>
                </form>
                <p class="mk-note" id="opReturnError" style="color:#b91c1c"></p>
                <div class="mk-actions">
                    <button type="button" class="mk-btn mk-btn-ghost" data-close-modal>Hủy</button>
                    <button type="submit" form="opReturnForm" class="mk-btn mk-btn-success" data-return-submit>Xác nhận trả xe</button>
                </div>`);
            state.modal = "return";
            state.returnPayload = { id: t.id };
            const form = document.getElementById("opReturnForm");
            form?.addEventListener("input", () => {
                const box = document.getElementById("opReturnError");
                if (box) box.textContent = "";
            });
        } catch (_) {
            openModal(`
                <h2>Xác nhận tiếp nhận trả xe</h2>
                <p class="mk-note" style="color:#b91c1c">Lỗi mạng khi tải dữ liệu xe.</p>
                <div class="mk-actions"><button type="button" class="mk-btn mk-btn-ghost" data-close-modal>Đóng</button></div>`);
        }
    }

    const INCIDENT_TYPES = { Accident: "Tai nạn / va chạm", VehicleIssue: "Xe hỏng / sự cố kỹ thuật", CustomerIssue: "Vấn đề với khách", Other: "Khác" };

    /** nowVn() keeps Vietnam wall-clock time in the UTC fields. */
    function nowLocalInput() {
        const d = nowVn();
        const p = (n) => String(n).padStart(2, "0");
        return `${d.getUTCFullYear()}-${p(d.getUTCMonth() + 1)}-${p(d.getUTCDate())}T${p(d.getUTCHours())}:${p(d.getUTCMinutes())}`;
    }

    /** Dispatcher records an incident reported by phone (customer of a self-drive booking, or the trip's driver). */
    function reportIncidentForm(t) {
        if (!t) return;
        const who = t.mode === "SelfDrive" ? "khách tự lái" : "tài xế / khách";
        openModal(`
            <h2>Ghi sự cố · Đơn #${t.id}</h2>
            <dl class="mk-dl">
                <div><dt>Xe</dt><dd>${esc(t.vehicle)} · ${esc(t.plate)}</dd></div>
                <div><dt>Hình thức</dt><dd>${MODE_LABEL[t.mode] || t.mode}${t.driver ? " · Tài xế " + esc(t.driver) : ""}</dd></div>
            </dl>
            <form id="opReportIncidentForm" data-id="${t.id}" class="op-form-grid">
                <label class="mk-field"><span>Loại sự cố *</span>
                    <select name="incidentType"><option value="">— Chọn —</option>${Object.entries(INCIDENT_TYPES).map(([k, v]) => `<option value="${k}">${v}</option>`).join("")}</select></label>
                <label class="mk-field"><span>Thời điểm xảy ra</span><input type="datetime-local" name="occurredAt" value="${nowLocalInput()}" /></label>
                <label class="mk-field" style="grid-column:1/-1"><span>Mô tả *</span>
                    <textarea name="description" rows="3" maxlength="500" class="mk-input" placeholder="Ví dụ: khách báo xe nổ lốp trên cao tốc, đang chờ cứu hộ"></textarea></label>
            </form>
            <p class="mk-note">Dùng khi ${who} gọi điện báo. Hệ thống lưu tên bạn là người báo${t.mode === "WithDriver" ? " và gửi thông báo cho tài xế" : ""}. Xe hỏng cần thay thì dùng thêm nút "Đổi xe".</p>
            <p class="mk-note" id="opReportIncidentError" style="color:#b91c1c"></p>
            <div class="mk-actions">
                <button type="button" class="mk-btn mk-btn-ghost" data-close-modal>Đóng</button>
                <button type="submit" form="opReportIncidentForm" class="mk-btn mk-btn-danger">Ghi sự cố</button>
            </div>`);
        state.modal = "reportIncident";
    }

    function incidentForm(t) {
        const i = t.incident;
        if (!i) {
            reportIncidentForm(t);
            return;
        }
        openModal(`
            <h2>Báo cáo sự cố</h2>
            <dl class="mk-dl">
                <div><dt>Mã đơn</dt><dd>#${t.id}</dd></div>
                <div><dt>Xe</dt><dd>${t.vehicle}<br>${t.plate}</dd></div>
                <div><dt>Người báo</dt><dd>${i.reporter}</dd></div>
                <div><dt>Thời gian</dt><dd>${i.time}</dd></div>
                <div><dt>Loại</dt><dd>${INCIDENT_TYPES[i.type] || esc(i.type)}</dd></div>
                <div><dt>Trạng thái</dt><dd>${i.status}</dd></div>
                ${i.resolvedBy ? `<div><dt>Người xử lý</dt><dd>${esc(i.resolvedBy)}</dd></div>` : ""}
            </dl>
            <p>${esc(i.desc || "")}</p>
            ${i.resolutionNote ? `<p class="mk-note">Cách xử lý: ${esc(i.resolutionNote)}</p>` : ""}
            ${i.apiStatus === "Open" ? `
            <form id="opIncidentForm" data-id="${t.id}" data-incident-id="${i.id}" style="margin-top:.7rem">
                <label for="opIncNote" style="display:block;font-weight:600;margin-bottom:.3rem">Cách xử lý <span style="color:#b91c1c">*</span></label>
                <textarea id="opIncNote" name="note" rows="3" maxlength="500" class="mk-input" style="width:100%" required
                    placeholder="Ví dụ: đã gọi tài xế, xe đi tiếp; đã gửi xe thay thế..."></textarea>
                <p class="mk-note" id="opIncError" style="color:#b91c1c"></p>
                <p class="mk-note">Hệ thống lưu tên bạn và thời điểm xử lý.</p>
                <div class="mk-actions">
                    <button type="submit" class="mk-btn mk-btn-success">Đã xử lý</button>
                    <button type="button" class="mk-btn mk-btn-ghost" data-close-modal>Đóng</button>
                </div>
            </form>` : `
            <div class="mk-actions" style="margin-top:.7rem">
                <button type="button" class="mk-btn mk-btn-ghost" data-close-modal>Đóng</button>
            </div>`}`);
        state.modal = "incident";
    }

    function settlementForm(t) {
        const c = t.charges || { base: t.totalAmount || 0, deposit: t.depositAmount || 0, paid: 0, extraKm: 0, extraFuel: 0, damage: 0, other: 0 };
        const total = Number(c.base || 0) + Number(c.extraFuel || 0) + Number(c.damage || 0) + Number(c.other || 0);
        const paid = Number(c.paid || 0);
        const remain = total - paid;
        const settled = t.settlement === "settled";
        const remainText = (v) => v > 0 ? "Thu thêm " + money(v) : v < 0 ? "Hoàn lại " + money(-v) : "0 ₫";
        const form = settled ? `
            <p class="mk-note">✓ Đã quyết toán · ${esc(t.settledBy || "")}${t.settlementNote ? " · " + esc(t.settlementNote) : ""}</p>` : `
            <form id="opSettleForm" data-id="${t.id}" data-total="${total}" data-paid="${paid}" class="op-form-grid">
                <label class="mk-field"><span>Nhiên liệu bổ sung (₫)</span><input name="extraFuel" inputmode="numeric" value="0" data-settle-input /></label>
                <label class="mk-field"><span>Hư hỏng (₫)</span><input name="damage" inputmode="numeric" value="0" data-settle-input /></label>
                <label class="mk-field"><span>Phí khác (₫)</span><input name="other" inputmode="numeric" value="0" data-settle-input /></label>
                <label class="mk-field"><span>Phương thức thu / hoàn</span>
                    <select name="paymentMethod"><option value="Cash">Tiền mặt</option><option value="BankTransfer">Chuyển khoản</option><option value="MoMo">MoMo</option><option value="VNPay">VNPay</option></select></label>
                <label class="mk-field" style="grid-column:1/-1"><span>Ghi chú</span><input name="note" maxlength="500" placeholder="Ví dụ: trầy cản sau, khách đồng ý bồi thường" /></label>
            </form>
            <p class="mk-note">Sau khi chốt: tổng mới <strong data-settle-total>${money(total)}</strong> · <strong data-settle-remain>${remainText(remain)}</strong>. Hệ thống ghi khoản thu thêm / hoàn lại (mô phỏng), lưu tên bạn và báo cho khách.</p>
            <p class="mk-note" id="opSettleError" style="color:#b91c1c"></p>`;
        openModal(`
            <h2>Quyết toán chuyến thuê</h2>
            <p class="mk-note">Tổng đã gồm km vượt, trả muộn, nhiên liệu và chênh lệch đổi xe (chốt lúc trả xe). Nhập thêm phí phát sinh (nếu có) rồi chốt.</p>
            <ol class="mk-note" style="padding-left:1.1rem">
                <li>Đơn #${t.id} · ${MODE_LABEL[t.mode] || t.mode}</li>
                <li>Xe: ${t.vehicle} ${t.plate}</li>
                <li>Khách: ${t.customer}</li>
                <li>Tổng (API): ${t.totalAmountText || money(total)}</li>
                <li>Cọc theo báo giá: ${t.depositAmountText || money(c.deposit)}</li>
            </ol>
            <table class="op-settle">
                <tr><th>Giá thuê cơ bản</th><td>${money(c.base)}</td></tr>
                <tr><th>Phí nhiên liệu</th><td>${money(c.extraFuel)}</td></tr>
                <tr><th>Hư hỏng</th><td>${money(c.damage)}</td></tr>
                <tr><th>Khác</th><td>${money(c.other)}</td></tr>
                <tr class="total"><th>Tổng cộng</th><td>${money(total)}</td></tr>
                <tr><th>Khách đã thanh toán</th><td>${money(paid)}</td></tr>
                <tr><th>Còn lại</th><td>${settled ? "Đã tất toán" : remainText(remain)}</td></tr>
            </table>
            <p>${settleBadge(t.settlement)}</p>
            ${form}
            <div class="mk-actions">
                <button type="button" class="mk-btn mk-btn-ghost" data-close-modal>Đóng</button>
                ${settled ? "" : `<button type="submit" form="opSettleForm" class="mk-btn mk-btn-success">✓ Chốt quyết toán</button>`}
            </div>`);
        state.modal = "settle";
    }

    // Order = page order: metrics → queue → timeline → table.
    function renderBoardViews() {
        const now = nowVn();
        renderMetrics(now);
        renderQueue(now);
        renderTimeline(now);
        renderTable(now);
    }

    function render() {
        renderBoardViews();
        if (state.selectedId) {
            const t = trip(state.selectedId);
            if (t) renderDrawer(t);
        }
    }

    fillVehicles();

    els.form.addEventListener("submit", (e) => {
        e.preventDefault();
        const data = new FormData(els.form);
        state.q = (data.get("q") || "").toString().trim();
        state.status = (data.get("status") || "").toString();
        state.mode = (data.get("mode") || "").toString();
        state.dstatus = (data.get("dstatus") || "").toString();
        state.vehicle = (data.get("vehicle") || "").toString();
        state.page = 1;
        render();
    });
    document.getElementById("opReset").addEventListener("click", (e) => {
        e.preventDefault();
        els.form.reset();
        state.q = state.status = state.mode = state.dstatus = state.vehicle = state.metric = "";
        state.page = 1;
        render();
    });

    document.body.addEventListener("click", async (e) => {
        if (e.target.closest("[data-close]")) {
            state.selectedId = null;
            els.drawer.classList.remove("is-open");
            return;
        }
        if (e.target.closest("[data-close-modal]") || e.target === els.modal) {
            closeModal();
            return;
        }
        const metric = e.target.closest("[data-metric]");
        if (metric) {
            // Second click on the active card clears the metric filter.
            state.metric = state.metric === metric.dataset.metric ? "" : metric.dataset.metric;
            state.page = 1;
            renderBoardViews();
            return;
        }
        const scrollTable = e.target.closest("[data-scroll-table]");
        if (scrollTable) {
            e.preventDefault();
            document.getElementById("opTableCard")?.scrollIntoView({ behavior: "smooth", block: "start" });
            return;
        }
        const page = e.target.closest("[data-page]");
        if (page) { state.page = Number(page.dataset.page); render(); return; }

        const contact = e.target.closest("[data-contact]");
        if (contact) { toast("Liên hệ — không có API gọi/SMS. Presentation only."); return; }
        const inspect = e.target.closest("[data-inspect]");
        if (inspect) {
            const t = trip(inspect.dataset.inspect);
            if (t && (t.handover || t.ret)) {
                toast("Đã có biên bản từ API — xem trong drawer chi tiết.");
                state.selectedId = t.id;
                renderDrawer(t);
            } else toast("Chưa có biên bản inspection từ API cho chuyến này.");
            return;
        }
        const report = e.target.closest("[data-report]");
        if (report) { reportIncidentForm(trip(report.dataset.report)); return; }

        const vc = e.target.closest("[data-vehicle-change]");
        if (vc) { vehicleChangeForm(trip(vc.dataset.vehicleChange)); return; }
        const pc = e.target.closest("[data-precheck]");
        if (pc) { precheckForm(trip(pc.dataset.precheck)); return; }
        const ho = e.target.closest("[data-handover]");
        if (ho) {
            const ht = trip(ho.dataset.handover);
            if (ht && ht.mode !== "SelfDrive") precheckForm(ht); else handoverForm(ht);
            return;
        }
        const handoverSubmit = e.target.closest("[data-handover-submit]");
        if (handoverSubmit) {
            if (handoverSubmit.disabled || !state.handoverPayload) return;
            syncTripsHandoverConfirm();
            const payload = state.handoverPayload;
            if (payload.fuelLevel === null || payload.fuelLevel === undefined) {
                toast("Vui lòng nhập mức nhiên liệu từ 0 đến 100.");
                return;
            }
            const licenceBox = document.querySelector("[data-handover-licence-check]");
            if (licenceBox && !licenceBox.checked) {
                toast("Vui lòng kiểm tra bằng lái gốc của khách và tick ô xác nhận.");
                return;
            }
            const url = handlers.handover + (handlers.handover.includes("?") ? "&" : "?") + "id=" + payload.id;
            handoverSubmit.disabled = true;
            const { ok, data: res } = await postForm(url, {
                odometerKm: payload.odometerKm,
                fuelLevel: payload.fuelLevel,
                originalLicenseChecked: licenceBox ? licenceBox.checked : false
            });
            if (!ok) {
                toast((res && res.error) || "Giao xe thất bại.");
                handoverSubmit.disabled = false;
                return;
            }
            if (res.trip) upsertTrip(res.trip);
            closeModal();
            toast("Đã giao xe.");
            await reloadFromServer();
            render();
            const updated = trip(payload.id);
            if (updated) renderDrawer(updated);
            return;
        }
        const rt = e.target.closest("[data-return]");
        if (rt) { returnForm(trip(rt.dataset.return)); return; }
        const inc = e.target.closest("[data-incident]");
        if (inc) { incidentForm(trip(inc.dataset.incident)); return; }
        const se = e.target.closest("[data-settle]");
        if (se) { settlementForm(trip(se.dataset.settle)); return; }

        const open = e.target.closest("[data-open]");
        if (open && !e.target.closest("[data-handover],[data-return],[data-incident],[data-settle]")) {
            state.selectedId = Number(open.dataset.open);
            if (handlers.detail) {
                const url = handlers.detail + (handlers.detail.includes("?") ? "&" : "?") + "id=" + state.selectedId;
                const { ok, data } = await getJson(url);
                if (ok && data.trip) upsertTrip(data.trip);
            }
            const t = trip(state.selectedId);
            if (t) renderDrawer(t);
        }
    });

    document.body.addEventListener("input", (e) => {
        if (!e.target.matches || !e.target.matches("[data-settle-input]")) return;
        const form = e.target.form;
        let total = Number(form.dataset.total || 0);
        form.querySelectorAll("[data-settle-input]").forEach((i) => {
            const v = Number(String(i.value || "0").replace(/[.,\s]/g, ""));
            if (Number.isFinite(v) && v > 0) total += v;
        });
        const remain = total - Number(form.dataset.paid || 0);
        const t = document.querySelector("[data-settle-total]");
        const r = document.querySelector("[data-settle-remain]");
        if (t) t.textContent = money(total);
        if (r) r.textContent = remain > 0 ? "Thu thêm " + money(remain) : remain < 0 ? "Hoàn lại " + money(-remain) : "0 ₫";
    });

    document.body.addEventListener("submit", async (e) => {
        if (e.target.id === "opPrecheckForm") {
            e.preventDefault();
            const form = e.target;
            const data = new FormData(form);
            const box = document.getElementById("opPrecheckError");
            const odo = String(data.get("odo") || "").trim();
            const fuel = String(data.get("fuel") || "").trim();
            if (odo === "" || !Number.isFinite(Number(odo)) || Number(odo) < 0) { box.textContent = "Vui lòng nhập số km hợp lệ."; return; }
            if (fuel === "" || !Number.isFinite(Number(fuel)) || Number(fuel) < 0 || Number(fuel) > 100) { box.textContent = "Mức nhiên liệu phải từ 0 đến 100."; return; }
            if (!data.get("exterior") || !data.get("tech")) { box.textContent = "Vui lòng chọn tình trạng ngoại thất và kỹ thuật."; return; }
            const url = handlers.preHandover + (handlers.preHandover.includes("?") ? "&" : "?") + "id=" + form.dataset.id;
            const { ok, data: res } = await postForm(url, {
                odometerKm: odo, fuelLevel: fuel,
                exteriorCondition: data.get("exterior"), technicalCondition: data.get("tech"), notes: data.get("note")
            });
            if (!ok) { box.textContent = (res && res.error) || "Không lưu được biên bản trước giao."; return; }
            if (res.trip) upsertTrip(res.trip);
            toast("Đã lưu biên bản trước giao. Chờ tài xế xác nhận.");
            closeModal();
            await reloadFromServer();
            render();
            return;
        }
        if (e.target.id === "opReportIncidentForm") {
            e.preventDefault();
            const form = e.target;
            const data = new FormData(form);
            const box = document.getElementById("opReportIncidentError");
            if (!data.get("incidentType")) { box.textContent = "Vui lòng chọn loại sự cố."; return; }
            const desc = String(data.get("description") || "").trim();
            if (!desc) { box.textContent = "Vui lòng mô tả sự cố."; return; }
            const url = handlers.reportIncident + (handlers.reportIncident.includes("?") ? "&" : "?") + "id=" + form.dataset.id;
            const { ok, data: res } = await postForm(url, { incidentType: data.get("incidentType"), description: desc, occurredAt: data.get("occurredAt") });
            if (!ok) { box.textContent = (res && res.error) || "Không ghi được sự cố."; return; }
            if (res.trip) upsertTrip(res.trip);
            toast("Đã ghi sự cố.");
            closeModal();
            await reloadFromServer();
            render();
            const updated = trip(Number(form.dataset.id));
            if (updated && state.selectedId === updated.id) renderDrawer(updated);
            return;
        }
        if (e.target.id === "opSettleForm") {
            e.preventDefault();
            const form = e.target;
            const data = new FormData(form);
            const box = document.getElementById("opSettleError");
            const fields = { note: data.get("note"), paymentMethod: data.get("paymentMethod") };
            for (const k of ["extraFuel", "damage", "other"]) {
                const v = String(data.get(k) || "0").replace(/[.,\s]/g, "");
                if (!/^\d+$/.test(v)) { box.textContent = "Phí nhập tay phải là số tiền không âm."; return; }
                fields[k] = v;
            }
            const btn = document.querySelector('button[form="opSettleForm"]');
            if (btn) btn.disabled = true;
            const url = handlers.settle + (handlers.settle.includes("?") ? "&" : "?") + "id=" + form.dataset.id;
            const { ok, data: res } = await postForm(url, fields);
            if (!ok) {
                box.textContent = (res && res.error) || "Không chốt được quyết toán.";
                if (btn) btn.disabled = false;
                return;
            }
            if (res.trip) upsertTrip(res.trip);
            toast("Đã quyết toán. Khách hàng đã được thông báo.");
            closeModal();
            await reloadFromServer();
            render();
            const updated = trip(Number(form.dataset.id));
            if (updated && state.selectedId === updated.id) renderDrawer(updated);
            return;
        }
        if (e.target.id === "opVehicleChangeForm") {
            e.preventDefault();
            const form = e.target;
            const data = new FormData(form);
            const box = document.getElementById("opVehicleChangeError");
            const sel = form.querySelector("[data-vc-vehicle]");
            if (!sel || !sel.value) { box.textContent = "Vui lòng chọn xe thay thế."; return; }
            if (!data.get("reason")) { box.textContent = "Vui lòng chọn lý do đổi xe."; return; }
            const diff = Number(sel.selectedOptions[0].dataset.diff || 0);
            if (diff !== 0 && !data.get("agreed")) { box.textContent = "Cần xác nhận khách đã đồng ý chênh lệch giá."; return; }
            const fields = { newVehicleId: sel.value, reason: data.get("reason"), note: data.get("note"), customerAgreed: data.get("agreed") ? "true" : "false" };
            if (form.querySelector("[name=oldOdo]")) {
                const nums = { oldOdometerKm: "oldOdo", oldFuelLevel: "oldFuel", newOdometerKm: "newOdo", newFuelLevel: "newFuel" };
                for (const [k, n] of Object.entries(nums)) {
                    const v = String(data.get(n) || "").trim();
                    if (v === "" || !Number.isFinite(Number(v)) || Number(v) < 0) { box.textContent = "Đổi giữa chuyến: nhập số km và mức nhiên liệu của cả xe cũ và xe mới."; return; }
                    if (k.endsWith("FuelLevel") && Number(v) > 100) { box.textContent = "Mức nhiên liệu phải từ 0 đến 100."; return; }
                    fields[k] = v;
                }
            }
            const url = handlers.changeVehicle + (handlers.changeVehicle.includes("?") ? "&" : "?") + "id=" + form.dataset.id;
            const { ok, data: res } = await postForm(url, fields);
            if (!ok) { box.textContent = (res && res.error) || "Không đổi được xe."; return; }
            if (res.trip) upsertTrip(res.trip);
            toast("Đã đổi xe. Khách hàng đã được thông báo.");
            closeModal();
            await reloadFromServer();
            render();
            const updated = trip(Number(form.dataset.id));
            if (updated) renderDrawer(updated);
            return;
        }
        if (e.target.id === "opDisputeForm") {
            e.preventDefault();
            const form = e.target;
            const url = handlers.resolveDispute + (handlers.resolveDispute.includes("?") ? "&" : "?") + "id=" + form.dataset.id;
            const { ok, data: res } = await postForm(url, {
                disputeId: form.dataset.disputeId, note: new FormData(form).get("note")
            });
            if (!ok) { toast((res && res.error) || "Không cập nhật được báo sai lệch."); return; }
            if (res.trip) upsertTrip(res.trip);
            toast("Đã báo tài xế kiểm tra lại biên bản.");
            closeModal();
            await reloadFromServer();
            render();
            return;
        }
        if (e.target.id === "opIncidentForm") {
            e.preventDefault();
            const form = e.target;
            const note = String(new FormData(form).get("note") || "").trim();
            const box = document.getElementById("opIncError");
            if (!note) {
                if (box) box.textContent = "Vui lòng ghi cách xử lý sự cố.";
                return;
            }
            const url = handlers.resolveIncident + (handlers.resolveIncident.includes("?") ? "&" : "?") + "id=" + form.dataset.id;
            const { ok, data: res } = await postForm(url, { incidentId: form.dataset.incidentId, note });
            if (!ok) {
                if (box) box.textContent = (res && res.error) || "Không cập nhật được sự cố.";
                return;
            }
            if (res.trip) upsertTrip(res.trip);
            toast("Đã ghi nhận xử lý sự cố.");
            closeModal();
            await reloadFromServer();
            render();
            return;
        }
        if (e.target.id === "opHandoverForm") {
            e.preventDefault();
            return;
        }
        if (e.target.id === "opReturnForm") {
            e.preventDefault();
            const t = trip(e.target.dataset.id);
            if (!t) return;
            const data = new FormData(e.target);
            const odoRaw = String(data.get("odo") || "").trim();
            const fuelRaw = String(data.get("fuel") || "").trim();
            const box = document.getElementById("opReturnError");
            if (odoRaw === "") {
                if (box) box.textContent = "Vui lòng nhập số km khi trả xe.";
                return;
            }
            if (!Number.isFinite(Number(odoRaw))) {
                if (box) box.textContent = "Số km khi trả xe không hợp lệ.";
                return;
            }
            if (Number(odoRaw) < 0) {
                if (box) box.textContent = "Số km khi trả xe không được âm.";
                return;
            }
            if (fuelRaw === "") {
                if (box) box.textContent = "Vui lòng nhập mức nhiên liệu khi trả xe từ 0 đến 100.";
                return;
            }
            if (!Number.isFinite(Number(fuelRaw)) || Number(fuelRaw) < 0 || Number(fuelRaw) > 100) {
                if (box) box.textContent = "Mức nhiên liệu phải từ 0 đến 100.";
                return;
            }
            if (t.mode !== "SelfDrive") {
                toast("WithDriver complete: thiếu API dispatcher — không ghi Backend. (Mock UI only.)");
                closeModal();
                return;
            }
            const url = handlers.complete + (handlers.complete.includes("?") ? "&" : "?") + "id=" + t.id;
            const { ok, data: res } = await postForm(url, {
                odometerKm: data.get("odo"),
                fuelLevel: data.get("fuel"),
                exteriorCondition: data.get("exterior"),
                technicalCondition: data.get("tech"),
                notes: data.get("note")
            });
            if (!ok) {
                const box = document.getElementById("opReturnError");
                if (box) box.textContent = (res && res.error) || "Trả xe / hoàn thành thất bại.";
                else toast((res && res.error) || "Trả xe / hoàn thành thất bại.");
                return;
            }
            if (res.trip) upsertTrip(res.trip);
            toast("Đã hoàn thành trả xe qua API (SelfDrive).");
            closeModal();
            await reloadFromServer();
            render();
        }
    });

    render();
    // Keep "còn / quá X phút" moving without touching an open drawer or modal.
    setInterval(renderBoardViews, 60 * 1000);

    // Realtime (realtime.js): a booking changed somewhere -> refetch and redraw the board views only.
    // The drawer / an open handover or return form are left untouched.
    let realtimeTimer = null;
    function onRealtime(e) {
        if (e.type === "carrental:realtime" && (!e.detail || e.detail.bookingId == null)) return;
        clearTimeout(realtimeTimer);
        realtimeTimer = setTimeout(async () => {
            await reloadFromServer();
            renderBoardViews();
        }, 800);
    }
    window.addEventListener("carrental:realtime", onRealtime);
    window.addEventListener("carrental:realtime-reconnected", onRealtime);
})();
