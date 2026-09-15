(() => {
    const toast = document.querySelector("[data-bd-toast]");
    if (!toast) return;

    document.querySelectorAll("[data-bd-ui]").forEach((btn) => {
        btn.addEventListener("click", () => {
            toast.textContent = btn.getAttribute("data-bd-ui") || "Tính năng này sẽ được kết nối ở bước sau.";
            toast.classList.add("is-on");
        });
    });
})();
