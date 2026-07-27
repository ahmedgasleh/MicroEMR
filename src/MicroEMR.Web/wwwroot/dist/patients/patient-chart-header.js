function activateChartTab(tabName) {
    const tabButton = document.querySelector(`[data-bs-toggle="tab"][data-bs-target="#${tabName}"]`);
    if (!tabButton)
        return;
    const updateLocation = () => {
        const url = new URL(window.location.href);
        url.searchParams.set("tab", tabName);
        window.history.replaceState({}, "", url);
    };
    if (tabButton.classList.contains("active")) {
        updateLocation();
        return;
    }
    tabButton.addEventListener("shown.bs.tab", updateLocation, { once: true });
    bootstrap.Tab.getOrCreateInstance(tabButton).show();
}
function initializePatientChartHeader() {
    const banner = document.getElementById("patientChartBanner");
    if (!banner || banner.dataset.initialized === "true")
        return;
    banner.dataset.initialized = "true";
    banner
        .querySelectorAll("[data-chart-tab-link]")
        .forEach((link) => {
        link.addEventListener("click", () => {
            const tabName = link.dataset.chartTabLink;
            if (tabName)
                activateChartTab(tabName);
        });
    });
}
if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", initializePatientChartHeader, {
        once: true,
    });
}
else {
    initializePatientChartHeader();
}
export {};
//# sourceMappingURL=patient-chart-header.js.map