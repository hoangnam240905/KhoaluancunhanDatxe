(function () {
    // DEMO TEMP: SignalR client off. Next week: delete this return to restore.
    return;

    var cfg = window.carRentalRealtime;
    if (!cfg || !cfg.hubUrl || !cfg.accessToken)
        return;

    var RS = String.fromCharCode(0x1e);
    var retry = 0;
    var socket = null;
    var closedByUs = false;
    var reloadOnOpen = false;

    function hubHttp() {
        return String(cfg.hubUrl).replace(/\/$/, "");
    }

    function hubWs(connectionToken) {
        var http = hubHttp();
        var ws = /^https:/i.test(http) ? http.replace(/^https/i, "wss") : http.replace(/^http/i, "ws");
        return ws + "?id=" + encodeURIComponent(connectionToken)
            + "&access_token=" + encodeURIComponent(cfg.accessToken);
    }

    function refreshFromApi() {
        if (typeof cfg.onEvent === "function") {
            cfg.onEvent();
            return;
        }
        window.location.reload();
    }

    function handleRecord(raw) {
        if (!raw) return;
        var msg;
        try { msg = JSON.parse(raw); } catch (_) { return; }
        if (msg.type === 6 && socket && socket.readyState === WebSocket.OPEN) {
            socket.send(JSON.stringify({ type: 7 }) + RS);
            return;
        }
        if (msg.type === 1 && msg.target === "ReceiveEvent")
            refreshFromApi();
    }

    function scheduleReconnect(reload) {
        retry += 1;
        if (retry > 8) return;
        if (reload)
            reloadOnOpen = true;
        setTimeout(connect, Math.min(1000 * retry, 5000));
    }

    function connect() {
        closedByUs = false;
        fetch(hubHttp() + "/negotiate?negotiateVersion=1", {
            method: "POST",
            headers: { "Authorization": "Bearer " + cfg.accessToken }
        }).then(function (res) {
            if (!res.ok) throw new Error("negotiate " + res.status);
            return res.json();
        }).then(function (body) {
            var token = body.connectionToken || body.connectionId;
            if (!token) throw new Error("missing connection token");
            socket = new WebSocket(hubWs(token));
            socket.onopen = function () {
                retry = 0;
                socket.send(JSON.stringify({ protocol: "json", version: 1 }) + RS);
                if (reloadOnOpen) {
                    reloadOnOpen = false;
                    refreshFromApi();
                }
            };
            var buffer = "";
            socket.onmessage = function (ev) {
                buffer += ev.data;
                var parts = buffer.split(RS);
                buffer = parts.pop();
                for (var i = 0; i < parts.length; i++)
                    handleRecord(parts[i]);
            };
            socket.onclose = function () {
                if (!closedByUs)
                    scheduleReconnect(true);
            };
            socket.onerror = function () {
                try { socket.close(); } catch (_) { /* ignore */ }
            };
        }).catch(function (err) {
            console.warn("Realtime unavailable", err);
            scheduleReconnect(false);
        });
    }

    connect();
})();
