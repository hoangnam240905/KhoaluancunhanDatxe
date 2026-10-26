// DriveX chat widget: one question → one short answer from /Chatbot?handler=Ask (vehicle search or FAQ).
(function () {
    const root = document.getElementById("dxChat");
    if (!root) return;
    const askUrl = root.dataset.askUrl;
    const panel = root.querySelector(".cw-panel");
    const log = root.querySelector("[data-cw-log]");
    const form = root.querySelector("[data-cw-form]");
    const input = form.querySelector("input[name=message]");
    const fab = root.querySelector(".cw-fab");
    let busy = false;

    const esc = (s) => String(s ?? "").replace(/[&<>"']/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]));
    // Only same-site paths from the server are turned into links.
    const safeUrl = (u) => (typeof u === "string" && u.startsWith("/") && !u.startsWith("//")) ? u : null;

    function toggle(open) {
        const show = open ?? panel.hidden;
        panel.hidden = !show;
        fab.setAttribute("aria-expanded", show ? "true" : "false");
        root.classList.toggle("is-open", show);
        if (show) setTimeout(() => input.focus(), 0);
    }

    function add(html, cls) {
        const div = document.createElement("div");
        div.className = "cw-msg " + cls;
        div.innerHTML = html;
        log.appendChild(div);
        log.scrollTop = log.scrollHeight;
        return div;
    }

    function renderAnswer(data) {
        let html = `<p>${esc(data.reply)}</p>`;
        if (Array.isArray(data.vehicles) && data.vehicles.length) {
            html += `<ul class="cw-cars">` + data.vehicles.map((v) => {
                const book = safeUrl(v.bookUrl);
                const action = !book ? ""
                    : data.loggedInCustomer
                        ? `<a class="cw-book" href="${esc(book)}">Đặt xe</a>`
                        : `<a class="cw-book is-ghost" href="/Account/Login">Đăng nhập để đặt</a>`;
                return `<li>
                    <div><strong>${esc(v.name)}</strong><small>${esc(v.typeName)} · ${esc(v.licensePlate)} · ${esc(v.days)} ngày</small></div>
                    <div class="cw-price">${esc(v.totalText)}${action}</div>
                </li>`;
            }).join("") + `</ul>`;
        }
        if (Array.isArray(data.links) && data.links.length) {
            html += `<div class="cw-links">` + data.links
                .map((l) => safeUrl(l.url) ? `<a href="${esc(l.url)}">${esc(l.text)} →</a>` : "")
                .join("") + `</div>`;
        }
        add(html, "is-bot");
    }

    async function ask(text) {
        if (busy || !text) return;
        busy = true;
        add(esc(text), "is-me");
        const typing = add(`<span class="cw-typing"><i></i><i></i><i></i></span>`, "is-bot");
        try {
            const body = new URLSearchParams({ message: text });
            const res = await fetch(askUrl, { method: "POST", credentials: "same-origin", body });
            const data = await res.json().catch(() => null);
            typing.remove();
            if (!res.ok || !data || !data.ok) add(esc((data && data.error) || "Trợ lý tạm thời không phản hồi, bạn thử lại sau nhé."), "is-bot is-error");
            else renderAnswer(data);
        } catch (_) {
            typing.remove();
            add("Không kết nối được, bạn thử lại sau nhé.", "is-bot is-error");
        } finally {
            busy = false;
        }
    }

    root.addEventListener("click", (e) => {
        if (e.target.closest("[data-cw-toggle]")) { toggle(); return; }
        const sample = e.target.closest("[data-cw-sample]");
        if (sample) ask(sample.dataset.cwSample);
    });
    form.addEventListener("submit", (e) => {
        e.preventDefault();
        const text = input.value.trim();
        input.value = "";
        ask(text);
    });
    document.addEventListener("keydown", (e) => { if (e.key === "Escape" && !panel.hidden) toggle(false); });
})();
