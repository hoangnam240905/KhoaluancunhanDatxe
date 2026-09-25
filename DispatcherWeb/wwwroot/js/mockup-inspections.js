(function () {
    const DEMO = "Đây là chế độ mô phỏng — chưa kết nối hệ thống.";
    const PAGE_SIZE = 10;
    const CHECKS = ["Giấy tờ xe", "Chìa khóa", "Ngoại thất", "Nội thất", "Lốp xe", "Đèn xe", "Phanh", "Điều hòa", "Thiết bị an toàn"];
    const PHOTOS = ["Mặt trước", "Mặt sau", "Bên trái", "Bên phải", "Nội thất", "Đồng hồ km"];
    const TYPE = { checkout: "Giao xe", checkin: "Nhận xe" };
    const COND = {
        good: { text: "Tốt", cls: "mk-badge-ok" },
        minor: { text: "Vấn đề nhỏ", cls: "mk-badge-warn" },
        inspect: { text: "Cần kiểm tra", cls: "insp-badge-inspect" },
        major: { text: "Vấn đề nghiêm trọng", cls: "mk-badge-incident" }
    };
    const VEHICLES = [
        ["Toyota Vios", "51K-123.45"], ["Honda CR-V", "51H-678.90"], ["Mazda CX-5", "51A-456.78"],
        ["Toyota Camry", "51C-321.00"], ["Kia Seltos", "51F-987.65"], ["Mercedes E-Class", "51A-888.88"],
        ["Kia Carnival", "51G-456.78"], ["Hyundai Santa Fe", "51F-999.00"], ["VinFast VF8", "51K-777.77"],
        ["Mazda 3", "51D-222.11"], ["Ford Transit", "51H-246.80"], ["BMW 5 Series", "51A-555.55"]
    ];
    const CUSTOMERS = ["Nguyễn Văn An", "Trần Thị Bích Ngọc", "Lê Minh Đức", "Phạm Thị Hà", "Hoàng Gia Bảo", "Vũ Thị Mai", "Ngô Phương Chi", "Đặng Gia Linh", "Mai Lan Phương", "Lý Ngọc Trâm"];
    const INSPECTORS = ["Nguyễn Văn Hùng", "Trần Minh Tuấn", "Lê Quang Duy", "Phạm Tiến Đạt", "Hoàng Văn Nam", "Trần Minh Nam"];
    const DRIVERS = ["Nguyễn Văn Hùng", "Trần Minh Nam", "Lê Quang Duy", "Phạm Tiến Đạt"];

    function allChecked(off) {
        const o = {};
        CHECKS.forEach((c) => { o[c] = !off || !off.includes(c); });
        return o;
    }
    function kmText(n) { return Number(n).toLocaleString("vi-VN") + " km"; }

    const SEED = [
        { id: 1, booking: "BK202601", plate: "51K-123.45", vehicle: "Toyota Vios", type: "checkout", km: 28450, fuel: 80, condition: "good", datetime: "15/09/2026 08:00", dateIso: "2026-09-15", inspector: "Nguyễn Văn Hùng", customer: "Nguyễn Văn An", driver: "Nguyễn Văn Hùng", mode: "Có tài xế", rentFrom: "15/09/2026", rentTo: "17/09/2026", exterior: "Tốt", interior: "Tốt", tires: "Tốt", technical: "Tốt", notes: "Xe hoạt động bình thường.\nKhông phát hiện vết trầy xước mới.", issue: null },
        { id: 2, booking: "BK202602", plate: "51H-678.90", vehicle: "Honda CR-V", type: "checkin", km: 42850, kmOut: 42470, fuel: 65, fuelOut: 80, condition: "minor", datetime: "15/09/2026 17:30", dateIso: "2026-09-15", inspector: "Trần Minh Tuấn", customer: "Trần Thị Bích Ngọc", driver: "Khách tự lái", mode: "Tự lái", rentFrom: "14/09/2026", rentTo: "15/09/2026", exterior: "Vấn đề nhỏ", interior: "Tốt", tires: "Tốt", technical: "Tốt", notes: "Phát hiện vết xước nhẹ ở cản trước khi nhận xe.", issue: { title: "Xước nhẹ ở cản trước", level: "Nhỏ", note: "Vết xước dài khoảng 5 cm." }, checkoutRef: { km: 42470, fuel: 80, exterior: "Tốt", technical: "Tốt" } },
        { id: 3, booking: "BK202603", plate: "51A-456.78", vehicle: "Mazda CX-5", type: "checkout", km: 18760, fuel: 90, condition: "good", datetime: "16/09/2026 09:00", dateIso: "2026-09-16", inspector: "Lê Quang Duy", customer: "Lê Minh Đức", driver: "Lê Quang Duy", mode: "Có tài xế", rentFrom: "16/09/2026", rentTo: "18/09/2026", exterior: "Tốt", interior: "Tốt", tires: "Tốt", technical: "Tốt", notes: "Xe sạch, đầy đủ giấy tờ và chìa khóa.", issue: null },
        { id: 4, booking: "BK202604", plate: "51C-321.00", vehicle: "Toyota Camry", type: "checkin", km: 53180, kmOut: 52800, fuel: 55, fuelOut: 85, condition: "good", datetime: "16/09/2026 18:10", dateIso: "2026-09-16", inspector: "Phạm Tiến Đạt", customer: "Phạm Thị Hà", driver: "Phạm Tiến Đạt", mode: "Có tài xế", rentFrom: "15/09/2026", rentTo: "16/09/2026", exterior: "Tốt", interior: "Tốt", tires: "Tốt", technical: "Tốt", notes: "Nhận xe đúng giờ, không phát sinh hư hỏng.", issue: null, checkoutRef: { km: 52800, fuel: 85, exterior: "Tốt", technical: "Tốt" } },
        { id: 5, booking: "BK202605", plate: "51F-987.65", vehicle: "Kia Seltos", type: "checkout", km: 22110, fuel: 70, condition: "inspect", datetime: "16/09/2026 07:40", dateIso: "2026-09-16", inspector: "Hoàng Văn Nam", customer: "Hoàng Gia Bảo", driver: "Khách tự lái", mode: "Tự lái", rentFrom: "16/09/2026", rentTo: "19/09/2026", exterior: "Tốt", interior: "Tốt", tires: "Cần kiểm tra", technical: "Cần kiểm tra", notes: "Áp suất lốp sau trái hơi thấp. Nên kiểm tra trước khi giao.", issue: { title: "Áp suất lốp sau trái thấp", level: "Cần kiểm tra", note: "Đề nghị bơm lốp và theo dõi." }, off: ["Lốp xe"] },
        { id: 6, booking: "BK202606", plate: "51A-888.88", vehicle: "Mercedes E-Class", type: "checkin", km: 61240, kmOut: 60810, fuel: 40, fuelOut: 90, condition: "major", datetime: "17/09/2026 20:00", dateIso: "2026-09-17", inspector: "Nguyễn Văn Hùng", customer: "Ngô Phương Chi", driver: "Khách tự lái", mode: "Tự lái", rentFrom: "15/09/2026", rentTo: "17/09/2026", exterior: "Vấn đề nghiêm trọng", interior: "Vấn đề nhỏ", tires: "Tốt", technical: "Tốt", notes: "Cản trước móp rõ. Nội thất có vết bẩn.", issue: { title: "Móp cản trước bên phải", level: "Nghiêm trọng", note: "Cần đưa vào xưởng trước chuyến tiếp theo." }, checkoutRef: { km: 60810, fuel: 90, exterior: "Tốt", technical: "Tốt" }, off: ["Ngoại thất"] },
        { id: 7, booking: "BK202607", plate: "51G-456.78", vehicle: "Kia Carnival", type: "checkout", km: 33420, fuel: 85, condition: "good", datetime: "17/09/2026 06:30", dateIso: "2026-09-17", inspector: "Trần Minh Nam", customer: "Lê Thị Hương", driver: "Trần Minh Nam", mode: "Có tài xế", rentFrom: "17/09/2026", rentTo: "19/09/2026", exterior: "Tốt", interior: "Tốt", tires: "Tốt", technical: "Tốt", notes: "Xe 7 chỗ sạch sẽ, ghế trẻ em đầy đủ.", issue: null },
        { id: 8, booking: "BK202608", plate: "51F-999.00", vehicle: "Hyundai Santa Fe", type: "checkin", km: 47920, kmOut: 47540, fuel: 60, fuelOut: 75, condition: "minor", datetime: "17/09/2026 19:15", dateIso: "2026-09-17", inspector: "Lê Quang Duy", customer: "Võ Thanh Hà", driver: "Lê Quang Duy", mode: "Có tài xế", rentFrom: "16/09/2026", rentTo: "17/09/2026", exterior: "Tốt", interior: "Vấn đề nhỏ", tires: "Tốt", technical: "Tốt", notes: "Ghế sau có vết ố nhỏ.", issue: { title: "Vết ố ghế sau", level: "Nhỏ", note: "Cần vệ sinh nội thất." }, checkoutRef: { km: 47540, fuel: 75, exterior: "Tốt", technical: "Tốt" } },
        { id: 9, booking: "BK202609", plate: "51K-777.77", vehicle: "VinFast VF8", type: "checkout", km: 15200, fuel: 95, condition: "good", datetime: "18/09/2026 08:20", dateIso: "2026-09-18", inspector: "Phạm Tiến Đạt", customer: "Đặng Gia Linh", driver: "Khách tự lái", mode: "Tự lái", rentFrom: "18/09/2026", rentTo: "20/09/2026", exterior: "Tốt", interior: "Tốt", tires: "Tốt", technical: "Tốt", notes: "Pin sạc đầy, không lỗi cảnh báo.", issue: null },
        { id: 10, booking: "BK202610", plate: "51D-222.11", vehicle: "Mazda 3", type: "checkin", km: 30110, kmOut: 29840, fuel: 50, fuelOut: 70, condition: "good", datetime: "18/09/2026 16:40", dateIso: "2026-09-18", inspector: "Hoàng Văn Nam", customer: "Phạm Quốc Bảo", driver: "Khách tự lái", mode: "Tự lái", rentFrom: "18/09/2026", rentTo: "18/09/2026", exterior: "Tốt", interior: "Tốt", tires: "Tốt", technical: "Tốt", notes: "Nhận xe trong ngày, tình trạng ổn.", issue: null, checkoutRef: { km: 29840, fuel: 70, exterior: "Tốt", technical: "Tốt" } },
        { id: 11, booking: "BK202611", plate: "51H-246.80", vehicle: "Ford Transit", type: "checkout", km: 88900, fuel: 60, condition: "minor", datetime: "18/09/2026 05:10", dateIso: "2026-09-18", inspector: "Nguyễn Văn Hùng", customer: "Hoàng Đức Anh", driver: "Nguyễn Văn Hùng", mode: "Có tài xế", rentFrom: "18/09/2026", rentTo: "20/09/2026", exterior: "Vấn đề nhỏ", interior: "Tốt", tires: "Tốt", technical: "Tốt", notes: "Trầy nhẹ gương trái từ chuyến trước, đã ghi nhận.", issue: { title: "Trầy gương trái", level: "Nhỏ", note: "Đã có từ trước, khách được thông báo." } },
        { id: 12, booking: "BK202612", plate: "51A-555.55", vehicle: "BMW 5 Series", type: "checkin", km: 41200, kmOut: 40820, fuel: 45, fuelOut: 80, condition: "inspect", datetime: "19/09/2026 11:00", dateIso: "2026-09-19", inspector: "Trần Minh Tuấn", customer: "Mai Lan Phương", driver: "Khách tự lái", mode: "Tự lái", rentFrom: "17/09/2026", rentTo: "19/09/2026", exterior: "Tốt", interior: "Tốt", tires: "Tốt", technical: "Cần kiểm tra", notes: "Đèn cảnh báo động cơ nhấp nháy lúc nhận.", issue: { title: "Đèn cảnh báo động cơ", level: "Cần kiểm tra", note: "Cần kỹ thuật viên đọc lỗi." }, checkoutRef: { km: 40820, fuel: 80, exterior: "Tốt", technical: "Tốt" }, off: ["Phanh"] },
        { id: 13, booking: "BK202613", plate: "51K-123.45", vehicle: "Toyota Vios", type: "checkout", km: 28830, fuel: 75, condition: "good", datetime: "19/09/2026 08:00", dateIso: "2026-09-19", inspector: "Lê Quang Duy", customer: "Vũ Thị Mai", driver: "Lê Quang Duy", mode: "Có tài xế", rentFrom: "19/09/2026", rentTo: "21/09/2026", exterior: "Tốt", interior: "Tốt", tires: "Tốt", technical: "Tốt", notes: "Giao xe cho chuyến tiếp theo sau khi vệ sinh.", issue: null },
        { id: 14, booking: "BK202614", plate: "51H-678.90", vehicle: "Honda CR-V", type: "checkin", km: 44120, kmOut: 42850, fuel: 30, fuelOut: 65, condition: "major", datetime: "19/09/2026 21:20", dateIso: "2026-09-19", inspector: "Phạm Tiến Đạt", customer: "Trịnh Văn Khoa", driver: "Phạm Tiến Đạt", mode: "Có tài xế", rentFrom: "18/09/2026", rentTo: "19/09/2026", exterior: "Tốt", interior: "Tốt", tires: "Vấn đề nghiêm trọng", technical: "Vấn đề nghiêm trọng", notes: "Lốp trước phải thủng, khách đã thay bản vá tạm.", issue: { title: "Lốp trước phải thủng", level: "Nghiêm trọng", note: "Cần thay lốp mới trước khi cho thuê lại." }, checkoutRef: { km: 42850, fuel: 65, exterior: "Vấn đề nhỏ", technical: "Tốt" }, off: ["Lốp xe"] },
        { id: 15, booking: "BK202615", plate: "51A-111.11", vehicle: "Mercedes V-Class", type: "checkout", km: 27440, fuel: 88, condition: "good", datetime: "20/09/2026 07:30", dateIso: "2026-09-20", inspector: "Hoàng Văn Nam", customer: "Lý Ngọc Trâm", driver: "Hoàng Văn Nam", mode: "Có tài xế", rentFrom: "20/09/2026", rentTo: "20/09/2026", exterior: "Tốt", interior: "Tốt", tires: "Tốt", technical: "Tốt", notes: "Xe đưa đón trong ngày, đầy đủ nước và khăn.", issue: null },
        { id: 16, booking: "BK202616", plate: "51E-135.79", vehicle: "Honda CR-V", type: "checkin", km: 35680, kmOut: 35290, fuel: 58, fuelOut: 72, condition: "minor", datetime: "20/09/2026 18:45", dateIso: "2026-09-20", inspector: "Trần Minh Nam", customer: "Mai Lan Phương", driver: "Khách tự lái", mode: "Tự lái", rentFrom: "18/09/2026", rentTo: "20/09/2026", exterior: "Vấn đề nhỏ", interior: "Tốt", tires: "Tốt", technical: "Tốt", notes: "Trầy nhẹ cánh cửa phụ.", issue: { title: "Trầy cánh cửa phụ", level: "Nhỏ", note: "Vết sơn dài khoảng 8 cm." }, checkoutRef: { km: 35290, fuel: 72, exterior: "Tốt", technical: "Tốt" } }
    ];

    function finish(row) {
        const r = { ...row };
        r.checklist = allChecked(r.off);
        r.photos = PHOTOS.slice();
        if (r.type === "checkin") {
            r.kmOut = r.kmOut ?? Math.max(0, r.km - 380);
            r.fuelOut = r.fuelOut ?? Math.min(100, r.fuel + 15);
            r.checkoutRef = r.checkoutRef || { km: r.kmOut, fuel: r.fuelOut, exterior: "Tốt", technical: "Tốt" };
        }
        return r;
    }

    function generate() {
        const list = SEED.map((r) => finish(r));
        let next = 17;
        const plan = [];
        for (let i = 0; i < 190; i++) plan.push("good");
        for (let i = 0; i < 30; i++) plan.push("minor");
        for (let i = 0; i < 6; i++) plan.push("inspect");
        for (let i = 0; i < 14; i++) plan.push("major");
        plan.forEach((condition, idx) => {
            const [vehicle, plate] = VEHICLES[idx % VEHICLES.length];
            const type = idx % 2 === 0 ? "checkout" : "checkin";
            const day = 1 + (idx % 28);
            const dd = String(day).padStart(2, "0");
            const km = 12000 + idx * 137;
            const fuel = 40 + (idx % 6) * 10;
            const row = {
                id: next,
                booking: "BK2026" + String(600 + next).slice(-3),
                plate, vehicle, type, km, fuel, condition,
                datetime: `${dd}/09/2026 ${String(7 + (idx % 12)).padStart(2, "0")}:${String((idx * 7) % 60).padStart(2, "0")}`,
                dateIso: `2026-09-${dd}`,
                inspector: INSPECTORS[idx % INSPECTORS.length],
                customer: CUSTOMERS[idx % CUSTOMERS.length],
                driver: idx % 3 === 0 ? "Khách tự lái" : DRIVERS[idx % DRIVERS.length],
                mode: idx % 3 === 0 ? "Tự lái" : "Có tài xế",
                rentFrom: `${dd}/09/2026`,
                rentTo: `${String(Math.min(30, day + 2)).padStart(2, "0")}/09/2026`,
                exterior: condition === "good" ? "Tốt" : condition === "major" ? "Vấn đề nghiêm trọng" : condition === "inspect" ? "Cần kiểm tra" : "Vấn đề nhỏ",
                interior: condition === "minor" && idx % 2 ? "Vấn đề nhỏ" : "Tốt",
                tires: condition === "inspect" ? "Cần kiểm tra" : "Tốt",
                technical: condition === "major" ? "Vấn đề nghiêm trọng" : "Tốt",
                notes: condition === "good" ? "Xe hoạt động bình thường.\nKhông phát hiện vết trầy xước mới." : "Đã ghi nhận tình trạng khi kiểm tra.",
                issue: condition === "good" ? null : { title: condition === "major" ? "Hư hỏng được ghi nhận" : "Vết xước nhẹ", level: COND[condition].text, note: "Ghi nhận trên biên bản mô phỏng." }
            };
            if (type === "checkin") {
                row.kmOut = km - 380;
                row.fuelOut = Math.min(100, fuel + 15);
            }
            list.push(finish(row));
            next += 1;
        });
        return list;
    }

    let records = generate();
    let nextId = records.reduce((m, r) => Math.max(m, r.id), 0) + 1;
    const filters = { q: "", type: "", vehicle: "", condition: "", from: "", to: "" };
    let page = 1;
    let editingId = null;

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

    function toast(msg) {
        els.toast.textContent = msg || DEMO;
        els.toast.classList.add("is-on");
        clearTimeout(toast._t);
        toast._t = setTimeout(() => els.toast.classList.remove("is-on"), 2600);
    }

    function typeBadge(t) {
        return `<span class="mk-badge ${t === "checkout" ? "mk-badge-ok" : "mk-badge-confirmed"}">${TYPE[t]}</span>`;
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
                const blob = `${r.booking} ${r.plate} ${r.vehicle} ${r.customer} ${r.inspector}`.toLowerCase();
                if (!blob.includes(q)) return false;
            }
            return true;
        });
    }

    function kpis() {
        const n = records.length;
        const good = records.filter((r) => r.condition === "good").length;
        const minor = records.filter((r) => r.condition === "minor" || r.condition === "inspect").length;
        const major = records.filter((r) => r.condition === "major").length;
        const pct = (x) => (n ? Math.round(x * 100 / n) + "% tổng số" : "0%");
        const items = [
            { key: "", icon: "📋", label: "Tổng biên bản", value: n, hint: "Trong tháng này" },
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
        const names = [...new Set(records.map((r) => r.vehicle))].sort((a, b) => a.localeCompare(b, "vi"));
        const cur = filters.vehicle;
        els.vehicle.innerHTML = `<option value="">Tất cả xe</option>` + names.map((n) => `<option value="${n}">${n}</option>`).join("");
        els.vehicle.value = names.includes(cur) ? cur : "";
    }

    function rowHtml(r, stt) {
        return `<tr>
            <td>${stt}</td>
            <td><strong>${r.booking}</strong></td>
            <td>${r.plate}</td>
            <td>${r.vehicle}</td>
            <td>${typeBadge(r.type)}<div class="mk-muted">${r.type === "checkout" ? "Kiểm tra trước khi bàn giao xe" : "Kiểm tra sau khi khách trả xe"}</div></td>
            <td>${kmText(r.km)}</td>
            <td>${r.fuel}%</td>
            <td>${condBadge(r.condition)}</td>
            <td>${r.datetime}</td>
            <td>${r.inspector}</td>
            <td><button type="button" class="mk-btn mk-btn-ghost" data-open="${r.id}">Xem</button></td>
        </tr>`;
    }

    function cardHtml(r) {
        return `<article class="insp-card">
            <div class="insp-card-top">
                <div><strong>${r.booking}</strong><div class="mk-muted">${r.vehicle} · ${r.plate}</div></div>
                ${typeBadge(r.type)}
            </div>
            <p class="mk-muted" style="margin:0.45rem 0">${r.datetime} · ${r.inspector}</p>
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
        els.meta.textContent = `Hiển thị ${from}–${to} trong ${all.length} biên bản`;
        const windowStart = Math.max(1, Math.min(page - 2, Math.max(1, pages - 4)));
        const nums = [];
        for (let i = windowStart; i <= Math.min(pages, windowStart + 4); i++) nums.push(i);
        els.pager.innerHTML = `
            <button type="button" class="mk-btn mk-btn-ghost" data-page="${Math.max(1, page - 1)}" ${page === 1 ? "disabled" : ""}>←</button>
            ${nums.map((n) => `<button type="button" class="mk-btn mk-btn-ghost ${n === page ? "is-on" : ""}" data-page="${n}">${n}</button>`).join("")}
            <button type="button" class="mk-btn mk-btn-ghost" data-page="${Math.min(pages, page + 1)}" ${page === pages ? "disabled" : ""}>→</button>`;
    }

    function photoBtns(r) {
        return r.photos.map((p, i) =>
            `<button type="button" class="insp-photo p${i + 1}" data-photo="${i}" data-id="${r.id}"><span>📷</span><small>${p}</small></button>`
        ).join("");
    }

    function checks(r) {
        return `<ul class="insp-checks">${CHECKS.map((c) => {
            const ok = r.checklist[c];
            return `<li class="${ok ? "ok" : "miss"}">${ok ? "☑" : "☐"} ${c}</li>`;
        }).join("")}</ul>`;
    }

    function compare(r) {
        if (r.type !== "checkin" || !r.checkoutRef) return "";
        const actual = r.km - (r.kmOut ?? r.checkoutRef.km);
        const changed = r.exterior !== r.checkoutRef.exterior || r.technical !== r.checkoutRef.technical;
        return `<div class="mk-block">
            <h3>Đối chiếu giao xe / nhận xe</h3>
            <div class="insp-compare">
                <article>
                    <h4>Lúc giao xe</h4>
                    <dl class="mk-dl">
                        <div><dt>Số km</dt><dd>${kmText(r.kmOut ?? r.checkoutRef.km)}</dd></div>
                        <div><dt>Nhiên liệu</dt><dd>${r.fuelOut ?? r.checkoutRef.fuel}%</dd></div>
                        <div><dt>Ngoại thất</dt><dd>${r.checkoutRef.exterior}</dd></div>
                        <div><dt>Kỹ thuật</dt><dd>${r.checkoutRef.technical}</dd></div>
                    </dl>
                </article>
                <article>
                    <h4>Lúc nhận xe</h4>
                    <dl class="mk-dl">
                        <div><dt>Số km</dt><dd>${kmText(r.km)}</dd></div>
                        <div><dt>Nhiên liệu</dt><dd>${r.fuel}%</dd></div>
                        <div><dt>Ngoại thất</dt><dd>${r.exterior}</dd></div>
                        <div><dt>Kỹ thuật</dt><dd>${r.technical}</dd></div>
                    </dl>
                </article>
            </div>
            <p style="margin:0.7rem 0 0"><strong>Quãng đường thực tế:</strong> ${kmText(actual)}</p>
            ${changed ? `<p class="mk-note" style="margin-top:0.35rem">⚠️ Có thay đổi ngoại thất</p>` : `<p class="mk-note" style="margin-top:0.35rem">Không có thay đổi ngoại thất / kỹ thuật.</p>`}
        </div>`;
    }

    function odo(r) {
        if (r.type === "checkin") {
            const actual = r.km - (r.kmOut ?? r.km);
            return `<div class="mk-block">
                <h3>Số km &amp; nhiên liệu</h3>
                <dl class="mk-dl">
                    <div><dt>Số km giao</dt><dd>${kmText(r.kmOut)}</dd></div>
                    <div><dt>Số km nhận</dt><dd>${kmText(r.km)}</dd></div>
                    <div><dt>Quãng đường thực tế</dt><dd>${kmText(actual)}</dd></div>
                    <div><dt>Nhiên liệu</dt><dd>${r.fuel}%<div class="insp-fuel"><i style="width:${r.fuel}%"></i></div></dd></div>
                </dl>
            </div>`;
        }
        return `<div class="mk-block">
            <h3>Số km &amp; nhiên liệu</h3>
            <dl class="mk-dl">
                <div><dt>Số km</dt><dd>${kmText(r.km)}</dd></div>
                <div><dt>Nhiên liệu</dt><dd>${r.fuel}%<div class="insp-fuel"><i style="width:${r.fuel}%"></i></div></dd></div>
            </dl>
        </div>`;
    }

    function issueBox(r) {
        if (!r.issue) return "";
        return `<div class="mk-block insp-issue">
            <h4>⚠️ Vấn đề được ghi nhận</h4>
            <p style="margin:0 0 0.35rem"><strong>${r.issue.title}</strong></p>
            <p class="mk-muted" style="margin:0 0 0.45rem">Mức độ: ${r.issue.level}<br>${r.issue.note}</p>
            <button type="button" class="mk-btn mk-btn-ghost" data-photo="0" data-id="${r.id}">Xem hình ảnh</button>
        </div>`;
    }

    function openDrawer(id) {
        const r = records.find((x) => x.id === Number(id));
        if (!r) return;
        els.drawer.classList.add("is-open");
        els.drawer.setAttribute("aria-hidden", "false");
        els.drawer.innerHTML = `
            <div class="mk-drawer-head">
                <div>
                    <h2 class="h5 mb-0">Chi tiết biên bản</h2>
                    <p class="mk-muted mb-0">${r.booking} · ${r.datetime}</p>
                </div>
                <button type="button" class="mk-btn mk-btn-ghost" data-close>Đóng</button>
            </div>
            <div class="mk-drawer-body">
                <dl class="mk-dl mk-block">
                    <div><dt>Mã đơn</dt><dd>${r.booking}</dd></div>
                    <div><dt>Ngày</dt><dd>${r.datetime}</dd></div>
                    <div><dt>Loại</dt><dd>${TYPE[r.type]}</dd></div>
                    <div><dt>Tình trạng</dt><dd>${condBadge(r.condition)}</dd></div>
                </dl>
                <div class="mk-block">
                    <h3>Thông tin xe</h3>
                    <div class="insp-vehicle">
                        <div class="insp-thumb">🚗</div>
                        <div>
                            <strong>${r.vehicle}</strong>
                            <dl class="mk-dl" style="margin-top:0.4rem">
                                <div><dt>Biển số</dt><dd>${r.plate}</dd></div>
                                <div><dt>Khách hàng</dt><dd>${r.customer}</dd></div>
                                <div><dt>Tài xế</dt><dd>${r.driver}</dd></div>
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
                        <div class="insp-cond"><span>🪑 Nội thất</span><strong>${r.interior}</strong></div>
                        <div class="insp-cond"><span>🛞 Lốp xe</span><strong>${r.tires}</strong></div>
                        <div class="insp-cond"><span>🔧 Kỹ thuật</span><strong>${r.technical}</strong></div>
                    </div>
                </div>
                <div class="mk-block">
                    <h3>Danh sách kiểm tra</h3>
                    ${checks(r)}
                </div>
                ${issueBox(r)}
                ${compare(r)}
                <div class="mk-block">
                    <h3>Ghi chú kiểm tra</h3>
                    <div class="insp-note-box">${r.notes}</div>
                    <button type="button" class="mk-btn mk-btn-ghost" data-edit="${r.id}" style="margin-top:0.5rem">Chỉnh sửa</button>
                </div>
                <div class="mk-block">
                    <h3>Hình ảnh kiểm tra</h3>
                    <div class="insp-gallery">${photoBtns(r)}</div>
                </div>
                <div class="insp-actions">
                    <button type="button" class="mk-btn mk-btn-primary" data-edit="${r.id}">Chỉnh sửa biên bản</button>
                    <button type="button" class="mk-btn mk-btn-ghost" data-print="${r.id}">In biên bản</button>
                </div>
                <p class="mk-note">${DEMO}</p>
            </div>`;
    }

    function openLightbox(id, idx) {
        const r = records.find((x) => x.id === Number(id));
        if (!r) return;
        const label = r.photos[idx] || PHOTOS[idx] || "Hình ảnh";
        els.lightbox.hidden = false;
        els.lightboxBody.innerHTML = `
            <div class="insp-lightbox-stage insp-photo p${(Number(idx) % 6) + 1}">📷</div>
            <h3 class="h6">${label}</h3>
            <p class="mk-muted mb-0">${r.vehicle} · ${r.plate} · ${r.booking}<br>Chế độ mô phỏng — không phải ảnh thật.</p>`;
    }

    function partOpts(val) {
        return ["Tốt", "Vấn đề nhỏ", "Cần kiểm tra", "Vấn đề nghiêm trọng"].map((x) => `<option ${val === x ? "selected" : ""}>${x}</option>`).join("");
    }

    function formHtml(r) {
        const vOpts = VEHICLES.map(([n]) => `<option ${r && r.vehicle === n ? "selected" : ""}>${n}</option>`).join("");
        const condOpts = Object.entries(COND).map(([k, v]) => `<option value="${k}" ${r && r.condition === k ? "selected" : ""}>${v.text}</option>`).join("");
        return `
            <h2>${r ? "Chỉnh sửa biên bản" : "Tạo biên bản kiểm xe"}</h2>
            <form id="inspForm" class="op-form-grid" data-id="${r ? r.id : ""}">
                <label class="mk-field"><span>Mã đơn</span><input name="booking" value="${r ? r.booking : "BK202601"}" required /></label>
                <label class="mk-field"><span>Xe</span><select name="vehicle">${vOpts}</select></label>
                <label class="mk-field"><span>Loại kiểm tra</span>
                    <select name="type">
                        <option value="checkout" ${!r || r.type === "checkout" ? "selected" : ""}>Giao xe</option>
                        <option value="checkin" ${r && r.type === "checkin" ? "selected" : ""}>Nhận xe</option>
                    </select>
                </label>
                <label class="mk-field"><span>Số km</span><input name="km" type="number" min="0" value="${r ? r.km : 28450}" required /></label>
                <label class="mk-field"><span>Nhiên liệu (%)</span><input name="fuel" type="number" min="0" max="100" value="${r ? r.fuel : 80}" required /></label>
                <label class="mk-field"><span>Tình trạng</span><select name="condition">${condOpts}</select></label>
                <label class="mk-field"><span>Ngoại thất</span><select name="exterior">${partOpts(r ? r.exterior : "Tốt")}</select></label>
                <label class="mk-field"><span>Nội thất</span><select name="interior">${partOpts(r ? r.interior : "Tốt")}</select></label>
                <label class="mk-field" style="grid-column:1/-1"><span>Tình trạng kỹ thuật</span><select name="technical">${partOpts(r ? r.technical : "Tốt")}</select></label>
                <label class="mk-field" style="grid-column:1/-1"><span>Ghi chú</span><textarea name="notes" rows="3">${r ? r.notes : ""}</textarea></label>
                <div class="insp-actions" style="grid-column:1/-1">
                    <button type="button" class="mk-btn mk-btn-ghost" data-close-modal>Hủy</button>
                    <button type="submit" class="mk-btn mk-btn-primary">${r ? "Lưu thay đổi" : "Lưu biên bản"}</button>
                </div>
            </form>
            <p class="mk-note">${DEMO}</p>`;
    }

    function openModal(id) {
        editingId = id ? Number(id) : null;
        const r = editingId ? records.find((x) => x.id === editingId) : null;
        els.modal.hidden = false;
        els.modalBody.innerHTML = formHtml(r);
    }

    function saveForm(form) {
        const fd = new FormData(form);
        const vehicle = String(fd.get("vehicle"));
        const plate = (VEHICLES.find(([n]) => n === vehicle) || ["", "51K-000.00"])[1];
        const condition = String(fd.get("condition"));
        const payload = {
            booking: String(fd.get("booking") || "BK2026").toUpperCase(),
            vehicle, plate,
            type: String(fd.get("type")),
            km: Number(fd.get("km") || 0),
            fuel: Number(fd.get("fuel") || 0),
            condition,
            exterior: String(fd.get("exterior")),
            interior: String(fd.get("interior")),
            tires: "Tốt",
            technical: String(fd.get("technical")),
            notes: String(fd.get("notes") || "Đã lưu trên giao diện mô phỏng."),
            inspector: "Trần Thị Diệu Phối",
            customer: "Khách mô phỏng",
            driver: String(fd.get("type")) === "checkout" ? "Nguyễn Văn Hùng" : "Khách tự lái",
            mode: "Có tài xế",
            datetime: "15/09/2026 10:00",
            dateIso: "2026-09-15",
            rentFrom: "15/09/2026",
            rentTo: "17/09/2026",
            issue: condition === "good" ? null : { title: "Vấn đề được ghi nhận", level: COND[condition].text, note: "Lưu từ biểu mẫu mô phỏng." }
        };
        if (editingId) {
            const i = records.findIndex((x) => x.id === editingId);
            const prev = records[i];
            records[i] = finish({
                ...prev,
                ...payload,
                id: editingId,
                inspector: prev.inspector,
                customer: prev.customer,
                driver: prev.driver,
                mode: prev.mode,
                datetime: prev.datetime,
                dateIso: prev.dateIso,
                rentFrom: prev.rentFrom,
                rentTo: prev.rentTo,
                kmOut: prev.kmOut,
                fuelOut: prev.fuelOut,
                checkoutRef: prev.checkoutRef
            });
            toast("Đã cập nhật biên bản mô phỏng.");
            openDrawer(editingId);
        } else {
            const row = finish({ ...payload, id: nextId++ });
            if (row.type === "checkin") {
                row.kmOut = Math.max(0, row.km - 380);
                row.fuelOut = Math.min(100, row.fuel + 15);
                row.checkoutRef = { km: row.kmOut, fuel: row.fuelOut, exterior: "Tốt", technical: "Tốt" };
            }
            records.unshift(row);
            page = 1;
            toast("Đã tạo biên bản mô phỏng.");
        }
        els.modal.hidden = true;
        fillVehicles();
        render();
    }

    function printRecord(id) {
        const r = records.find((x) => x.id === Number(id));
        if (!r) return;
        els.print.hidden = false;
        els.print.innerHTML = `
            <h1>Biên bản kiểm xe</h1>
            <p>${r.booking} · ${TYPE[r.type]} · ${r.datetime}</p>
            <p>${r.vehicle} · ${r.plate}<br>Khách hàng: ${r.customer}<br>Người kiểm tra: ${r.inspector}</p>
            <p>Số km: ${kmText(r.km)} · Nhiên liệu: ${r.fuel}% · Tình trạng: ${COND[r.condition].text}</p>
            <p>Ngoại thất: ${r.exterior} · Nội thất: ${r.interior} · Lốp: ${r.tires} · Kỹ thuật: ${r.technical}</p>
            <p>${r.notes.replace(/\n/g, "<br>")}</p>
            <p>Giao diện mô phỏng — chưa kết nối hệ thống.</p>`;
        window.print();
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
        toast(DEMO);
    });
    els.reset.addEventListener("click", () => {
        filters.q = filters.type = filters.vehicle = filters.condition = filters.from = filters.to = "";
        page = 1;
        setTimeout(() => { fillVehicles(); render(); }, 0);
    });
    document.getElementById("inspCreateBtn").addEventListener("click", () => openModal());

    document.body.addEventListener("click", (e) => {
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
            toast(DEMO);
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
            openDrawer(open.dataset.open);
            toast(DEMO);
            return;
        }
        const edit = e.target.closest("[data-edit]");
        if (edit) {
            openModal(edit.dataset.edit);
            return;
        }
        const pr = e.target.closest("[data-print]");
        if (pr) {
            printRecord(pr.dataset.print);
            toast("Đang mở bản in mô phỏng.");
            return;
        }
        const photo = e.target.closest("[data-photo]");
        if (photo) {
            openLightbox(photo.dataset.id, photo.dataset.photo);
            toast(DEMO);
        }
    });

    document.body.addEventListener("submit", (e) => {
        if (e.target.id !== "inspForm") return;
        e.preventDefault();
        saveForm(e.target);
    });
})();
