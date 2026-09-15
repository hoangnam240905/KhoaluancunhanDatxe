(() => {
    const root = document.querySelector("[data-rv-page]");
    if (!root) return;

    const labels = {
        1: "Rất không hài lòng",
        2: "Không hài lòng",
        3: "Bình thường",
        4: "Hài lòng",
        5: "Rất hài lòng"
    };

    let rating = 0;
    const stars = [...root.querySelectorAll("[data-rv-star]")];
    const comment = root.querySelector("[data-rv-comment]");
    const count = root.querySelector("[data-rv-count]");
    const hint = root.querySelector("[data-rv-hint]");
    const error = root.querySelector("[data-rv-error]");
    const score = root.querySelector("[data-rv-score]");
    const previewScore = root.querySelector("[data-rv-preview-score]");
    const previewLabel = root.querySelector("[data-rv-preview-label]");
    const submitLabels = root.querySelectorAll("[data-rv-submit-label]");

    const paint = (value, cls) => {
        stars.forEach((btn) => {
            const n = Number(btn.getAttribute("data-rv-star"));
            btn.classList.toggle(cls, n <= value);
        });
    };

    const required = () => rating >= 1 && rating <= 3;

    const syncHint = () => {
        if (!hint) return;
        if (!rating) {
            hint.textContent = "Chọn số sao để tiếp tục.";
            hint.classList.remove("is-need");
            comment?.classList.remove("is-need");
            return;
        }
        if (required()) {
            hint.textContent = "Với mức đánh giá này, vui lòng chia sẻ thêm nhận xét.";
            hint.classList.add("is-need");
            comment?.classList.add("is-need");
        } else {
            hint.textContent = "Nhận xét là tùy chọn.";
            hint.classList.remove("is-need");
            comment?.classList.remove("is-need");
        }
    };

    const syncScore = (value) => {
        const label = labels[value] || "Chưa chọn số sao";
        if (score) score.innerHTML = value ? `<span>${value} / 5</span> · ${label}` : "Chưa chọn số sao";
        if (previewScore) previewScore.textContent = value ? `${value} / 5` : "—";
        if (previewLabel) previewLabel.textContent = label;
        submitLabels.forEach((el) => {
            el.textContent = value ? `Đánh giá ${value} sao` : "Gửi đánh giá";
        });
    };

    const syncCount = () => {
        const n = (comment?.value || "").length;
        if (count) count.textContent = `${n} / 500`;
    };

    const show = (name) => {
        root.setAttribute("data-rv-view", name);
        root.querySelector("[data-rv-form]")?.classList.toggle("is-off", name !== "form");
        root.querySelectorAll("[data-rv-state]").forEach((el) => {
            el.classList.toggle("is-on", el.getAttribute("data-rv-state") === name);
        });
    };

    const showError = (text) => {
        if (!error) return;
        error.textContent = text;
        error.classList.toggle("is-on", Boolean(text));
    };

    const setRating = (value) => {
        rating = value;
        paint(value, "is-on");
        paint(0, "is-preview");
        syncHint();
        syncScore(value);
        showError("");
    };

    stars.forEach((btn) => {
        const value = Number(btn.getAttribute("data-rv-star"));
        btn.addEventListener("mouseenter", () => paint(value, "is-preview"));
        btn.addEventListener("click", () => setRating(value));
    });
    root.querySelector("[data-rv-stars]")?.addEventListener("mouseleave", () => {
        paint(0, "is-preview");
        paint(rating, "is-on");
    });

    comment?.addEventListener("input", () => {
        if ((comment.value || "").length > 500) comment.value = comment.value.slice(0, 500);
        syncCount();
        showError("");
    });

    root.querySelectorAll("[data-rv-submit]").forEach((btn) => {
        btn.addEventListener("click", () => {
            const text = (comment?.value || "").trim();
            if (!rating) {
                showError("Vui lòng chọn số sao đánh giá.");
                return;
            }
            if (required() && !text) {
                showError("Vui lòng nhập nhận xét cho mức đánh giá này.");
                return;
            }
            if ((comment?.value || "").length > 500) {
                showError("Nhận xét không được vượt quá 500 ký tự.");
                return;
            }
            showError("");
            const doneScore = root.querySelector("[data-rv-success-score]");
            const doneLabel = root.querySelector("[data-rv-success-label]");
            if (doneScore) doneScore.textContent = `${rating} / 5`;
            if (doneLabel) doneLabel.textContent = labels[rating];
            show("processing");
            window.setTimeout(() => show("success"), 1400);
        });
    });

    syncHint();
    syncScore(0);
    syncCount();
    show(root.getAttribute("data-rv-view") || "form");
})();
