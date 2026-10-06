(() => {
  const root = document.querySelector("[data-cr-page]");
  if (!root) return;

  const fetchUrl = root.getAttribute("data-cr-fetch");
  const bookBase = root.getAttribute("data-cr-book") || "/Customer/Recommendations?handler=Book";
  const searchUrl = root.getAttribute("data-cr-search") || "/Search";

  const elLoading = root.querySelector("[data-cr-loading]");
  const elError = root.querySelector("[data-cr-error]");
  const elErrorMsg = root.querySelector("[data-cr-error-msg]");
  const elEmpty = root.querySelector("[data-cr-empty]");
  const elList = root.querySelector("[data-cr-list]");
  const elSegment = root.querySelector("[data-cr-segment]");
  const elSegmentValue = root.querySelector("[data-cr-segment-value]");
  const btnRetry = root.querySelector("[data-cr-retry]");

  function show(which) {
    if (elLoading) elLoading.hidden = which !== "loading";
    if (elError) elError.hidden = which !== "error";
    if (elEmpty) elEmpty.hidden = which !== "empty";
    if (elList) elList.hidden = which !== "list";
  }

  function escapeHtml(value) {
    return String(value ?? "")
      .replace(/&/g, "&amp;")
      .replace(/</g, "&lt;")
      .replace(/>/g, "&gt;")
      .replace(/"/g, "&quot;");
  }

  function imageForSegment(segment) {
    const n = String(segment || "").toLowerCase();
    if (n.includes("limousine") || n.includes("luxury")) return "/images/home/limousine.jpg";
    if (n.includes("suv")) return "/images/home/suv.jpg";
    if (n.includes("van") || n.includes("mpv")) return "/images/home/van.jpg";
    return "/images/home/sedan.jpg";
  }

  function scoreText(score) {
    const n = Number(score);
    if (!Number.isFinite(n)) return "—";
    return n.toFixed(4);
  }

  function bookHref(vehicleId) {
    const url = new URL(bookBase, window.location.origin);
    url.searchParams.set("vehicleId", String(vehicleId));
    return url.pathname + url.search;
  }

  function renderItems(items) {
    if (!elList) return;
    elList.innerHTML = items.map((item, index) => {
      const brand = escapeHtml(item.brand);
      const model = escapeHtml(item.model);
      const segment = escapeHtml(item.segment);
      const carId = escapeHtml(item.carId);
      const vehicleId = Number(item.vehicleId) || 0;
      const img = imageForSegment(item.segment);
      const badge = index === 0 ? "Đề xuất hàng đầu" : "Gợi ý";
      const badgeClass = index === 0 ? "" : "ch-badge-alt";
      return `
        <article class="ch-card">
          <div class="ch-card-media">
            <img src="${img}" alt="${brand} ${model}" onerror="this.onerror=null;this.src='/images/home/sedan.jpg';" />
            <span class="ch-badge ${badgeClass}">${badge}</span>
          </div>
          <div class="ch-card-body">
            <h3>${brand} ${model}</h3>
            <div class="cr-meta-row">
              <span><i class="bi bi-tags"></i> ${segment}</span>
              <span class="cr-score">Điểm khớp <span>${scoreText(item.finalScore)}</span></span>
            </div>
            <div class="cr-debug">VehicleId ${vehicleId} · ${carId}</div>
            <div class="ch-price cr-card-actions">
              <a class="btn btn-primary btn-sm" href="${bookHref(vehicleId)}">Đặt xe này</a>
              <a class="btn btn-outline-primary btn-sm" href="${searchUrl}">Xem thêm xe</a>
            </div>
          </div>
        </article>`;
    }).join("");
  }

  async function load() {
    if (!fetchUrl) {
      show("error");
      if (elErrorMsg) elErrorMsg.textContent = "Thiếu địa chỉ tải gợi ý.";
      return;
    }

    show("loading");
    if (elSegment) elSegment.hidden = true;

    try {
      const res = await fetch(fetchUrl, {
        headers: { Accept: "application/json" },
        credentials: "same-origin"
      });

      if (res.status === 401 || res.status === 403) {
        window.location.href = "/Account/Login?returnUrl=" + encodeURIComponent(window.location.pathname);
        return;
      }

      let payload = null;
      try { payload = await res.json(); } catch { payload = null; }

      if (res.status === 503 || !res.ok) {
        show("error");
        if (elErrorMsg) {
          elErrorMsg.textContent =
            (payload && (payload.message || payload.Message)) ||
            "Dịch vụ gợi ý tạm thời không khả dụng. Bạn vẫn có thể tìm xe bình thường.";
        }
        return;
      }

      const predicted =
        (payload && (payload.predictedSegment || payload.PredictedSegment)) || "";
      const raw =
        (payload && (payload.recommendations || payload.Recommendations)) || [];

      const seen = new Set();
      const items = [];
      for (const row of raw) {
        const id = Number(row.vehicleId ?? row.VehicleId);
        if (!id || seen.has(id)) continue;
        seen.add(id);
        items.push({
          vehicleId: id,
          carId: row.carId ?? row.CarId ?? "",
          brand: row.brand ?? row.Brand ?? "",
          model: row.model ?? row.Model ?? "",
          segment: row.segment ?? row.Segment ?? "",
          finalScore: row.finalScore ?? row.FinalScore ?? 0
        });
        if (items.length >= 5) break;
      }

      if (elSegment && elSegmentValue) {
        if (predicted) {
          elSegmentValue.textContent = predicted;
          elSegment.hidden = false;
        } else {
          elSegment.hidden = true;
        }
      }

      if (items.length === 0) {
        show("empty");
        return;
      }

      renderItems(items);
      show("list");
    } catch {
      show("error");
      if (elErrorMsg) {
        elErrorMsg.textContent =
          "Không kết nối được dịch vụ gợi ý. Bạn vẫn có thể tìm xe bình thường.";
      }
    }
  }

  if (btnRetry) btnRetry.addEventListener("click", () => load());
  load();
})();
