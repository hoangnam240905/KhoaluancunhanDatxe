// DriveX chat widget: one question → one short answer from /Chatbot?handler=Ask (vehicle search or FAQ).
// History: a logged-in customer's chat comes back from the server (any page, any device); a guest's chat
// is kept in sessionStorage, so it survives moving between pages of the same tab.
(function () {
    const root = document.getElementById("dxChat");
    if (!root) return;
    const askUrl = root.dataset.askUrl;
    const historyUrl = root.dataset.historyUrl;
    const clearUrl = root.dataset.clearUrl;
    const isCustomer = root.dataset.customer === "1";
    const panel = root.querySelector(".cw-panel");
    const log = root.querySelector("[data-cw-log]");
    const form = root.querySelector("[data-cw-form]");
    const input = form.querySelector("input[name=message]");
    const fab = root.querySelector(".cw-fab");
    const GUEST_KEY = "dxChat:guest";
    const GUEST_MAX = 40;
    let busy = false;
    let historyLoad = null;

    const esc = (s) => String(s ?? "").replace(/[&<>"']/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]));
    // Only same-site paths from the server are turned into links.
    const safeUrl = (u) => (typeof u === "string" && u.startsWith("/") && !u.startsWith("//")) ? u : null;

    function toggle(open) {
        const show = open ?? panel.hidden;
        panel.hidden = !show;
        fab.setAttribute("aria-expanded", show ? "true" : "false");
        root.classList.toggle("is-open", show);
        if (show) {
            loadHistory();
            setTimeout(() => input.focus(), 0);
        }
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

    // One stored line: { me: true, text } or { me: false, text, answer }.
    function render(m) {
        if (m.me) add(esc(m.text), "is-me");
        else if (m.answer) renderAnswer(m.answer);
        else add(`<p>${esc(m.text)}</p>`, "is-bot");
    }

    function divider(text) {
        const div = document.createElement("div");
        div.className = "cw-divider";
        div.textContent = text;
        log.appendChild(div);
    }

    function guestRead() {
        try { return JSON.parse(sessionStorage.getItem(GUEST_KEY) || "[]"); } catch (_) { return []; }
    }

    function guestWrite(list) {
        try { sessionStorage.setItem(GUEST_KEY, JSON.stringify(list.slice(-GUEST_MAX))); } catch (_) { /* storage off: chat still works */ }
    }

    function loadHistory() {
        if (historyLoad) return historyLoad;
        historyLoad = (async () => {
            let messages = [];
            if (isCustomer) {
                try {
                    const res = await fetch(historyUrl, { credentials: "same-origin", headers: { Accept: "application/json" } });
                    const data = await res.json().catch(() => null);
                    messages = (data && Array.isArray(data.messages)) ? data.messages : [];
                } catch (_) { /* no history shown; new questions still work */ }
            } else {
                messages = guestRead();
            }
            if (!messages.length) return;
            divider("Cuộc trò chuyện trước");
            messages.forEach(render);
            divider("Tiếp tục trò chuyện");
            log.scrollTop = log.scrollHeight;
        })();
        return historyLoad;
    }

    async function ask(text) {
        if (busy || !text) return;
        busy = true;
        await loadHistory();
        add(esc(text), "is-me");
        const typing = add(`<span class="cw-typing"><i></i><i></i><i></i></span>`, "is-bot");
        try {
            const body = new URLSearchParams({ message: text });
            const res = await fetch(askUrl, { method: "POST", credentials: "same-origin", body });
            const data = await res.json().catch(() => null);
            typing.remove();
            if (!res.ok || !data || !data.ok) {
                add(esc((data && data.error) || "Trợ lý tạm thời không phản hồi, bạn thử lại sau nhé."), "is-bot is-error");
            } else {
                renderAnswer(data.answer);
                if (!isCustomer) guestWrite([...guestRead(), { me: true, text }, { me: false, text: data.answer.reply, answer: data.answer }]);
            }
        } catch (_) {
            typing.remove();
            add("Không kết nối được, bạn thử lại sau nhé.", "is-bot is-error");
        } finally {
            busy = false;
        }
    }

    async function clearHistory() {
        if (!confirm("Xóa toàn bộ lịch sử trò chuyện với trợ lý DriveX?")) return;
        try {
            const res = await fetch(clearUrl, { method: "POST", credentials: "same-origin", headers: { "X-Requested-With": "XMLHttpRequest" } });
            const data = await res.json().catch(() => null);
            if (!res.ok || !data || !data.ok) throw new Error();
            // Keep the welcome message and suggestions, drop everything after them.
            const chips = log.querySelector(".cw-chips");
            while (chips && chips.nextSibling) chips.nextSibling.remove();
        } catch (_) {
            add("Chưa xóa được lịch sử, bạn thử lại sau nhé.", "is-bot is-error");
        }
    }

    root.addEventListener("click", (e) => {
        if (e.target.closest("[data-cw-toggle]")) { toggle(); return; }
        if (e.target.closest("[data-cw-clear]")) { clearHistory(); return; }
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
