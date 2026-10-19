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
                    : btn("mk-btn-outline", "data-contact", "Liên hệ tài xế")
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
        </div>${km !== null ? `<p class="mk-note">Km thực tế: ${km.toLocaleString("vi-VN")} km = trả − giao (từ biên bản API).</p>` : ""}`;
    }

    function quickActions(t) {
        const a = [`<button type="button" class="mk-btn mk-btn-ghost" data-contact="${t.id}">📞 Liên hệ</button>`];
        if (t.status === "inProgress" || t.status === "accepted" || t.status === "handedOver") {
            a.push(`<button type="button" class="mk-btn mk-btn-danger" data-report="${t.id}">⚠️ Báo sự cố</button>`);
        }
        a.push(`<button type="button" class="mk-btn mk-btn-ghost" data-inspect="${t.id}">📋 Inspection</button>`);
        if (t.canHandoverApi || t.status === "waitingHandover") {
            a.push(`<button type="button" class="mk-btn mk-btn-primary" data-handover="${t.id}">🚗 Giao xe</button>`);
        }
        if (t.canCompleteApi || t.status === "handedOver" || t.status === "inProgress" || t.status === "returning" || t.status === "waitingHandover" || t.status === "accepted") {
            a.push(`<button type="button" class="mk-btn mk-btn-primary" data-return="${t.id}">↩ Trả xe</button>`);
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
                    ${t.mode === "WithDriver" ? '<p class="mk-note">Có tài xế — hoàn thành trả xe qua API dispatcher chưa có (chỉ SelfDrive).</p>' : ""}
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
        if (!state.handoverPayload.requiresFuelInput) {
            btn.disabled = !(isValidKm(state.handoverPayload.odometerKm) && isValidFuelPercent(state.handoverPayload.fuelLevel));
            return;
        }
        const input = document.querySelector("[data-handover-fuel-input]");
        const fuel = parseHandoverFuel(input?.value);
        state.handoverPayload.fuelLevel = fuel;
        btn.disabled = fuel === null || !isValidKm(state.handoverPayload.odometerKm);
    }

    async function handoverForm(t) {
        if (!t.canHandoverApi && t.mode !== "SelfDrive") {
            toast("Chỉ SelfDrive dùng API handover dispatcher.");
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
            } else if (requiresFuelInput) {
                fuelBlock = `<div><dt>Mức nhiên liệu khi giao xe (%)</dt><dd>
                    <input type="number" min="0" max="100" step="1" placeholder="Nhập %"
                           inputmode="numeric" data-handover-fuel-input class="mk-input" style="max-width:8rem" />
                </dd></div>`;
            } else {
                fuelBlock = `<div><dt>Mức nhiên liệu</dt><dd><strong>${Number(data.fuelLevel).toLocaleString("vi-VN")}% 🔒</strong></dd></div>`;
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
                ${kmOk && requiresFuelInput ? `<p class="mk-note">Xe chưa có dữ liệu nhiên liệu, vui lòng nhập mức nhiên liệu thực tế.</p>` : ""}
                ${kmOk && !requiresFuelInput ? `<p class="mk-note">KM và mức nhiên liệu lấy từ hệ thống — không thể chỉnh sửa tại đây.</p>` : ""}
                ${kmOk && requiresFuelInput ? `<p class="mk-note">Số km lấy từ hồ sơ xe trên hệ thống — không thể chỉnh sửa tại đây.</p>` : ""}
                <div class="mk-actions">
                    <button type="button" class="mk-btn mk-btn-ghost" data-close-modal>Hủy</button>
                    <button type="button" class="mk-btn mk-btn-success" data-handover-submit ${canConfirmNow ? "" : "disabled"}>Xác nhận giao xe</button>
                </div>`);
            state.modal = "handover";
            if (kmOk && requiresFuelInput) {
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

    function incidentForm(t) {
        const i = t.incident;
        if (!i) {
            toast("Chuyến này chưa có sự cố từ API. Dispatcher không có API tạo sự cố.");
            return;
        }
        openModal(`
            <h2>Báo cáo sự cố</h2>
            <p class="mk-note">Dữ liệu từ GET /api/dispatch/incidents. Không có API cập nhật trạng thái sự cố cho dispatcher.</p>
            <dl class="mk-dl">
                <div><dt>Mã đơn</dt><dd>#${t.id}</dd></div>
                <div><dt>Xe</dt><dd>${t.vehicle}<br>${t.plate}</dd></div>
                <div><dt>Người báo</dt><dd>${i.reporter}</dd></div>
                <div><dt>Thời gian</dt><dd>${i.time}</dd></div>
                <div><dt>Loại</dt><dd>${i.type}</dd></div>
                <div><dt>Trạng thái API</dt><dd id="opIncStatus">${i.status}</dd></div>
            </dl>
            <p>${i.desc || ""}</p>
            <div class="op-photo">[ Ảnh hiện trường — không có API media ]</div>
            <p class="mk-note" style="margin-top:.7rem">Nút trạng thái dưới đây chỉ đổi UI cục bộ — chưa gọi API.</p>
            <div class="mk-actions" style="margin-top:.7rem">
                <button type="button" class="mk-btn mk-btn-primary" data-inc-status="Tiếp nhận" data-for="${t.id}">Tiếp nhận</button>
                <button type="button" class="mk-btn mk-btn-ghost" data-inc-status="Đang xử lý" data-for="${t.id}">Đang xử lý</button>
                <button type="button" class="mk-btn mk-btn-success" data-inc-status="Đã xử lý" data-for="${t.id}">Đã xử lý</button>
                <button type="button" class="mk-btn mk-btn-ghost" data-close-modal>Đóng</button>
            </div>`);
        state.modal = "incident";
    }

    function settlementForm(t) {
        const c = t.charges || { base: t.totalAmount || 0, deposit: t.depositAmount || 0, extraKm: 0, extraFuel: 0, damage: 0, other: 0 };
        const total = Number(c.base || 0) + Number(c.extraFuel || 0) + Number(c.damage || 0) + Number(c.other || 0);
        const remain = total - Number(c.deposit || 0);
        openModal(`
            <h2>Quyết toán chuyến thuê</h2>
            <p class="mk-note">Số tiền lấy từ Booking/Fees API khi có. <strong>Chốt quyết toán</strong> không có endpoint — chỉ presentation.</p>
            <ol class="mk-note" style="padding-left:1.1rem">
                <li>Đơn #${t.id} · ${MODE_LABEL[t.mode] || t.mode}</li>
                <li>Xe: ${t.vehicle} ${t.plate}</li>
                <li>Khách: ${t.customer}</li>
                <li>Tổng (API): ${t.totalAmountText || money(total)}</li>
                <li>Cọc: ${t.depositAmountText || money(c.deposit)}</li>
            </ol>
            <table class="op-settle">
                <tr><th>Giá thuê cơ bản</th><td>${money(c.base)}</td></tr>
                <tr><th>Tiền cọc</th><td>${money(c.deposit)}</td></tr>
                <tr><th>Phí nhiên liệu</th><td>${money(c.extraFuel)}</td></tr>
                <tr><th>Hư hỏng</th><td>${money(c.damage)}</td></tr>
                <tr><th>Khác</th><td>${money(c.other)}</td></tr>
                <tr class="total"><th>Tổng cộng</th><td>${money(total)}</td></tr>
                <tr><th>Còn lại</th><td>${money(remain)}</td></tr>
            </table>
            <p>${settleBadge(t.settlement)}</p>
            <div class="mk-actions">
                <button type="button" class="mk-btn mk-btn-ghost" data-close-modal>Đóng</button>
                <button type="button" class="mk-btn mk-btn-success" data-close-settle="${t.id}">✓ Chốt quyết toán (mock)</button>
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
        if (report) {
            toast("Dispatcher không có API tạo sự cố (chỉ tài xế POST incidents). Không gắn cờ giả như dữ liệu thật.");
            return;
        }
        const incSt = e.target.closest("[data-inc-status]");
        if (incSt) {
            toast(`UI only: «${incSt.dataset.incStatus}» — thiếu API cập nhật trạng thái sự cố dispatcher.`);
            const box = document.getElementById("opIncStatus");
            if (box) box.textContent = incSt.dataset.incStatus + " (local)";
            return;
        }
        const settleDone = e.target.closest("[data-close-settle]");
        if (settleDone) {
            toast("Chốt quyết toán — thiếu API. Không đổi trạng thái Backend.");
            closeModal();
            return;
        }
        const ho = e.target.closest("[data-handover]");
        if (ho) { handoverForm(trip(ho.dataset.handover)); return; }
        const handoverSubmit = e.target.closest("[data-handover-submit]");
        if (handoverSubmit) {
            if (handoverSubmit.disabled || !state.handoverPayload) return;
            syncTripsHandoverConfirm();
            const payload = state.handoverPayload;
            if (payload.fuelLevel === null || payload.fuelLevel === undefined) {
                toast("Vui lòng nhập mức nhiên liệu từ 0 đến 100.");
                return;
            }
            const url = handlers.handover + (handlers.handover.includes("?") ? "&" : "?") + "id=" + payload.id;
            handoverSubmit.disabled = true;
            const { ok, data: res } = await postForm(url, {
                odometerKm: payload.odometerKm,
                fuelLevel: payload.fuelLevel
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

    document.body.addEventListener("submit", async (e) => {
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
})();
