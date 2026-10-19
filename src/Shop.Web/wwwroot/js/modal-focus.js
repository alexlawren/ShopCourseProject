// Keep keyboard navigation inside the active dialog without a UI framework.
document.addEventListener("keydown", event => {
    if (event.key !== "Tab") return;
    const dialog = document.querySelector(".shop-modal[aria-modal='true']");
    if (!dialog) return;
    const controls = [...dialog.querySelectorAll("button, input, select, textarea, a[href], [tabindex]")]
        .filter(element => !element.disabled && element.tabIndex >= 0 && element.getClientRects().length);
    if (!controls.length) { event.preventDefault(); dialog.focus(); return; }
    const first = controls[0], last = controls[controls.length - 1];
    if (event.shiftKey && (document.activeElement === first || !dialog.contains(document.activeElement) || document.activeElement === dialog)) {
        event.preventDefault(); last.focus();
    } else if (!event.shiftKey && (document.activeElement === last || !dialog.contains(document.activeElement))) {
        event.preventDefault(); first.focus();
    }
});
