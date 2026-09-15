(function () {
    const DEMO = "Đây là chế độ mô phỏng — chưa kết nối hệ thống.";
    const DATA = {
        kpis: [
            { key: "pending", icon: "📋", label: "Đơn chờ xác nhận", value: "12", delta: "+3 so với hôm qua", dir: "up", href: "/Mockup/Hub" },
            { key: "trips", icon: "🚗", label: "Chuyến đang thực hiện", value: "28", delta: "+12% so với hôm qua", dir: "up", href: "/Mockup/Trips" },
            { key: "drivers", icon: "👨‍✈️", label: "Tài xế khả dụng", value: "15 / 32", delta: "47% khả dụng", dir: "up", href: "/Mockup/Schedule" },
            { key: "conflict", icon: "⚠️", label: "Cảnh báo xung đột", value: "3", delta: "-50% so với hôm qua", dir: "down", href: "/Mockup/Schedule" }
        ],
        charts: {
            today: {
                labels: ["06:00", "09:00", "12:00", "15:00", "18:00", "21:00"],
                completed: [2, 6, 11, 18, 27, 32],
                ongoing: [1, 8, 16, 24, 22, 28],
                bookings: [4, 9, 14, 12, 8, 6]
            },
            week: {
                labels: ["T2", "T3", "T4", "T5", "T6", "T7", "CN"],
                completed: [28, 31, 26, 34, 38, 22, 18],
                ongoing: [20, 24, 22, 28, 30, 16, 12],
                bookings: [18, 22, 19, 25, 27, 14, 10]
            },
            month: {
                labels: ["Tuần 1", "Tuần 2", "Tuần 3", "Tuần 4"],
                completed: [110, 128, 121, 96],
                ongoing: [80, 92, 88, 70],
                bookings: [74, 86, 81, 64]
            }
        },
        tripStatus: [
            { label: "Hoàn thành", value: 32, pct: 47, color: "#15803d" },
            { label: "Đang thực hiện", value: 28, pct: 41, color: "#2563eb" },
            { label: "Sắp nhận", value: 5, pct: 7, color: "#0369a1" },
            { label: "Đã hủy", value: 3, pct: 4, color: "#b91c1c" }
        ],
        tasks: [
            { id: "t1", tone: "🔴", title: "Xác nhận đơn thuê BK2026001", desc: "Khách hàng đang chờ xác nhận", time: "08:12", href: "/Mockup/Hub" },
            { id: "t2", tone: "🟡", title: "Xử lý xung đột lịch", desc: "Xe 51H-678.90 bị trùng lịch đặt", time: "08:40", href: "/Mockup/Schedule" },
            { id: "t3", tone: "🔵", title: "Phân công tài xế cho BK2026002", desc: "Chưa phân công tài xế", time: "09:05", href: "/Mockup/Hub" },
            { id: "t4", tone: "🟡", title: "Kiểm tra bảo trì xe", desc: "Xe 51K-123.45 đến hạn bảo trì trong 2 ngày", time: "09:20", href: "/Mockup/Inspections" },
            { id: "t5", tone: "🔵", title: "Theo dõi khách hàng", desc: "Lê Minh Đức đến trễ giờ nhận xe", time: "09:48", href: "/Mockup/Hub" }
        ],
        bookings: [
            { time: "08:00", customer: "Nguyễn Văn An", vehicle: "Toyota Vios", status: "confirmed", thumb: "🚗" },
            { time: "09:30", customer: "Trần Thị Bích Ngọc", vehicle: "Honda CR-V", status: "pending", thumb: "🚙" },
            { time: "10:00", customer: "Lê Minh Đức", vehicle: "Mazda CX-5", status: "pending", thumb: "🚘" },
            { time: "13:00", customer: "Phạm Thị Hà", vehicle: "Toyota Camry", status: "confirmed", thumb: "🚗" },
            { time: "15:00", customer: "Hoàng Gia Bảo", vehicle: "Kia Seltos", status: "pending", thumb: "🚙" }
        ],
        drivers: [
            { name: "Nguyễn Văn Hùng", rating: "5.0", status: "ontrip", plate: "51K-123.45", ini: "NH" },
            { name: "Trần Minh Nam", rating: "4.7", status: "ontrip", plate: "51H-678.90", ini: "TN" },
            { name: "Lê Quang Duy", rating: "4.8", status: "available", plate: "—", ini: "LD" },
            { name: "Phạm Tiến Đạt", rating: "4.5", status: "ontrip", plate: "51F-987.65", ini: "PD" },
            { name: "Hoàng Văn Nam", rating: "4.9", status: "offline", plate: "—", ini: "HN" }
        ],
        vehicles: [
            { key: "av", label: "Khả dụng", icon: "🟢", count: 22, color: "#15803d", pct: 38 },
            { key: "use", label: "Đang sử dụng", icon: "🔵", count: 28, color: "#2563eb", pct: 48 },
            { key: "mnt", label: "Bảo trì", icon: "🛠", count: 6, color: "#b45309", pct: 10 },
            { key: "oos", label: "Không khả dụng", icon: "🔴", count: 2, color: "#b91c1c", pct: 4 }
        ],
        activities: [
            { time: "10:24", icon: "📥", text: "Có đơn thuê mới BK2026018", sub: "Khách hàng: Vũ Thị Mai" },
            { time: "09:55", icon: "▶️", text: "Chuyến BK2026007 đã bắt đầu", sub: "Tài xế: Phạm Tiến Đạt" },
            { time: "09:30", icon: "⚠️", text: "Xe 51F-987.65 được báo có vấn đề nhỏ", sub: "Cần điều phối viên theo dõi" },
            { time: "08:15", icon: "✅", text: "Đơn thuê BK2026008 đã được xác nhận", sub: "Khách hàng: Hoàng Gia Bảo" }
        ],
        notices: [
            { icon: "🔧", title: "Bảo trì hệ thống theo lịch", body: "Hệ thống sẽ tạm ngưng để bảo trì", when: "20/09/2026 · 02:00 - 04:00" },
            { icon: "📢", title: "Tính năng mới", body: "Ứng dụng tài xế phiên bản 2.1 đã sẵn sàng", when: "14/09/2026" },
            { icon: "📄", title: "Cập nhật chính sách", body: "Chính sách phụ phí nhiên liệu mới áp dụng từ 01/10/2026", when: "Có hiệu lực 01/10/2026" }
        ],
        pins: [
            { id: "p1", name: "Toyota Vios", x: 28, y: 38 },
            { id: "p2", name: "Mercedes E-Class", x: 62, y: 30 },
            { id: "p3", name: "Toyota Camry", x: 74, y: 62 }
        ]
    };

    const STATUS = {
        confirmed: { text: "Đã xác nhận", cls: "mk-badge mk-badge-confirmed" },
        pending: { text: "Chờ xác nhận", cls: "mk-badge mk-badge-pending" },
        ontrip: { text: "Đang chạy", cls: "mk-badge mk-badge-inprogress" },
        available: { text: "Khả dụng", cls: "mk-badge mk-badge-ok" },
        offline: { text: "Ngoại tuyến", cls: "mk-badge mk-badge-gray" }
    };

    const els = {
        kpis: document.getElementById("dashKpis"),
        tasks: document.getElementById("dashTasks"),
        bookings: document.getElementById("dashBookings"),
        drivers: document.getElementById("dashDrivers"),
        vehicles: document.getElementById("dashVehicles"),
        activities: document.getElementById("dashActivities"),
        notices: document.getElementById("dashNotices"),
        legend: document.getElementById("dashDonutLegend"),
        map: document.getElementById("dashMap"),
        period: document.getElementById("dashPeriod"),
        drawer: document.getElementById("dashDrawer"),
        toast: document.getElementById("dashToast")
    };

    let perfChart;
    let mapScale = 1;

    function toast(msg) {
        els.toast.textContent = msg || DEMO;
        els.toast.classList.add("is-on");
        clearTimeout(toast._t);
        toast._t = setTimeout(() => els.toast.classList.remove("is-on"), 2600);
    }

    function chip(key) {
        const s = STATUS[key] || { text: key, cls: "mk-badge mk-badge-gray" };
        return `<span class="${s.cls}">${s.text}</span>`;
    }

    function actions(buttons) {
        return `<div class="dash-drawer-actions">${buttons.map((b) =>
            `<button type="button" class="mk-btn ${b.cls || "mk-btn-ghost"}" data-nav="${b.href || ""}">${b.label}</button>`
        ).join("")}</div>`;
    }

    function openDrawer(title, body) {
        els.drawer.classList.add("is-open");
        els.drawer.setAttribute("aria-hidden", "false");
        els.drawer.innerHTML = `
            <div class="mk-drawer-head">
                <h2 class="h5 mb-0">${title}</h2>
                <button type="button" class="mk-btn mk-btn-ghost" data-close>Đóng</button>
            </div>
            <div class="mk-drawer-body">
                ${body}
                <p class="mk-note">${DEMO}</p>
            </div>`;
    }

    els.kpis.innerHTML = DATA.kpis.map((k) => `
        <button type="button" class="dash-kpi" data-href="${k.href}" data-kind="kpi">
            <div class="dash-kpi-ico">${k.icon}</div>
            <span>${k.label}</span>
            <strong>${k.value}</strong>
            <em class="${k.dir}">${k.delta}</em>
        </button>`).join("");

    els.tasks.innerHTML = DATA.tasks.map((t) => `
        <button type="button" class="dash-task" data-href="${t.href}" data-kind="task" data-id="${t.id}">
            <span>${t.tone}</span>
            <span><strong>${t.title}</strong><br><small>${t.desc}</small></span>
            <time>${t.time}</time>
        </button>`).join("");

    els.bookings.innerHTML = DATA.bookings.map((b, i) => `
        <button type="button" class="dash-book" data-kind="booking" data-i="${i}">
            <time>${b.time}</time>
            <span class="dash-thumb">${b.thumb}</span>
            <span><strong>${b.customer}</strong><br><small>${b.vehicle}</small></span>
            ${chip(b.status)}
        </button>`).join("");

    els.drivers.innerHTML = DATA.drivers.map((d, i) => `
        <button type="button" class="dash-drv" data-kind="driver" data-i="${i}">
            <span class="dash-ava">${d.ini}</span>
            <span><strong>${d.name}</strong><br><small>⭐ ${d.rating} · ${d.plate}</small></span>
            ${chip(d.status)}
        </button>`).join("");

    els.vehicles.innerHTML = `<div class="dash-vgrid">${DATA.vehicles.map((v) => `
        <button type="button" class="dash-vcell" data-kind="vehicle" data-key="${v.key}">
            <span>${v.icon} ${v.label}</span>
            <strong>${v.count}</strong>
        </button>`).join("")}</div>
        <div class="dash-bar">${DATA.vehicles.map((v) => `<i style="width:${v.pct}%;background:${v.color}"></i>`).join("")}</div>`;

    els.activities.innerHTML = DATA.activities.map((a, i) => `
        <button type="button" class="dash-act" data-kind="activity" data-i="${i}">
            <span>${a.icon}</span>
            <span><time>${a.time}</time><strong>${a.text}</strong><br><small>${a.sub}</small></span>
        </button>`).join("");

    els.notices.innerHTML = DATA.notices.map((n, i) => `
        <button type="button" class="dash-note" data-kind="notice" data-i="${i}">
            <span>${n.icon}</span>
            <span><strong>${n.title}</strong><br><small>${n.body}</small><br><small>${n.when}</small></span>
        </button>`).join("");

    els.legend.innerHTML = DATA.tripStatus.map((s) =>
        `<li><span><i class="dash-dot" style="background:${s.color}"></i>${s.label}</span><b>${s.value} (${s.pct}%)</b></li>`
    ).join("");

    DATA.pins.forEach((p) => {
        els.map.insertAdjacentHTML("beforeend",
            `<button type="button" class="dash-pin" style="left:${p.x}%;top:${p.y}%" data-kind="pin" data-id="${p.id}"><span>📍</span> ${p.name}</button>`);
    });

    const axis = "#64748b";
    const grid = "#e2e8f0";

    function renderPerf(period) {
        const pack = DATA.charts[period] || DATA.charts.today;
        const ctx = document.getElementById("dashPerfChart");
        if (perfChart) perfChart.destroy();
        perfChart = new Chart(ctx, {
            type: "bar",
            data: {
                labels: pack.labels,
                datasets: [
                    { type: "bar", label: "Chuyến hoàn thành", data: pack.completed, backgroundColor: "rgba(22,163,74,.78)", borderRadius: 6, yAxisID: "y" },
                    { type: "bar", label: "Chuyến đang thực hiện", data: pack.ongoing, backgroundColor: "rgba(37,99,235,.78)", borderRadius: 6, yAxisID: "y" },
                    { type: "line", label: "Đơn tiếp nhận", data: pack.bookings, borderColor: "#0891b2", backgroundColor: "transparent", tension: 0.35, pointRadius: 3, yAxisID: "y" }
                ]
            },
            options: {
                responsive: true, maintainAspectRatio: false,
                plugins: {
                    legend: { labels: { color: axis, boxWidth: 10, font: { size: 11 } } }
                },
                scales: {
                    x: { ticks: { color: axis }, grid: { color: grid } },
                    y: { ticks: { color: axis }, grid: { color: grid }, beginAtZero: true }
                }
            }
        });
    }

    new Chart(document.getElementById("dashDonut"), {
        type: "doughnut",
        data: {
            labels: DATA.tripStatus.map((s) => s.label),
            datasets: [{ data: DATA.tripStatus.map((s) => s.value), backgroundColor: DATA.tripStatus.map((s) => s.color), borderWidth: 0, hoverOffset: 4 }]
        },
        options: {
            responsive: true, maintainAspectRatio: false, cutout: "72%",
            plugins: { legend: { display: false } }
        }
    });
    renderPerf("today");

    els.period.addEventListener("change", () => {
        renderPerf(els.period.value);
        toast(DEMO);
    });

    document.body.addEventListener("click", (e) => {
        const navBtn = e.target.closest("[data-nav]");
        if (navBtn && navBtn.dataset.nav) {
            toast(DEMO);
            window.location.href = navBtn.dataset.nav;
            return;
        }
        if (e.target.closest("[data-close]")) {
            els.drawer.classList.remove("is-open");
            return;
        }
        const mapBtn = e.target.closest("[data-map]");
        if (mapBtn) {
            const act = mapBtn.dataset.map;
            if (act === "in") mapScale = Math.min(1.35, mapScale + 0.12);
            else if (act === "out") mapScale = Math.max(0.85, mapScale - 0.12);
            else mapScale = 1;
            els.map.style.transform = `scale(${mapScale})`;
            toast("Chế độ mô phỏng — chưa kết nối định vị hay hệ thống.");
            return;
        }
        const item = e.target.closest("[data-kind]");
        if (!item) return;
        const kind = item.dataset.kind;
        if (kind === "kpi") {
            toast(DEMO);
            if (item.dataset.href) window.location.href = item.dataset.href;
            return;
        }
        if (kind === "task") {
            const t = DATA.tasks.find((x) => x.id === item.dataset.id);
            openDrawer("Công việc cần xử lý", `<p><strong>${t.title}</strong><br><span class="mk-muted">${t.desc}<br>${t.time}</span></p>`
                + actions([
                    { label: "Xử lý ngay", cls: "mk-btn-primary", href: t.href },
                    { label: "Xem chi tiết", href: t.href }
                ]));
            toast(DEMO);
            return;
        }
        if (kind === "booking") {
            const b = DATA.bookings[Number(item.dataset.i)];
            openDrawer("Đơn thuê hôm nay", `<p><strong>${b.time}</strong> · ${b.customer}<br>${b.vehicle}<br>Trạng thái: ${STATUS[b.status].text}</p>`
                + actions([
                    { label: "Xem đơn thuê", cls: "mk-btn-primary", href: "/Mockup/Hub" },
                    { label: "Xem chi tiết", href: "/Mockup/Hub" }
                ]));
            toast(DEMO);
            return;
        }
        if (kind === "driver") {
            const d = DATA.drivers[Number(item.dataset.i)];
            openDrawer("Tài xế đang hoạt động", `<p><strong>${d.name}</strong><br>⭐ ${d.rating}<br>${STATUS[d.status].text}<br>Xe: ${d.plate}</p>`
                + actions([
                    { label: "Liên hệ tài xế", cls: "mk-btn-primary", href: "/Mockup/Schedule" },
                    { label: "Xem chi tiết", href: "/Mockup/Schedule" }
                ]));
            toast(DEMO);
            return;
        }
        if (kind === "vehicle") {
            const v = DATA.vehicles.find((x) => x.key === item.dataset.key);
            openDrawer("Tình trạng xe", `<p><strong>${v.icon} ${v.label}</strong><br>${v.count} xe (dữ liệu mẫu)</p>`
                + actions([
                    { label: "Xem xe", cls: "mk-btn-primary", href: "/Mockup/Schedule" },
                    { label: "Xem chi tiết", href: "/Mockup/Schedule" }
                ]));
            toast(DEMO);
            return;
        }
        if (kind === "activity") {
            const a = DATA.activities[Number(item.dataset.i)];
            openDrawer("Hoạt động gần đây", `<p><strong>${a.time}</strong><br>${a.text}<br>${a.sub}</p>`
                + actions([{ label: "Xem chi tiết", cls: "mk-btn-primary", href: "/Mockup/Notifications" }]));
            toast(DEMO);
            return;
        }
        if (kind === "notice") {
            const n = DATA.notices[Number(item.dataset.i)];
            openDrawer("Thông báo hệ thống", `<p><strong>${n.title}</strong><br>${n.body}<br>${n.when}</p>`
                + actions([{ label: "Xem tất cả", cls: "mk-btn-primary", href: "/Mockup/Notifications" }]));
            toast(DEMO);
            return;
        }
        if (kind === "pin") {
            const p = DATA.pins.find((x) => x.id === item.dataset.id);
            openDrawer("Bản đồ theo dõi", `<p>📍 ${p.name}<br>Vị trí mô phỏng — không phải định vị thật.<br>Chế độ mô phỏng</p>`
                + actions([
                    { label: "Xem bản đồ", cls: "mk-btn-primary", href: "/Mockup/Trips" },
                    { label: "Xem xe", href: "/Mockup/Schedule" }
                ]));
            toast(DEMO);
        }
    });
})();
