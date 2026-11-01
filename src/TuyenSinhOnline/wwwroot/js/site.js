"use strict";
document.querySelectorAll("[data-password-target]").forEach(function (button) {
    button.addEventListener("click", function () {
        const input = document.getElementById(button.dataset.passwordTarget);
        if (!input) return;
        const show = input.type === "password";
        input.type = show ? "text" : "password";
        button.textContent = show ? "Ẩn" : "Hiện";
        button.setAttribute("aria-pressed", String(show));
        const fieldName = input.id === "ConfirmPassword" ? "mật khẩu nhập lại" : "mật khẩu";
        button.setAttribute("aria-label", (show ? "Ẩn " : "Hiện ") + fieldName);
    });
});
const navToggle = document.querySelector("[data-nav-toggle]");
if (navToggle) {
    navToggle.addEventListener("click", function () {
        const navigation = document.getElementById(navToggle.getAttribute("aria-controls"));
        if (!navigation) return;
        const expanded = navToggle.getAttribute("aria-expanded") === "true";
        navToggle.setAttribute("aria-expanded", String(!expanded));
        navigation.classList.toggle("is-open", !expanded);
    });
}
