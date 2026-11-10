// Heart button on car cards: any [data-fav-vehicle] button, on every customer page.
// Guests are sent to the login page; customers' favourites are saved on the server.
(function () {
    const me = document.currentScript;
    const isCustomer = me.dataset.customer === "1";
    const idsUrl = me.dataset.idsUrl;
    const toggleUrl = me.dataset.toggleUrl;
    const favorites = new Set();

    function paint(btn, on) {
        btn.classList.toggle("is-on", on);
        btn.setAttribute("aria-pressed", on ? "true" : "false");
        const icon = btn.querySelector("i");
        icon?.classList.toggle("bi-heart", !on);
        icon?.classList.toggle("bi-heart-fill", on);
        const label = btn.querySelector("[data-fav-label]");
        if (label) label.textContent = on ? "Đã yêu thích" : "Thêm vào yêu thích";
    }

    // Cards rendered later (recommendations) call this again.
    function mark() {
        document.querySelectorAll("[data-fav-vehicle]").forEach((btn) => {
            paint(btn, favorites.has(Number(btn.dataset.favVehicle)));
        });
    }
    window.dxFavorites = { mark };

    async function save(id, on) {
        const body = new URLSearchParams({ vehicleId: id, on: on });
        const res = await fetch(toggleUrl, {
            method: "POST",
            credentials: "same-origin",
            headers: { "X-Requested-With": "XMLHttpRequest" },
            body
        });
        return res.ok;
    }

    document.addEventListener("click", async (e) => {
        const btn = e.target.closest("[data-fav-vehicle]");
        if (!btn) return;
        e.preventDefault();
        if (!isCustomer) {
            location.href = "/Account/Login";
            return;
        }
        if (btn.disabled) return;
        const id = Number(btn.dataset.favVehicle);
        const on = !favorites.has(id);
        btn.disabled = true;
        paint(btn, on);
        let ok = false;
        try { ok = await save(id, on); } catch (_) { /* restored below */ }
        btn.disabled = false;
        if (!ok) {
            paint(btn, !on);
            return;
        }
        if (on) favorites.add(id); else favorites.delete(id);
        mark();
        // On the favourites page an un-hearted car leaves the list.
        const card = btn.closest("[data-fav-card]");
        if (card && !on) {
            card.remove();
            if (!document.querySelector("[data-fav-card]")) {
                document.querySelector("[data-fav-empty]")?.removeAttribute("hidden");
            }
        }
    });

    if (isCustomer) {
        fetch(idsUrl, { credentials: "same-origin", headers: { Accept: "application/json" } })
            .then((r) => (r.ok ? r.json() : null))
            .then((data) => {
                (data?.ids || []).forEach((id) => favorites.add(id));
                mark();
            })
            .catch(() => { /* hearts stay empty; clicking still works */ });
    }
})();
