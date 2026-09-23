(() => {
    const money = (n) => Number(n).toLocaleString("vi-VN") + " VNĐ";
    const root = document.querySelector("[data-cb-page]");
    const form = document.querySelector("[data-cb-form]");
    if (!root || !form) return;

    const quoteUrl = root.getAttribute("data-cb-quote-url") || "";
    const availabilityUrl = root.getAttribute("data-cb-availability-url") || "";
    const creatingText = root.getAttribute("data-cb-creating") || "Đang tạo đơn...";
    const loadingText = root.getAttribute("data-cb-quote-loading") || "Đang tính giá...";
    const availabilityLoadingText = root.getAttribute("data-cb-availability-loading") || "Đang kiểm tra tình trạng xe...";
    const unavailableText = root.getAttribute("data-cb-quote-unavailable") || "Không thể tính báo giá. Vui lòng kiểm tra lại thông tin.";
    const vehicleUnavailableText = root.getAttribute("data-cb-vehicle-unavailable")
        || "Xe này hiện không khả dụng trong khoảng thời gian bạn đã chọn.";
    const pickup = document.querySelector("[data-cb-pickup]");
    const dropoff = document.querySelector("[data-cb-dropoff]");
    const same = document.querySelector("[data-cb-same]");
    const modeInput = document.querySelector("[data-cb-rental-mode]");
    const typeInput = form.querySelector("[name='Input.VehicleTypeId']");
    const vehicleInput = form.querySelector("[name='Input.VehicleId']");
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
    let availabilityOk = true;
    let submitLock = false;

    const currentMode = () => modeInput?.value || "WithDriver";
    const selectedVehicleId = () => {
        const raw = vehicleInput?.value || "";
        const id = Number(raw);
        return Number.isFinite(id) && id > 0 ? id : null;
    };
    const fingerprint = () => {
        const typeId = typeInput?.value || "0";
        const vehicleId = vehicleInput?.value || "";
        const distance = form.querySelector("[data-cb-distance-input]")?.value || "";
        return `${typeId}|${currentMode()}|${vehicleId}|${pickup?.value || ""}|${dropoff?.value || ""}|${distance}`;
    };

    const setStatus = (text, isError) => {
        if (!statusEl) return;
        statusEl.textContent = text || "";
        statusEl.style.color = isError ? "#dc2626" : "";
    };

    const setConfirmEnabled = (enabled) => {
        if (!confirmBtn || submitLock) return;
        confirmBtn.disabled = !enabled;
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
        setConfirmEnabled(availabilityOk);
    };

    const clearQuote = () => {
        if (confirmedInput) confirmedInput.value = "false";
        if (fingerprintInput) fingerprintInput.value = "";
        if (daysEl) daysEl.textContent = "—";
        if (rentalEl) rentalEl.textContent = "—";
        if (totalEl) totalEl.textContent = "—";
        if (depositEl) depositEl.textContent = "—";
        if (remainRow) remainRow.hidden = true;
        setConfirmEnabled(false);
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

    const syncDropoff = () => {
        const a = document.querySelector("[data-cb-pickup-place]");
        const b = document.querySelector("[data-cb-return-place]");
        if (same?.checked && a && b) b.value = a.value;
    };

    const checkAvailability = async () => {
        const vehicleId = selectedVehicleId();
        if (!availabilityUrl || !vehicleId || !datesValid()) {
            availabilityOk = true;
            return true;
        }

        setStatus(availabilityLoadingText, false);
        setConfirmEnabled(false);
        const url = new URL(availabilityUrl, window.location.origin);
        url.searchParams.set("vehicleId", String(vehicleId));
        url.searchParams.set("startDate", pickup.value);
        url.searchParams.set("endDate", dropoff.value);
        try {
            const res = await fetch(url.toString(), { headers: { Accept: "application/json" } });
            let body = null;
            try { body = await res.json(); } catch { /* ignore */ }
            if (!res.ok) {
                availabilityOk = false;
                setStatus((body && body.message) || vehicleUnavailableText, true);
                return false;
            }
            availabilityOk = body?.available === true;
            if (!availabilityOk) {
                setStatus((body && body.message) || vehicleUnavailableText, true);
                return false;
            }
            return true;
        } catch {
            availabilityOk = false;
            setStatus(vehicleUnavailableText, true);
            return false;
        }
    };

    const fetchQuote = async () => {
        if (!quoteUrl || !datesValid()) {
            clearQuote();
            return false;
        }
        const seq = ++quoteSeq;
        quoteBusy = true;
        setConfirmEnabled(false);

        const available = await checkAvailability();
        if (seq !== quoteSeq) return false;
        if (!available) {
            clearQuote();
            quoteBusy = false;
            return false;
        }

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
        availabilityOk = true;
        window.clearTimeout(quoteTimer);
        quoteTimer = window.setTimeout(() => { fetchQuote(); }, 400);
    };

    document.querySelectorAll("[data-cb-mode]").forEach((btn) => {
        btn.addEventListener("click", () => {
            document.querySelectorAll("[data-cb-mode]").forEach((el) => el.classList.remove("is-on"));
            btn.classList.add("is-on");
            const mode = btn.getAttribute("data-cb-mode") || "WithDriver";
            if (modeInput) modeInput.value = mode;
            document.querySelector("[data-cb-driver-note]")?.classList.toggle("is-on", mode === "WithDriver");
            scheduleQuote();
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

    pickup?.addEventListener("change", scheduleQuote);
    dropoff?.addEventListener("change", scheduleQuote);

    form.addEventListener("submit", (e) => {
        if (submitLock) {
            e.preventDefault();
            return;
        }
        if (!datesValid()) {
            e.preventDefault();
            return;
        }
        if (quoteBusy) {
            e.preventDefault();
            setStatus(loadingText, false);
            return;
        }
        if (!availabilityOk) {
            e.preventDefault();
            setStatus(vehicleUnavailableText, true);
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
    } else if (selectedVehicleId()) {
        checkAvailability().then((ok) => {
            if (!ok) clearQuote();
            else setConfirmEnabled(true);
        });
    }
})();
