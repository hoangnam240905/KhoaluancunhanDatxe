(() => {
    const money = (n) => Number(n).toLocaleString("vi-VN") + " VNĐ";
    const root = document.querySelector("[data-cb-page]");
    const form = document.querySelector("[data-cb-form]");
    if (!root || !form) return;

    const quoteUrl = root.getAttribute("data-cb-quote-url") || "";
    const availabilityUrl = root.getAttribute("data-cb-availability-url") || "";
    const driversUrl = root.getAttribute("data-cb-drivers-url") || "";
    const scheduleUrl = root.getAttribute("data-cb-schedule-url") || "";
    const creatingText = root.getAttribute("data-cb-creating") || "Đang tạo đơn...";
    const loadingText = root.getAttribute("data-cb-quote-loading") || "Đang tính giá...";
    const unavailableText = root.getAttribute("data-cb-quote-unavailable") || "Không thể tính báo giá. Vui lòng kiểm tra lại thông tin.";
    const pickup = document.querySelector("[data-cb-pickup]");
    const dropoff = document.querySelector("[data-cb-dropoff]");
    const same = document.querySelector("[data-cb-same]");
    const modeInput = document.querySelector("[data-cb-rental-mode]");
    const typeInput = form.querySelector("[name='Input.VehicleTypeId']");
    const confirmedInput = document.querySelector("[data-cb-quote-confirmed]");
    const fingerprintInput = document.querySelector("[data-cb-quote-fingerprint]");
    const confirmBtn = document.querySelector("[data-cb-confirm]");
    const statusEl = document.querySelector("[data-cb-status]");
    const daysEl = document.querySelector("[data-cb-days]");
    const rentalEl = document.querySelector("[data-cb-rental]");
    const driverRow = document.querySelector("[data-cb-driver-row]");
    const driverEl = document.querySelector("[data-cb-driver]");
    const distanceRow = document.querySelector("[data-cb-distance-row]");
    const distanceEl = document.querySelector("[data-cb-distance]");
    const totalEl = document.querySelector("[data-cb-total]");
    const depositEl = document.querySelector("[data-cb-deposit-amount]");
    const remainRow = document.querySelector("[data-cb-remain-row]");
    const remainEl = document.querySelector("[data-cb-remain]");
    let quoteTimer = 0;
    let quoteSeq = 0;
    let quoteBusy = false;
    let submitLock = false;

    const currentMode = () => modeInput?.value || "WithDriver";
    const fingerprint = () => {
        const typeId = typeInput?.value || "0";
        const vehicleId = form.querySelector("[name='Input.VehicleId']")?.value || "";
        const distance = form.querySelector("[data-cb-distance-input]")?.value || "";
        return `${typeId}|${currentMode()}|${vehicleId}|${pickup?.value || ""}|${dropoff?.value || ""}|${distance}`;
    };

    const setStatus = (text, isError) => {
        if (!statusEl) return;
        statusEl.textContent = text || "";
        statusEl.style.color = isError ? "#dc2626" : "";
    };

    const field = (q, camel, pascal) => q[camel] ?? q[pascal];
    const applyQuote = (q) => {
        if (!q) return;
        const pricePerDay = field(q, "quotedPricePerDay", "QuotedPricePerDay");
        const days = field(q, "quotedDays", "QuotedDays");
        const rental = field(q, "rentalAmount", "RentalAmount");
        const driver = field(q, "driverAmount", "DriverAmount") || 0;
        const distanceAmt = field(q, "distanceAmount", "DistanceAmount") || 0;
        const total = field(q, "totalAmount", "TotalAmount");
        const deposit = field(q, "depositAmount", "DepositAmount");
        if (daysEl) daysEl.textContent = money(pricePerDay) + " × " + days + " ngày";
        if (rentalEl) rentalEl.textContent = money(rental);
        if (driverEl) driverEl.textContent = money(driver);
        if (driverRow) driverRow.hidden = !(driver > 0);
        if (distanceEl) distanceEl.textContent = money(distanceAmt);
        if (distanceRow) distanceRow.hidden = !(distanceAmt > 0);
        if (totalEl) totalEl.textContent = money(total);
        if (depositEl) depositEl.textContent = money(deposit);
        if (remainEl) remainEl.textContent = money((total || 0) - (deposit || 0));
        if (remainRow) remainRow.hidden = false;
        if (confirmedInput) confirmedInput.value = "true";
        if (fingerprintInput) fingerprintInput.value = fingerprint();
        setStatus("", false);
    };

    const clearQuote = () => {
        if (confirmedInput) confirmedInput.value = "false";
        if (fingerprintInput) fingerprintInput.value = "";
    };

    const datesValid = () => {
        const startEmpty = !pickup?.value;
        const endEmpty = !dropoff?.value;
        const orderBad = !startEmpty && !endEmpty && new Date(dropoff.value) <= new Date(pickup.value);
        pickup?.closest(".cb-field")?.classList.toggle("is-invalid", startEmpty);
        dropoff?.closest(".cb-field")?.classList.toggle("is-invalid", endEmpty || orderBad);
        const endError = dropoff?.closest(".cb-field")?.querySelector("[data-cb-end-error]");
        if (endError) {
            endError.textContent = endEmpty
                ? "Vui lòng chọn ngày trả xe."
                : "Ngày trả xe phải sau ngày nhận xe.";
        }
        return !startEmpty && !endEmpty && !orderBad;
    };

    const driverSection = document.querySelector("[data-cb-drivers]");
    const driverList = document.querySelector("[data-cb-driver-list]");
    const driverInput = document.querySelector("[data-cb-driver-id]");
    const driverError = document.querySelector("[data-cb-driver-error]");
    const driverPicked = document.querySelector("[data-cb-driver-picked]");
    const selfNote = document.querySelector("[data-cb-self-note]");
    const modal = document.querySelector("[data-cb-modal]");
    const modalTitle = document.querySelector("[data-cb-modal-title]");
    const modalBody = document.querySelector("[data-cb-modal-body]");
    let driverCache = [];
    let driverSeq = 0;

    const initials = (name) => {
        const parts = (name || "").trim().split(/\s+/).filter(Boolean);
        const letters = parts.slice(-2).map((p) => p[0]).join("");
        return (letters || "TX").toUpperCase();
    };
    const pad = (n) => String(n).padStart(2, "0");
    const fmtWhen = (value) => {
        const d = value instanceof Date ? value : new Date(value);
        if (Number.isNaN(d.getTime())) return String(value);
        return `${pad(d.getDate())}/${pad(d.getMonth() + 1)}/${d.getFullYear()} ${pad(d.getHours())}:${pad(d.getMinutes())}`;
    };
    const esc = (value) => String(value ?? "")
        .replace(/&/g, "&amp;")
        .replace(/</g, "&lt;")
        .replace(/>/g, "&gt;")
        .replace(/"/g, "&quot;");
    const overlaps = (aStart, aEnd, bStart, bEnd) => aStart < bEnd && aEnd > bStart;
    const showDriverError = (text) => {
        if (!driverError) return;
        driverError.textContent = text || "";
        driverError.classList.toggle("is-on", Boolean(text));
    };
    const closeModal = () => { if (modal) modal.hidden = true; };
    const openModal = (title, html) => {
        if (!modal) return;
        if (modalTitle) modalTitle.textContent = title;
        if (modalBody) modalBody.innerHTML = html;
        modal.hidden = false;
    };
    const syncDriverVisibility = () => {
        const withDriver = currentMode() === "WithDriver";
        if (driverSection) driverSection.hidden = !withDriver;
        if (selfNote) selfNote.hidden = withDriver;
        if (!withDriver && driverInput) driverInput.value = "";
        if (!withDriver) syncPickedLabel();
    };
    const syncPickedLabel = () => {
        if (!driverPicked) return;
        const selected = driverInput?.value || "";
        if (currentMode() !== "WithDriver" || !selected) {
            driverPicked.hidden = true;
            driverPicked.textContent = "";
            return;
        }
        const row = driverCache.find((d) => String(field(d, "driverId", "DriverId")) === selected);
        const name = row ? (field(row, "fullName", "FullName") || "") : "";
        driverPicked.hidden = false;
        driverPicked.textContent = name ? `Đã chọn: ${name}` : `Đã chọn tài xế #${selected}`;
    };
    const selectionState = (d, selected) => {
        const id = String(field(d, "driverId", "DriverId"));
        const canSelect = field(d, "canSelect", "CanSelect");
        const available = canSelect !== false
            && (field(d, "availabilityStatus", "AvailabilityStatus") || "").indexOf("Không") < 0;
        if (id === selected) return { key: "selected", label: "Đã chọn" };
        if (!available) return { key: "unavailable", label: "Không khả dụng" };
        return { key: "selectable", label: "Có thể chọn" };
    };
    const buildSegments = (windowStart, windowEnd, busyRows) => {
        const clipped = busyRows
            .map((b) => {
                const start = new Date(field(b, "startDate", "StartDate"));
                const end = new Date(field(b, "endDate", "EndDate"));
                if (Number.isNaN(start.getTime()) || Number.isNaN(end.getTime())) return null;
                if (!overlaps(start, end, windowStart, windowEnd)) return null;
                return {
                    start: new Date(Math.max(start.getTime(), windowStart.getTime())),
                    end: new Date(Math.min(end.getTime(), windowEnd.getTime()))
                };
            })
            .filter(Boolean)
            .sort((a, b) => a.start - b.start);

        const merged = [];
        for (const row of clipped) {
            const last = merged[merged.length - 1];
            if (last && row.start <= last.end) {
                if (row.end > last.end) last.end = row.end;
            } else {
                merged.push({ start: row.start, end: row.end });
            }
        }

        const segments = [];
        let cursor = windowStart;
        for (const busy of merged) {
            if (busy.start > cursor) {
                segments.push({ start: cursor, end: busy.start, busy: false });
            }
            segments.push({ start: busy.start, end: busy.end, busy: true });
            cursor = busy.end;
        }
        if (cursor < windowEnd) {
            segments.push({ start: cursor, end: windowEnd, busy: false });
        }
        return { segments, hasBusy: merged.length > 0 };
    };
    const renderDrivers = (rows) => {
        driverCache = rows;
        if (!driverList) return;
        const selected = driverInput?.value || "";
        if (selected) {
            const current = rows.find((d) => String(field(d, "driverId", "DriverId")) === selected);
            const canKeep = current && selectionState(current, selected).key !== "unavailable";
            if (!canKeep) {
                if (driverInput) driverInput.value = "";
                showDriverError("Tài xế không còn khả dụng. Vui lòng chọn tài xế khác.");
            }
        }
        syncPickedLabel();
        if (rows.length === 0) {
            driverList.innerHTML = `<p class="cb-note">Không có tài xế phù hợp trong khoảng thời gian này.</p>`;
            return;
        }
        driverList.innerHTML = rows.map((d) => {
            const id = field(d, "driverId", "DriverId");
            const name = field(d, "fullName", "FullName") || "";
            const phone = field(d, "phone", "Phone") || "—";
            const rating = field(d, "averageRating", "AverageRating");
            const trips = field(d, "totalTrips", "TotalTrips");
            const status = field(d, "status", "Status") || "—";
            const experience = field(d, "experienceYears", "ExperienceYears");
            const expText = experience == null || experience === "" ? "—" : `${experience} năm`;
            const state = selectionState(d, selected);
            const pickDisabled = state.key === "unavailable" ? " disabled" : "";
            return `<article class="cb-driver is-${state.key}" data-driver-card="${id}">
                <span class="cb-driver-avatar">${esc(initials(name))}</span>
                <div>
                    <div class="cb-driver-top">
                        <strong>${esc(name)}</strong>
                        <span class="cb-driver-badge is-${state.key}">${state.label}</span>
                    </div>
                    <p>Điện thoại ${esc(phone)}</p>
                    <p>Đánh giá ${esc(rating)} · ${esc(trips)} chuyến</p>
                    <p>Trạng thái ${esc(status)} · Kinh nghiệm ${esc(expText)}</p>
                    <div class="cb-driver-actions">
                        <button type="button" data-driver-schedule="${id}">Xem lịch</button>
                        <button type="button" data-driver-pick="${id}"${pickDisabled}>${state.key === "selected" ? "Đã chọn" : "Chọn tài xế"}</button>
                    </div>
                </div>
            </article>`;
        }).join("");
    };
    const loadDrivers = async () => {
        syncDriverVisibility();
        if (currentMode() !== "WithDriver" || !driversUrl || !driverList) return;
        if (!datesValid()) {
            driverList.innerHTML = `<p class="cb-note">Chọn thời gian thuê để xem tài xế.</p>`;
            syncPickedLabel();
            return;
        }
        const seq = ++driverSeq;
        const vehicleId = vehicleInput?.value || "";
        const typeId = typeInput?.value || "";
        const qs = new URLSearchParams({
            startDate: pickup.value,
            endDate: dropoff.value
        });
        if (vehicleId) qs.set("vehicleId", vehicleId);
        if (typeId) qs.set("vehicleTypeId", typeId);
        driverList.innerHTML = `<p class="cb-note">Đang tải tài xế...</p>`;
        try {
            const res = await fetch(`${driversUrl}&${qs.toString()}`, { headers: { Accept: "application/json" } });
            const body = await res.json().catch(() => null);
            if (seq !== driverSeq) return;
            if (!res.ok) {
                driverList.innerHTML = `<p class="cb-note">${esc(body?.message || "Không tải được danh sách tài xế.")}</p>`;
                return;
            }
            renderDrivers(Array.isArray(body) ? body : []);
        } catch {
            if (seq !== driverSeq) return;
            driverList.innerHTML = `<p class="cb-note">Không tải được danh sách tài xế.</p>`;
        }
    };

    const syncDropoff = () => {
        const a = document.querySelector("[data-cb-pickup-place]");
        const b = document.querySelector("[data-cb-return-place]");
        if (same?.checked && a && b) b.value = a.value;
    };

    const fetchQuote = async () => {
        if (!quoteUrl || !datesValid()) {
            clearQuote();
            return false;
        }
        const seq = ++quoteSeq;
        quoteBusy = true;
        setStatus(loadingText, false);
        const url = new URL(quoteUrl, window.location.origin);
        url.searchParams.set("vehicleTypeId", typeInput?.value || "0");
        url.searchParams.set("startDate", pickup.value);
        url.searchParams.set("endDate", dropoff.value);
        url.searchParams.set("rentalMode", currentMode());
        const distance = form.querySelector("[data-cb-distance-input]")?.value;
        if (distance) url.searchParams.set("estimatedDistance", distance);
        try {
            const res = await fetch(url.toString(), { headers: { Accept: "application/json" } });
            if (seq !== quoteSeq) return false;
            if (!res.ok) {
                let message = unavailableText;
                try {
                    const body = await res.json();
                    if (body?.message) message = body.message;
                } catch { /* ignore */ }
                clearQuote();
                setStatus(message, true);
                return false;
            }
            const data = await res.json();
            applyQuote(data);
            return true;
        } catch {
            if (seq !== quoteSeq) return false;
            clearQuote();
            setStatus(unavailableText, true);
            return false;
        } finally {
            if (seq === quoteSeq) quoteBusy = false;
        }
    };

    const scheduleQuote = () => {
        clearQuote();
        window.clearTimeout(quoteTimer);
        quoteTimer = window.setTimeout(() => { fetchQuote(); }, 400);
    };

    document.querySelectorAll("[data-cb-mode]").forEach((btn) => {
        btn.addEventListener("click", () => {
            document.querySelectorAll("[data-cb-mode]").forEach((el) => el.classList.remove("is-on"));
            btn.classList.add("is-on");
            const mode = btn.getAttribute("data-cb-mode") || "WithDriver";
            if (modeInput) modeInput.value = mode;
            syncDriverVisibility();
            scheduleQuote();
            loadDrivers();
        });
    });

    document.querySelectorAll("[data-cb-addon]").forEach((box) => {
        box.addEventListener("change", () => {
            box.closest(".cb-addon")?.classList.toggle("is-on", box.checked);
        });
    });

    same?.addEventListener("change", () => {
        const ret = document.querySelector("[data-cb-return-place]");
        if (!ret) return;
        if (same.checked) {
            ret.value = document.querySelector("[data-cb-pickup-place]")?.value || "";
            ret.setAttribute("readonly", "readonly");
        } else {
            ret.removeAttribute("readonly");
        }
    });
    if (same?.checked) {
        document.querySelector("[data-cb-return-place]")?.setAttribute("readonly", "readonly");
        syncDropoff();
    }

    document.querySelector("[data-cb-pickup-place]")?.addEventListener("input", () => {
        if (same?.checked) syncDropoff();
    });

    document.querySelector("[data-cb-swap]")?.addEventListener("click", () => {
        const a = document.querySelector("[data-cb-pickup-place]");
        const b = document.querySelector("[data-cb-return-place]");
        if (!a || !b || b.hasAttribute("readonly")) return;
        const tmp = a.value;
        a.value = b.value;
        b.value = tmp;
    });

    pickup?.addEventListener("change", () => { scheduleQuote(); loadDrivers(); });
    dropoff?.addEventListener("change", () => { scheduleQuote(); loadDrivers(); });

    driverList?.addEventListener("click", async (e) => {
        const schedule = e.target.closest("[data-driver-schedule]");
        const pick = e.target.closest("[data-driver-pick]");
        const id = (schedule || pick)?.getAttribute(schedule ? "data-driver-schedule" : "data-driver-pick");
        if (!id) return;
        const row = driverCache.find((d) => String(field(d, "driverId", "DriverId")) === id);
        if (pick) {
            if (pick.disabled) {
                showDriverError("Tài xế đang có chuyến trong khoảng thời gian này.");
                return;
            }
            if (driverInput) driverInput.value = id;
            showDriverError("");
            renderDrivers(driverCache);
            return;
        }
        if (!schedule || !scheduleUrl || !datesValid()) return;
        const requestedStart = new Date(pickup.value);
        const requestedEnd = new Date(dropoff.value);
        const qs = new URLSearchParams({
            driverId: id,
            startDate: pickup.value,
            endDate: dropoff.value
        });
        openModal("Lịch tài xế", `<p class="cb-note">Đang tải lịch...</p>`);
        try {
            const res = await fetch(`${scheduleUrl}&${qs.toString()}`, { headers: { Accept: "application/json" } });
            const body = await res.json().catch(() => null);
            if (!res.ok || !body) {
                openModal("Lịch tài xế", `<p>${esc(body?.message || "Không tải được lịch tài xế.")}</p>`);
                return;
            }
            const name = field(body, "fullName", "FullName") || (row ? field(row, "fullName", "FullName") : "");
            const busy = field(body, "busy", "Busy") || [];
            const { segments, hasBusy } = buildSegments(requestedStart, requestedEnd, busy);
            const conflict = hasBusy;
            const windowVerdict = conflict
                ? `<div class="cb-schedule-verdict is-bad">🔴 Tài xế đang bận trong khoảng thời gian bạn chọn.</div>`
                : `<div class="cb-schedule-verdict is-ok">🟢 Tài xế không bận trong khoảng thời gian bạn chọn.</div>`;
            const items = segments.length === 0 || (!hasBusy && segments.length === 1)
                ? `<li class="is-free"><span>${esc(fmtWhen(requestedStart))} → ${esc(fmtWhen(requestedEnd))}</span><em>🟢 Không có chuyến trong khoảng thời gian này. Tài xế không bận.</em></li>`
                : segments.map((seg) => `<li class="${seg.busy ? "is-busy" : "is-free"}">
                        <span>${esc(fmtWhen(seg.start))} → ${esc(fmtWhen(seg.end))}</span>
                        <em>${seg.busy ? "🔴 Đang bận" : "🟢 Không bận"}</em>
                    </li>`).join("");
            openModal(`Lịch tài xế — ${name}`, `
                <div class="cb-schedule-window">
                    <strong>Khoảng thời gian bạn chọn</strong>
                    <p>${esc(fmtWhen(requestedStart))} → ${esc(fmtWhen(requestedEnd))}</p>
                    ${windowVerdict}
                </div>
                <h4 class="cb-schedule-title">Lịch</h4>
                <ul class="cb-schedule">${items}</ul>`);
        } catch {
            openModal("Lịch tài xế", `<p>Không tải được lịch tài xế.</p>`);
        }
    });
    modal?.querySelector("[data-cb-modal-close]")?.addEventListener("click", closeModal);
    modal?.addEventListener("click", (e) => { if (e.target === modal) closeModal(); });

    form.addEventListener("submit", (e) => {
        if (submitLock) {
            e.preventDefault();
            return;
        }
        if (!datesValid()) {
            e.preventDefault();
            return;
        }
        if (currentMode() === "WithDriver" && driverInput?.value) {
            const row = driverCache.find((d) => String(field(d, "driverId", "DriverId")) === driverInput.value);
            if (row && selectionState(row, driverInput.value).key === "unavailable") {
                e.preventDefault();
                showDriverError("Tài xế đang có chuyến trong khoảng thời gian này.");
                return;
            }
        }
        if (quoteBusy) {
            e.preventDefault();
            setStatus(loadingText, false);
            return;
        }
        syncDropoff();
        submitLock = true;
        if (confirmBtn) {
            confirmBtn.disabled = true;
            confirmBtn.textContent = creatingText;
        }
    });

    if (!confirmedInput?.value || confirmedInput.value.toLowerCase() !== "true") {
        fetchQuote();
    }
    loadDrivers();
})();
