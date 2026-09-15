(function () {
    const boot = window.__INSPECTIONS_BOOTSTRAP__;
    const handlers = window.__INSPECTIONS_HANDLERS__ || {};
    const ready = window.__INSPECTIONS_READY__ === true;

    if (!boot || typeof boot !== "object" || !Array.isArray(boot.records) || !ready) {
        const body = document.getElementById("inspTableBody");
        if (body) {
            body.innerHTML = `<tr><td colspan="11">Inspections chưa nhận bootstrap API. Rebuild/restart PortalWeb.</td></tr>`;
        }
        console.error("[Inspections] Missing __INSPECTIONS_BOOTSTRAP__ — aborting.");
        return;
    }

    const PAGE_SIZE = 10;
    const TYPE = { checkout: "Giao xe", checkin: "Nhận xe" };
    const COND = {
        good: { text: "Tốt", cls: "mk-badge-ok" },
        minor: { text: "Vấn đề nhỏ", cls: "mk-badge-warn" },
        inspect: { text: "Cần kiểm tra", cls: "insp-badge-inspect" },
        major: { text: "Vấn đề nghiêm trọng", cls: "mk-badge-incident" }
    };

    let records = boot.records.slice();
    const filters = { q: "", type: "", vehicle: "", condition: "", from: "", to: "" };
    let page = 1;

    const els = {
        kpis: document.getElementById("inspKpis"),
        form: document.getElementById("inspFilter"),
        pills: document.getElementById("inspTypePills"),
        vehicle: document.getElementById("inspVehicleFilter"),
        table: document.getElementById("inspTableBody"),
        cards: document.getElementById("inspCards"),
        empty: document.getElementById("inspEmpty"),
        meta: document.getElementById("inspMeta"),
        pager: document.getElementById("inspPager"),
        drawer: document.getElementById("inspDrawer"),
        modal: document.getElementById("inspModal"),
        modalBody: document.getElementById("inspModalBody"),
        lightbox: document.getElementById("inspLightbox"),
        lightboxBody: document.getElementById("inspLightboxBody"),
        print: document.getElementById("inspPrint"),
        toast: document.getElementById("inspToast"),
        reset: document.getElementById("inspReset")
    };

    function kmText(n) { return Number(n || 0).toLocaleString("vi-VN") + " km"; }

    function toast(msg) {
        els.toast.textContent = msg;
        els.toast.classList.add("is-on");
        clearTimeout(toast._t);
        toast._t = setTimeout(() => els.toast.classList.remove("is-on"), 2600);
    }

    async function getJson(url) {
        const res = await fetch(url, { credentials: "same-origin" });
        let data = null;
        try { data = await res.json(); } catch { /* ignore */ }
        return { ok: res.ok && data && data.ok !== false, data };
    }

    function typeBadge(t) {
        return `<span class="mk-badge ${t === "checkout" ? "mk-badge-ok" : "mk-badge-confirmed"}">${TYPE[t] || t}</span>`;
    }
    function condBadge(c) {
        const x = COND[c] || COND.good;
        return `<span class="mk-badge ${x.cls}">${x.text}</span>`;
    }

    function visible() {
        return records.filter((r) => {
            if (filters.type && r.type !== filters.type) return false;
            if (filters.vehicle && r.vehicle !== filters.vehicle) return false;
            if (filters.condition === "minorGroup") {
                if (r.condition !== "minor" && r.condition !== "inspect") return false;
            } else if (filters.condition && r.condition !== filters.condition) return false;
            if (filters.from && r.dateIso < filters.from) return false;
            if (filters.to && r.dateIso > filters.to) return false;
            if (filters.q) {
                const q = filters.q.toLowerCase();
                const blob = `${r.booking} ${r.bookingId} ${r.plate} ${r.vehicle} ${r.customer} ${r.inspector}`.toLowerCase();
                if (!blob.includes(q)) return false;
            }
            return true;
        });
    }

    function kpis() {
        const m = boot.metrics || window.__INSPECTIONS_SSR_METRICS__ || {};
        const n = Number(m.total ?? records.length);
        const good = Number(m.good ?? records.filter((r) => r.condition === "good").length);
        const minor = Number(m.minor ?? records.filter((r) => r.condition === "minor" || r.condition === "inspect").length);
        const major = Number(m.major ?? records.filter((r) => r.condition === "major").length);
        const pct = (x) => (n ? Math.round(x * 100 / n) + "% tổng số" : "0%");
        const items = [
            { key: "", icon: "📋", label: "Tổng biên bản", value: n, hint: "Từ Backend" },
            { key: "good", icon: "🟢", label: "Tình trạng tốt", value: good, hint: pct(good) },
            { key: "minorGroup", icon: "🟡", label: "Có vấn đề nhỏ", value: minor, hint: pct(minor) },
            { key: "major", icon: "🔴", label: "Có vấn đề nghiêm trọng", value: major, hint: pct(major) }
        ];
        els.kpis.innerHTML = items.map((k) => `
            <button type="button" class="mk-metric insp-kpi ${filters.condition === k.key ? "is-on" : ""}" data-kpi="${k.key}">
                <span>${k.icon} ${k.label}</span>
                <strong>${k.value}</strong>
                <em>${k.hint}</em>
            </button>`).join("");
    }

    function pills() {
        const items = [
            { v: "", t: "Tất cả" },
            { v: "checkout", t: "Giao xe" },
            { v: "checkin", t: "Nhận xe" }
        ];
        els.pills.innerHTML = items.map((p) =>
            `<button type="button" class="mk-pill ${filters.type === p.v ? "is-on" : ""}" data-type="${p.v}">${p.t}</button>`
        ).join("");
    }

    function fillVehicles() {
        const names = [...new Set(records.map((r) => r.vehicle).filter(Boolean))].sort((a, b) => a.localeCompare(b, "vi"));
        const cur = filters.vehicle;
        els.vehicle.innerHTML = `<option value="">Tất cả xe</option>` + names.map((n) => `<option value="${n}">${n}</option>`).join("");
        els.vehicle.value = names.includes(cur) ? cur : "";
    }

    function rowHtml(r, stt) {
        const typeLabel = r.type === "checkout" ? "Kiểm tra trước khi bàn giao xe (Handover)" : "Kiểm tra sau khi khách trả xe (Return)";
        return `<tr>
            <td>${stt}</td>
            <td><strong>${r.booking}</strong></td>
            <td>${r.plate}</td>
            <td>${r.vehicle}</td>
            <td>${typeBadge(r.type)}<div class="mk-muted">${typeLabel}</div></td>
            <td>${kmText(r.km)}</td>
            <td>${r.fuel}%</td>
            <td>${condBadge(r.condition)}</td>
            <td>${r.datetime}</td>
            <td>${r.inspector}<div class="mk-muted">không có trong API</div></td>
            <td><button type="button" class="mk-btn mk-btn-ghost" data-open="${r.id}">Xem</button></td>
        </tr>`;
    }

    function cardHtml(r) {
        return `<article class="insp-card">
            <div class="insp-card-top">
                <div><strong>${r.booking}</strong><div class="mk-muted">${r.vehicle} · ${r.plate}</div></div>
                ${typeBadge(r.type)}
            </div>
            <p class="mk-muted" style="margin:0.45rem 0">${r.datetime}</p>
            <div class="mk-actions">
                ${condBadge(r.condition)}
                <button type="button" class="mk-btn mk-btn-ghost" data-open="${r.id}">Xem</button>
            </div>
        </article>`;
    }

    function render() {
        kpis();
        pills();
        const all = visible();
        const pages = Math.max(1, Math.ceil(all.length / PAGE_SIZE));
        if (page > pages) page = pages;
        const start = (page - 1) * PAGE_SIZE;
        const slice = all.slice(start, start + PAGE_SIZE);
        els.table.innerHTML = slice.map((r, i) => rowHtml(r, start + i + 1)).join("");
        els.cards.innerHTML = slice.map(cardHtml).join("");
        els.empty.hidden = slice.length > 0;
        const from = all.length ? start + 1 : 0;
        const to = start + slice.length;
        els.meta.textContent = `Hiển thị ${from}–${to} trong ${all.length} biên bản (Backend)`;
        const windowStart = Math.max(1, Math.min(page - 2, Math.max(1, pages - 4)));
        const nums = [];
        for (let i = windowStart; i <= Math.min(pages, windowStart + 4); i++) nums.push(i);
        els.pager.innerHTML = `
            <button type="button" class="mk-btn mk-btn-ghost" data-page="${Math.max(1, page - 1)}" ${page === 1 ? "disabled" : ""}>←</button>
            ${nums.map((n) => `<button type="button" class="mk-btn mk-btn-ghost ${n === page ? "is-on" : ""}" data-page="${n}">${n}</button>`).join("")}
            <button type="button" class="mk-btn mk-btn-ghost" data-page="${Math.min(pages, page + 1)}" ${page === pages ? "disabled" : ""}>→</button>`;
    }

    function compare(r) {
        if (r.type !== "checkin" || !r.checkoutRef) return "";
        const actual = r.km - (r.kmOut ?? r.checkoutRef.km);
        const changed = r.exterior !== r.checkoutRef.exterior || r.technical !== r.checkoutRef.technical;
        return `<div class="mk-block">
            <h3>Đối chiếu giao xe / nhận xe</h3>
            <div class="insp-compare">
                <article>
                    <h4>Lúc giao xe (Handover)</h4>
                    <dl class="mk-dl">
                        <div><dt>Số km</dt><dd>${kmText(r.kmOut ?? r.checkoutRef.km)}</dd></div>
                        <div><dt>Nhiên liệu</dt><dd>${r.fuelOut ?? r.checkoutRef.fuel}%</dd></div>
                        <div><dt>Ngoại thất</dt><dd>${r.checkoutRef.exterior}</dd></div>
                        <div><dt>Kỹ thuật</dt><dd>${r.checkoutRef.technical}</dd></div>
                    </dl>
                </article>
                <article>
                    <h4>Lúc nhận xe (Return)</h4>
                    <dl class="mk-dl">
                        <div><dt>Số km</dt><dd>${kmText(r.km)}</dd></div>
                        <div><dt>Nhiên liệu</dt><dd>${r.fuel}%</dd></div>
                        <div><dt>Ngoại thất</dt><dd>${r.exterior}</dd></div>
                        <div><dt>Kỹ thuật</dt><dd>${r.technical}</dd></div>
                    </dl>
                </article>
            </div>
            <p style="margin:0.7rem 0 0"><strong>Quãng đường thực tế:</strong> ${kmText(actual)}</p>
            ${changed ? `<p class="mk-note" style="margin-top:0.35rem">⚠️ Có thay đổi ngoại thất / kỹ thuật</p>` : `<p class="mk-note" style="margin-top:0.35rem">Không có thay đổi ngoại thất / kỹ thuật.</p>`}
        </div>`;
    }

    function odo(r) {
        if (r.type === "checkin" && (r.kmOut != null || r.checkoutRef)) {
            const out = r.kmOut ?? r.checkoutRef.km;
            const actual = r.km - out;
            return `<div class="mk-block">
                <h3>Số km &amp; nhiên liệu</h3>
                <dl class="mk-dl">
                    <div><dt>Số km giao</dt><dd>${kmText(out)}</dd></div>
                    <div><dt>Số km nhận</dt><dd>${kmText(r.km)}</dd></div>
                    <div><dt>Quãng đường thực tế</dt><dd>${kmText(actual)}</dd></div>
                    <div><dt>Nhiên liệu</dt><dd>${r.fuel}%<div class="insp-fuel"><i style="width:${Math.min(100, r.fuel)}%"></i></div></dd></div>
                </dl>
            </div>`;
        }
        return `<div class="mk-block">
            <h3>Số km &amp; nhiên liệu</h3>
            <dl class="mk-dl">
                <div><dt>Số km</dt><dd>${kmText(r.km)}</dd></div>
                <div><dt>Nhiên liệu</dt><dd>${r.fuel}%<div class="insp-fuel"><i style="width:${Math.min(100, r.fuel)}%"></i></div></dd></div>
            </dl>
        </div>`;
    }

    function issueBox(r) {
        if (!r.issue) return "";
        return `<div class="mk-block insp-issue">
            <h4>⚠️ Vấn đề được ghi nhận</h4>
            <p style="margin:0 0 0.35rem"><strong>${r.issue.title}</strong></p>
            <p class="mk-muted" style="margin:0">Mức độ: ${r.issue.level}<br>${r.issue.note || ""}</p>
        </div>`;
    }

    function openDrawer(r) {
        els.drawer.classList.add("is-open");
        els.drawer.setAttribute("aria-hidden", "false");
        els.drawer.innerHTML = `
            <div class="mk-drawer-head">
                <div>
                    <h2 class="h5 mb-0">Chi tiết biên bản</h2>
                    <p class="mk-muted mb-0">${r.booking} · ${r.datetime} · <span class="mk-badge mk-badge-ok">API</span></p>
                </div>
                <button type="button" class="mk-btn mk-btn-ghost" data-close>Đóng</button>
            </div>
            <div class="mk-drawer-body">
                <dl class="mk-dl mk-block">
                    <div><dt>Mã đơn</dt><dd>${r.booking}</dd></div>
                    <div><dt>Ngày</dt><dd>${r.datetime}</dd></div>
                    <div><dt>Loại</dt><dd>${TYPE[r.type]} (${r.inspectionType || ""})</dd></div>
                    <div><dt>Tình trạng</dt><dd>${condBadge(r.condition)}</dd></div>
                </dl>
                <div class="mk-block">
                    <h3>Thông tin xe / chuyến</h3>
                    <div class="insp-vehicle">
                        <div class="insp-thumb">🚗</div>
                        <div>
                            <strong>${r.vehicle}</strong>
                            <dl class="mk-dl" style="margin-top:0.4rem">
                                <div><dt>Biển số</dt><dd>${r.plate}</dd></div>
                                <div><dt>Khách hàng</dt><dd>${r.customer}</dd></div>
                                <div><dt>Tài xế</dt><dd>${r.driver}</dd></div>
                                <div><dt>Hình thức</dt><dd>${r.mode}</dd></div>
                                <div><dt>Thời gian thuê</dt><dd>${r.rentFrom} → ${r.rentTo}</dd></div>
                            </dl>
                        </div>
                    </div>
                </div>
                ${odo(r)}
                <div class="mk-block">
                    <h3>Tình trạng xe</h3>
                    <div class="insp-cond-grid">
                        <div class="insp-cond"><span>🚗 Ngoại thất</span><strong>${r.exterior}</strong></div>
                        <div class="insp-cond"><span>🪑 Nội thất</span><strong>${r.interior}</strong><small class="mk-muted"> (không có API)</small></div>
                        <div class="insp-cond"><span>🛞 Lốp xe</span><strong>${r.tires}</strong><small class="mk-muted"> (không có API)</small></div>
                        <div class="insp-cond"><span>🔧 Kỹ thuật</span><strong>${r.technical}</strong></div>
                    </div>
                </div>
                <div class="mk-block">
                    <h3>Danh sách kiểm tra</h3>
                    <p class="mk-note">Checklist chi tiết không có trong API — bỏ trống.</p>
                </div>
                ${issueBox(r)}
                ${compare(r)}
                <div class="mk-block">
                    <h3>Ghi chú kiểm tra</h3>
                    <div class="insp-note-box">${(r.notes || "—").replace(/\n/g, "<br>")}</div>
                </div>
                <div class="mk-block">
                    <h3>Hình ảnh kiểm tra</h3>
                    <p class="mk-note">Không có API media / ảnh biên bản.</p>
                </div>
                <div class="insp-actions">
                    <button type="button" class="mk-btn mk-btn-ghost" data-edit="${r.id}">Chỉnh sửa (thiếu API)</button>
                    <button type="button" class="mk-btn mk-btn-ghost" data-print="${r.id}">In biên bản</button>
                </div>
                <p class="mk-note">Người kiểm tra không có trên DTO Backend.</p>
            </div>`;
    }

    function printRecord(r) {
        els.print.hidden = false;
        els.print.innerHTML = `
            <h1>Biên bản kiểm xe</h1>
            <p>${r.booking} · ${TYPE[r.type]} · ${r.datetime}</p>
            <p>${r.vehicle} · ${r.plate}<br>Khách hàng: ${r.customer}</p>
            <p>Số km: ${kmText(r.km)} · Nhiên liệu: ${r.fuel}% · Tình trạng: ${(COND[r.condition] || COND.good).text}</p>
            <p>Ngoại thất: ${r.exterior} · Kỹ thuật: ${r.technical}</p>
            <p>${(r.notes || "").replace(/\n/g, "<br>")}</p>
            <p>In từ dữ liệu Backend (presentation print).</p>`;
        window.print();
    }

    async function openById(id) {
        let r = records.find((x) => x.id === Number(id));
        if (handlers.detail) {
            const url = handlers.detail + (handlers.detail.includes("?") ? "&" : "?") + "id=" + id;
            const { ok, data } = await getJson(url);
            if (ok && data.record) {
                const i = records.findIndex((x) => x.id === data.record.id);
                if (i >= 0) records[i] = data.record;
                else records.unshift(data.record);
                r = data.record;
            }
        }
        if (r) openDrawer(r);
    }

    fillVehicles();
    render();

    els.form.addEventListener("submit", (e) => {
        e.preventDefault();
        const fd = new FormData(els.form);
        filters.q = String(fd.get("q") || "").trim();
        filters.from = String(fd.get("from") || "");
        filters.to = String(fd.get("to") || "");
        filters.vehicle = String(fd.get("vehicle") || "");
        filters.condition = String(fd.get("condition") || "");
        page = 1;
        render();
    });
    els.reset.addEventListener("click", () => {
        filters.q = filters.type = filters.vehicle = filters.condition = filters.from = filters.to = "";
        page = 1;
        setTimeout(() => { fillVehicles(); render(); }, 0);
    });
    document.getElementById("inspCreateBtn").addEventListener("click", () => {
        toast("Thiếu API tạo biên bản độc lập. Biên bản được tạo qua handover/complete/driver.");
        els.modal.hidden = false;
        els.modalBody.innerHTML = `
            <h2>Tạo biên bản kiểm xe</h2>
            <p class="mk-note">Không có endpoint POST inspection cho dispatcher. Dùng giao xe / trả xe SelfDrive (Trips) hoặc luồng tài xế.</p>
            <div class="mk-actions"><button type="button" class="mk-btn mk-btn-ghost" data-close-modal>Đóng</button></div>`;
    });

    document.body.addEventListener("click", async (e) => {
        if (e.target.closest("[data-close]")) {
            els.drawer.classList.remove("is-open");
            return;
        }
        if (e.target.closest("[data-close-modal]") || e.target === els.modal) {
            els.modal.hidden = true;
            return;
        }
        if (e.target.closest("[data-close-lightbox]") || e.target === els.lightbox) {
            els.lightbox.hidden = true;
            return;
        }
        const kpi = e.target.closest("[data-kpi]");
        if (kpi) {
            filters.condition = kpi.dataset.kpi || "";
            page = 1;
            render();
            return;
        }
        const pill = e.target.closest("[data-type]");
        if (pill && els.pills.contains(pill)) {
            filters.type = pill.dataset.type;
            page = 1;
            render();
            return;
        }
        const pg = e.target.closest("[data-page]");
        if (pg && els.pager.contains(pg) && !pg.disabled) {
            page = Number(pg.dataset.page);
            render();
            return;
        }
        const open = e.target.closest("[data-open]");
        if (open) {
            await openById(open.dataset.open);
            return;
        }
        const edit = e.target.closest("[data-edit]");
        if (edit) {
            toast("Thiếu API chỉnh sửa biên bản độc lập — không ghi Backend.");
            return;
        }
        const pr = e.target.closest("[data-print]");
        if (pr) {
            const r = records.find((x) => x.id === Number(pr.dataset.print));
            if (r) printRecord(r);
        }
    });
})();
