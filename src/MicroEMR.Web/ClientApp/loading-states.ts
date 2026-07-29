export {};

const searchForm = document.querySelector<HTMLFormElement>("[data-microemr-search-form]");
const searchButton = document.querySelector<HTMLButtonElement>("[data-microemr-search-button]");
const searchLoading = document.querySelector<HTMLElement>("[data-microemr-search-loading]");
const searchResults = document.querySelector<HTMLElement>("[data-microemr-search-results]");
const originalButtonContent = searchButton?.innerHTML ?? "";

const resetSearchState = (): void => {
    if (!searchButton || !searchLoading || !searchResults) return;
    searchButton.disabled = false;
    searchButton.classList.remove("microemr-button-loading");
    searchButton.innerHTML = originalButtonContent;
    searchLoading.classList.add("d-none");
    searchResults.classList.remove("d-none");
    searchResults.setAttribute("aria-busy", "false");
};

searchForm?.addEventListener("submit", (event: SubmitEvent) => {
    if (!searchForm.checkValidity()) return;

    window.setTimeout(() => {
        if (event.defaultPrevented || !searchButton || !searchLoading || !searchResults) return;
        searchButton.disabled = true;
        searchButton.classList.add("microemr-button-loading");
        searchButton.innerHTML = '<span class="spinner-border spinner-border-sm" aria-hidden="true"></span>Searching...';
        searchResults.classList.add("d-none");
        searchResults.setAttribute("aria-busy", "true");
        searchLoading.classList.remove("d-none");
    }, 0);
});

window.addEventListener("pageshow", resetSearchState);
