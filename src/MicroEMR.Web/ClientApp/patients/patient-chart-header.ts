export {};

interface BootstrapTab {
  show(): void;
}

declare const bootstrap: {
  Tab: {
    getOrCreateInstance(element: HTMLElement): BootstrapTab;
  };
};

function activateChartTab(tabName: string): void {
  const tabButton = document.querySelector<HTMLElement>(
    `[data-bs-toggle="tab"][data-bs-target="#${tabName}"]`,
  );
  if (!tabButton) return;

  const updateLocation = (): void => {
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

function initializePatientChartHeader(): void {
  const banner = document.getElementById("patientChartBanner");
  if (!banner || banner.dataset.initialized === "true") return;

  banner.dataset.initialized = "true";
  banner
    .querySelectorAll<HTMLButtonElement>("[data-chart-tab-link]")
    .forEach((link) => {
      link.addEventListener("click", () => {
        const tabName = link.dataset.chartTabLink;
        if (tabName) activateChartTab(tabName);
      });
    });
}

if (document.readyState === "loading") {
  document.addEventListener("DOMContentLoaded", initializePatientChartHeader, {
    once: true,
  });
} else {
  initializePatientChartHeader();
}
