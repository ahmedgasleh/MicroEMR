const jars = new Map();

function saveCookies(url, response) {
  const origin = new URL(url).origin;
  const jar = jars.get(origin) ?? new Map();
  for (const value of response.headers.getSetCookie?.() ?? []) {
    const pair = value.split(";", 1)[0];
    const split = pair.indexOf("=");
    if (split > 0) jar.set(pair.slice(0, split), pair.slice(split + 1));
  }
  jars.set(origin, jar);
}

async function request(url, options = {}) {
  const origin = new URL(url).origin;
  const headers = new Headers(options.headers ?? {});
  const jar = jars.get(origin);
  if (jar?.size) headers.set("cookie", [...jar].map(([key, value]) => `${key}=${value}`).join("; "));
  const response = await fetch(url, { ...options, headers, redirect: "manual" });
  saveCookies(url, response);
  return response;
}

function decode(value) {
  return value.replaceAll("&amp;", "&").replaceAll("&#x2B;", "+").replaceAll("&#x2F;", "/").replaceAll("&#x3D;", "=");
}

function hidden(html) {
  const result = new Map();
  for (const match of html.matchAll(/<input[^>]+type=["']hidden["'][^>]*>/gi)) {
    const name = match[0].match(/name=["']([^"']+)["']/i)?.[1];
    const value = match[0].match(/value=["']([^"']*)["']/i)?.[1] ?? "";
    if (name) result.set(name, decode(value));
  }
  return result;
}

async function login() {
  let response = await request("https://localhost:7002/");
  let url = response.headers.get("location");
  response = await request(url);
  url = response.headers.get("location");
  response = await request(url);
  const loginFields = hidden(await response.text());
  loginFields.set("Username", process.env.STEP41_EMAIL ?? "");
  loginFields.set("Password", process.env.STEP41_PASSWORD ?? "");
  response = await request("https://localhost:7179/Account/Login", {
    method: "POST", headers: { "content-type": "application/x-www-form-urlencoded" }, body: new URLSearchParams(loginFields)
  });
  url = new URL(response.headers.get("location"), "https://localhost:7179").href;
  response = await request(url);
  if (response.status === 302 && response.headers.get("location")?.startsWith("/Account/SelectTenant")) {
    const selectionUrl = new URL(response.headers.get("location"), url).href;
    response = await request(selectionUrl);
    const selectionHtml = await response.text();
    const selectionFields = hidden(selectionHtml);
    const tenantLabel = [...selectionHtml.matchAll(/<label[\s\S]*?<\/label>/g)].map(x => x[0]).find(x => x.includes(">local-dev-fresh<"));
    const tenantUid = process.env.STEP41_TENANT_UID ?? tenantLabel?.match(/value="([0-9a-f-]{36})"/i)?.[1];
    if (!tenantUid) throw new Error("Synthetic tenant selection unavailable");
    selectionFields.set("SelectedTenantUid", tenantUid);
    response = await request("https://localhost:7179/Account/SelectTenant", {
      method: "POST", headers: { "content-type": "application/x-www-form-urlencoded" }, body: new URLSearchParams(selectionFields)
    });
    url = new URL(response.headers.get("location"), selectionUrl).href;
    response = await request(url);
  }
  const authorizeHtml = await response.text();
  const action = authorizeHtml.match(/<form[^>]+action=["']([^"']+)["']/i)?.[1];
  const authorizeFields = hidden(authorizeHtml);
  response = await request(new URL(decode(action), url).href, {
    method: "POST", headers: { "content-type": "application/x-www-form-urlencoded" }, body: new URLSearchParams(authorizeFields)
  });
  url = new URL(response.headers.get("location"), "https://localhost:7002").href;
  response = await request(url);
  if (response.status !== 200) throw new Error(`Login completion returned ${response.status}`);
}

await login();
const patientUid = "c280a1c3-9b56-4fbc-9370-99bed868cfe1";
const chart = await request(`https://localhost:7002/Patients/Details?patientUid=${patientUid}&tab=results`);
if (chart.status !== 200) throw new Error(`Chart returned ${chart.status}`);
const antiforgery = hidden(await chart.text()).get("__RequestVerificationToken");
if (!antiforgery) throw new Error("Chart antiforgery token unavailable");
async function post(path, fields) {
  fields.__RequestVerificationToken = antiforgery;
  const response = await request(`https://localhost:7002${path}`, {
    method: "POST", headers: { "content-type": "application/x-www-form-urlencoded" }, body: new URLSearchParams(fields)
  });
  const body = await response.text();
  let value;
  try { value = JSON.parse(body); } catch {}
  return { response, body, value };
}

const common = {
  PatientUid: patientUid,
  ResultType: "Lab",
  ResultName: "Step41R repaired reproduction",
  ResultDate: "2026-09-22T16:30:00",
  ResultSummary: "Controlled synthetic reproduction",
  ResultValue: "1",
  ResultUnit: "synthetic",
  ReferenceRange: "0-2",
  SourceType: "External",
  SourceOrganization: "Synthetic Laboratory",
  SourceSystem: "STEP41R",
  ExternalResultId: `STEP41R-${Date.now()}`,
  ReceivedAtUtc: "2026-09-22T16:31:00",
  Abnormality: "Normal"
};
const created = await post("/PatientResults/Create", { ...common });
const original = created.value?.result;
if (!original?.patientResultUid) throw new Error(`Create failed: ${created.response.status}`);
const reviewed = await post("/PatientResults/Review", {
  PatientUid: patientUid, PatientResultUid: original.patientResultUid,
  ExpectedRowVersion: original.rowVersion, ReviewNote: "Step41R repaired review"
});
const reviewedResult = reviewed.value?.result;
if (reviewed.response.status !== 200 || !reviewedResult?.rowVersion) throw new Error(`Review failed: ${reviewed.response.status}`);
const corrected = await post("/PatientResults/Correct", {
  ...common, PatientResultUid: original.patientResultUid,
  ExpectedRowVersion: reviewedResult.rowVersion,
  ResultValue: "2", ResultSummary: "Controlled corrected value"
});
if (corrected.response.status !== 200 || !corrected.value?.result?.patientResultUid) throw new Error(`Correction failed: ${corrected.response.status}`);
const stale = await post("/PatientResults/Correct", {
  ...common, PatientResultUid: original.patientResultUid,
  ExpectedRowVersion: reviewedResult.rowVersion, ResultValue: "3"
});
const history = await request(`https://localhost:7002/PatientResults/History?patientUid=${patientUid}&resultUid=${original.patientResultUid}`);
const historyBody = await history.text();
let historyJson;
try { historyJson = JSON.parse(historyBody); } catch {}
const entries = historyJson?.results ?? [];
const first = entries.find(x => x.patientResultUid === original.patientResultUid);
const next = entries.find(x => x.patientResultUid === corrected.value.result.patientResultUid);
console.log(JSON.stringify({
  endpoint: `/PatientResults/History?patientUid={synthetic-patient}&resultUid=${original.patientResultUid}`,
  sequence: {
    create: created.response.status,
    review: reviewed.response.status,
    correct: corrected.response.status,
    history: history.status, staleCorrection: stale.response.status
  },
  historyChecks: {
    twoEntries: entries.length === 2,
    originalReviewedSuperseded: first?.resultStatus === "Reviewed" && first?.lifecycleStatus === "Superseded",
    correctionCurrentNew: next?.resultStatus === "New" && next?.lifecycleStatus === "Current",
    lineage: next?.previousResultUid === original.patientResultUid,
    provenance: entries.length === 2 && entries.every(x => x.sourceType === "External" && x.sourceSystem === "STEP41R" && x.sourceOrganization === common.SourceOrganization && x.externalResultId === common.ExternalResultId),
    originalReviewPreserved: first?.reviewedAt === reviewedResult.reviewedAt && first?.reviewedBy === reviewedResult.reviewedBy,
    correctedUnreviewed: next?.reviewedAt == null && next?.reviewedBy == null
  },
  leak: {
    contentType: history.headers.get("content-type"),
    length: historyBody.length,
    stackTrace: /\n\s+at\s+/.test(historyBody),
    sourcePath: /[A-Z]:\\[^\r\n]+\.cs:line\s+\d+/i.test(historyBody),
    requestHeaders: /HEADERS\s*={3,}/i.test(historyBody),
    cookieHeader: /Cookie:\s+/i.test(historyBody),
    aspNetCoreCookieName: /\.AspNetCore/i.test(historyBody),
    authorization: /Authorization:\s+/i.test(historyBody),
    rawExceptionType: /System\.Net\.Http\.HttpRequestException/i.test(historyBody),
    traceId: /"traceId"\s*:/i.test(historyBody)
  }
}, null, 2));

if (history.status !== 200 || entries.length !== 2 || !next || next.previousResultUid !== original.patientResultUid || stale.response.status !== 409) process.exitCode = 1;
