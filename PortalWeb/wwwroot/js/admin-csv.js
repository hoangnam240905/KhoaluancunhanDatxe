// Admin CSV import (plan1.md Giai đoạn 3): pick a file, post it, show imported rows / row errors / temp passwords.
(function () {
    'use strict';

    function esc(value) {
        return String(value ?? '').replace(/[&<>"']/g, function (c) {
            return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c];
        });
    }

    function render(data) {
        var html = '<p class="aui-csv-sum"><strong>' + data.imported + '</strong> dòng đã nhập'
            + (data.errors.length ? ', <strong class="aui-csv-bad">' + data.errors.length + '</strong> dòng lỗi (không nhập).' : '.') + '</p>';
        if (data.accounts && data.accounts.length) {
            html += '<p class="aui-csv-note"><i class="bi bi-key"></i> Mật khẩu tạm chỉ hiện một lần — hãy lưu lại và gửi cho từng người.</p>'
                + '<table class="aui-csv-table"><thead><tr><th>Dòng</th><th>Email</th><th>Mật khẩu tạm</th></tr></thead><tbody>'
                + data.accounts.map(function (a) {
                    return '<tr><td>' + a.row + '</td><td>' + esc(a.email) + '</td><td><code>' + esc(a.temporaryPassword) + '</code></td></tr>';
                }).join('') + '</tbody></table>';
        }
        if (data.errors.length) {
            html += '<table class="aui-csv-table"><thead><tr><th>Dòng</th><th>Lý do</th></tr></thead><tbody>'
                + data.errors.map(function (e) {
                    return '<tr><td>' + e.row + '</td><td>' + esc(e.reason) + '</td></tr>';
                }).join('') + '</tbody></table>';
        }
        return html;
    }

    document.querySelectorAll('[data-aui-csv]').forEach(function (root) {
        var form = root.querySelector('[data-csv-form]');
        var input = root.querySelector('[data-csv-file]');
        var pick = root.querySelector('[data-csv-pick]');
        var modal = root.querySelector('[data-csv-modal]');
        var body = root.querySelector('[data-csv-body]');
        var imported = 0;

        function show(html) {
            body.innerHTML = html;
            modal.hidden = false;
        }

        pick.addEventListener('click', function () { input.value = ''; input.click(); });

        input.addEventListener('change', function () {
            if (!input.files.length) return;
            pick.disabled = true;
            pick.innerHTML = '<i class="bi bi-hourglass-split"></i> Đang nhập…';
            fetch(form.action, { method: 'POST', body: new FormData(form), credentials: 'same-origin' })
                .then(function (r) {
                    return r.json().catch(function () { return { message: 'Lỗi máy chủ (' + r.status + ').' }; })
                        .then(function (data) { return { ok: r.ok, data: data }; });
                })
                .then(function (res) {
                    if (!res.ok) { show('<p class="aui-csv-bad">' + esc(res.data.message || 'Nhập CSV thất bại.') + '</p>'); return; }
                    imported = res.data.imported;
                    show(render(res.data));
                })
                .catch(function () { show('<p class="aui-csv-bad">Không kết nối được máy chủ.</p>'); })
                .finally(function () {
                    pick.disabled = false;
                    pick.innerHTML = '<i class="bi bi-upload"></i> Nhập CSV';
                });
        });

        root.querySelectorAll('[data-csv-close]').forEach(function (btn) {
            btn.addEventListener('click', function () {
                modal.hidden = true;
                if (imported > 0) location.reload(); // show the new rows in the list
            });
        });
    });
})();
