interface ResourceDefaultsResponse {
    resourceUids: string[];
    hasSavedDefaults: boolean;
    rowVersion: string | null;
    unavailableResourceCount: number;
    isAvailable: boolean;
}

class DefaultsRequestError extends Error {
    constructor(public status: number) { super("Resource defaults request failed."); }
}

class SchedulingResourceDefaults {
    private version: string | null = null;
    private ready = false;
    private busy = false;
    private readonly available: Set<string>;
    message = "";
    messageKind = "info";

    constructor(private readonly resources: readonly string[], private readonly url: string,
        private readonly antiforgery: () => string, private readonly fetcher: typeof fetch = window.fetch.bind(window)) {
        this.available = new Set(resources);
    }

    get canSave(): boolean { return this.ready && !this.busy; }

    async load(): Promise<string[]> {
        this.busy = true;
        try {
            const response = await this.request("GET");
            if (!response.isAvailable || !Array.isArray(response.resourceUids)
                || typeof response.hasSavedDefaults !== "boolean"
                || (response.hasSavedDefaults && typeof response.rowVersion !== "string"))
                throw new Error("Resource defaults unavailable.");
            this.ready = true;
            this.version = response.rowVersion;
            const effective = [...new Set(response.resourceUids)].filter(uid => this.available.has(uid));
            this.messageKind = "info";
            this.message = !response.hasSavedDefaults ? "No saved defaults. Showing the initial resource selection."
                : effective.length === 0 ? "Your saved resources are unavailable. Showing the initial resource selection."
                : response.unavailableResourceCount > 0 ? "Unavailable saved resources were omitted. Your saved defaults have not changed."
                : "Your saved resource defaults are restored.";
            return response.hasSavedDefaults && effective.length ? effective : this.initial();
        } catch {
            this.ready = false;
            this.messageKind = "warning";
            this.message = "Resource defaults are unavailable. You can still select resources. Use Restore My Defaults to retry before saving.";
            return this.initial();
        } finally { this.busy = false; }
    }

    async restore(current: Iterable<string>): Promise<string[]> {
        const effective = await this.load();
        return this.ready ? effective : [...current];
    }

    async save(current: Iterable<string>): Promise<boolean> {
        if (!this.canSave) return false;
        const selected = [...new Set(current)];
        if (!selected.length || selected.length > 200 || selected.some(uid => !this.available.has(uid))) {
            this.messageKind = "warning";
            this.message = "Select between 1 and 200 available resources before saving defaults.";
            return false;
        }
        this.busy = true;
        try {
            const response = await this.request("POST", {resourceUids: selected, rowVersion: this.version});
            if (!response.isAvailable || !response.hasSavedDefaults || typeof response.rowVersion !== "string")
                throw new Error("Resource defaults save unavailable.");
            this.version = response.rowVersion;
            this.messageKind = "success";
            this.message = "Your default resources were saved. Temporary selections will not change them.";
            return true;
        } catch (error) {
            if (!(error instanceof DefaultsRequestError) || error.status !== 400) this.ready = false;
            this.messageKind = "danger";
            this.message = error instanceof DefaultsRequestError && error.status === 409
                ? "Defaults changed in another session. Use Restore My Defaults, then review and save again."
                : error instanceof DefaultsRequestError && error.status === 400
                    ? "One or more resources are no longer available. Review your selection before saving."
                    : "Resource defaults could not be saved. Use Restore My Defaults before retrying.";
            return false;
        } finally { this.busy = false; }
    }

    private initial(): string[] { return this.resources.slice(0, Math.min(5, this.resources.length)); }

    private async request(method: "GET" | "POST", body?: object): Promise<ResourceDefaultsResponse> {
        const controller = new AbortController();
        const timer = setTimeout(() => controller.abort(), 8000);
        try {
            const response = await this.fetcher(this.url, {
                method, cache: "no-store", signal: controller.signal,
                headers: method === "POST"
                    ? {"Accept": "application/json", "Content-Type": "application/json", "RequestVerificationToken": this.antiforgery()}
                    : {"Accept": "application/json"},
                body: body ? JSON.stringify(body) : undefined
            });
            if (!response.ok || response.redirected) throw new DefaultsRequestError(response.status);
            return await response.json() as ResourceDefaultsResponse;
        } finally { clearTimeout(timer); }
    }
}

declare global {
    interface Window { SchedulingResourceDefaults: typeof SchedulingResourceDefaults; }
}
window.SchedulingResourceDefaults = SchedulingResourceDefaults;
export {};
