// Dispatcher overview charts. All numbers come from the page (Pages/Mockup/Index.cshtml.cs), built from live data.
(function () {
    const data = JSON.parse(document.getElementById("dashData")?.textContent || "{}");
    if (!window.Chart || !data.labels) return;
    const axis = "#64748b";
    const grid = "#e2e8f0";

    new Chart(document.getElementById("dashPerfChart"), {
        type: "bar",
        data: {
            labels: data.labels,
            datasets: data.pickups.map((s) => ({ label: s.label, data: s.data, backgroundColor: s.color, borderRadius: 4, stack: "pickups" }))
        },
        options: {
            responsive: true, maintainAspectRatio: false,
            plugins: { legend: { labels: { color: axis, boxWidth: 10, font: { size: 11 } } } },
            scales: {
                x: { stacked: true, ticks: { color: axis }, grid: { color: grid } },
                y: { stacked: true, beginAtZero: true, ticks: { color: axis, precision: 0 }, grid: { color: grid } }
            }
        }
    });

    const total = data.open.reduce((sum, s) => sum + s.value, 0);
    new Chart(document.getElementById("dashDonut"), {
        type: "doughnut",
        data: {
            labels: data.open.map((s) => s.label),
            datasets: [{
                data: total === 0 ? [1] : data.open.map((s) => s.value),
                backgroundColor: total === 0 ? ["#e2e8f0"] : data.open.map((s) => s.color),
                borderWidth: 0
            }]
        },
        options: { responsive: true, maintainAspectRatio: false, cutout: "72%", plugins: { legend: { display: false }, tooltip: { enabled: total > 0 } } }
    });

    document.getElementById("dashDonutLegend").innerHTML = data.open.map((s) =>
        `<li><span><i class="dash-dot" style="background:${s.color}"></i>${s.label}</span><b>${s.value}${total ? ` (${Math.round(s.value * 100 / total)}%)` : ""}</b></li>`
    ).join("");
})();
