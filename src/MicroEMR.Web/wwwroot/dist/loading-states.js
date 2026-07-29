const searchForm = document.querySelector("[data-microemr-search-form]");
const searchButton = document.querySelector("[data-microemr-search-button]");
const searchLoading = document.querySelector("[data-microemr-search-loading]");
const searchResults = document.querySelector("[data-microemr-search-results]");
const originalButtonContent = searchButton?.innerHTML ?? "";
const resetSearchState = () => {
    if (!searchButton || !searchLoading || !searchResults)
        return;
    searchButton.disabled = false;
    searchButton.classList.remove("microemr-button-loading");
    searchButton.innerHTML = originalButtonContent;
    searchLoading.classList.add("d-none");
    searchResults.classList.remove("d-none");
    searchResults.setAttribute("aria-busy", "false");
};
searchForm?.addEventListener("submit", (event) => {
    if (!searchForm.checkValidity())
        return;
    window.setTimeout(() => {
        if (event.defaultPrevented || !searchButton || !searchLoading || !searchResults)
            return;
        searchButton.disabled = true;
        searchButton.classList.add("microemr-button-loading");
        searchButton.innerHTML = '<span class="spinner-border spinner-border-sm" aria-hidden="true"></span>Searching...';
        searchResults.classList.add("d-none");
        searchResults.setAttribute("aria-busy", "true");
        searchLoading.classList.remove("d-none");
    }, 0);
});
window.addEventListener("pageshow", resetSearchState);
export {};
//# sourceMappingURL=loading-states.js.map