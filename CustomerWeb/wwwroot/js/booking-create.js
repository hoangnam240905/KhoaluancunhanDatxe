(() => {
    const money = (n) => Number(n).toLocaleString("vi-VN") + " VNĐ";
    const root = document.querySelector("[data-cb-page]");
    const form = document.querySelector("[data-cb-form]");
    if (!root || !form) return;

    const quoteUrl = root.getAttribute("data-cb-quote-url") || "";
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
    const fmtWhen = (value) => {
        const d = new Date(value);
        if (Number.isNaN(d.getTime())) return value;
        const pad = (n) => String(n).padStart(2, "0");
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
        const vehicleId = form.querySelector("[name='Input.VehicleId']")?.value || "";
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
        const from = new Date(requestedStart);
        const to = new Date(requestedEnd);
        from.setDate(from.getDate() - 7);
        to.setDate(to.getDate() + 14);
        const pad = (n) => String(n).padStart(2, "0");
        const iso = (d) => `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
        const qs = new URLSearchParams({ driverId: id, startDate: iso(from), endDate: iso(to) });
        openModal("Lịch tài xế", `<p class="cb-note">Đang tải lịch...</p>`);
        try {
            const res = await fetch(`${scheduleUrl}&${qs.toString()}`, { headers: { Accept: "application/json" } });
            const body = await res.json().catch(() => null);
            if (!res.ok || !body) {
                openModal("Lịch tài xế", `<p>${esc(body?.message || "Không tải được lịch tài xế.")}</p>`);
                return;
            }
            const name = field(body, "fullName", "FullName") || (row ? field(row, "fullName", "FullName") : "");
            const rating = field(body, "averageRating", "AverageRating")
                ?? (row ? field(row, "averageRating", "AverageRating") : "—");
            const trips = field(body, "totalTrips", "TotalTrips")
                ?? (row ? field(row, "totalTrips", "TotalTrips") : "—");
            const busy = field(body, "busy", "Busy") || [];
            const conflict = busy.some((b) => overlaps(
                new Date(field(b, "startDate", "StartDate")),
                new Date(field(b, "endDate", "EndDate")),
                requestedStart,
                requestedEnd));
            const windowVerdict = conflict
                ? `<div class="cb-schedule-verdict is-bad">✗ Đã có chuyến → Không thể chọn</div>`
                : `<div class="cb-schedule-verdict is-ok">✓ Không trùng lịch → Có thể chọn</div>`;
            const items = busy.length
                ? busy.map((b) => {
                    const bStart = new Date(field(b, "startDate", "StartDate"));
                    const bEnd = new Date(field(b, "endDate", "EndDate"));
                    const hit = overlaps(bStart, bEnd, requestedStart, requestedEnd);
                    const bookingId = field(b, "bookingId", "BookingId");
                    const status = field(b, "status", "Status") || field(b, "label", "Label") || "—";
                    return `<li class="${hit ? "is-hit" : ""}">
                        <strong>#${esc(bookingId ?? "—")}</strong>
                        <span>${esc(fmtWhen(bStart))} → ${esc(fmtWhen(bEnd))}</span>
                        <span>Trạng thái ${esc(status)}</span>
                        <em>${hit ? "Trùng khung bạn chọn" : "Ngoài khung bạn chọn"}</em>
                    </li>`;
                }).join("")
                : `<li>Không có chuyến trong khoảng đang xem.</li>`;
            openModal(`Lịch tài xế · ${name}`, `
                <div class="cb-schedule-head">
                    <p><strong>${esc(name)}</strong></p>
                    <p>Đánh giá ${esc(rating)} · Tổng số chuyến ${esc(trips)}</p>
                </div>
                <div class="cb-schedule-window">
                    <strong>Khung bạn đang chọn</strong>
                    <p>${esc(fmtWhen(requestedStart))} → ${esc(fmtWhen(requestedEnd))}</p>
                    ${windowVerdict}
                </div>
                <div class="cb-schedule-timeline" aria-hidden="true">
                    <span class="is-request">Bạn chọn</span>
                    <span class="${conflict ? "is-busy" : "is-free"}">${conflict ? "Trùng lịch" : "Trống"}</span>
                </div>
                <h4 class="cb-schedule-title">Lịch đã có</h4>
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
