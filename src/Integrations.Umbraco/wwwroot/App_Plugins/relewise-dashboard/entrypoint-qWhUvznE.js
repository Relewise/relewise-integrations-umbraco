var I = Object.defineProperty;
var q = (e, t, r) => t in e ? I(e, t, { enumerable: !0, configurable: !0, writable: !0, value: r }) : e[t] = r;
var C = (e, t, r) => q(e, typeof t != "symbol" ? t + "" : t, r);
import { UMB_AUTH_CONTEXT as z } from "@umbraco-cms/backoffice/auth";
import { html as u, css as N, state as f, customElement as D } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement as W } from "@umbraco-cms/backoffice/lit-element";
var P = async (e, t) => {
  let r = typeof t == "function" ? await t(e) : t;
  if (r) return e.scheme === "bearer" ? `Bearer ${r}` : e.scheme === "basic" ? `Basic ${btoa(r)}` : r;
}, B = { bodySerializer: (e) => JSON.stringify(e, (t, r) => typeof r == "bigint" ? r.toString() : r) }, M = (e) => {
  switch (e) {
    case "label":
      return ".";
    case "matrix":
      return ";";
    case "simple":
      return ",";
    default:
      return "&";
  }
}, L = (e) => {
  switch (e) {
    case "form":
      return ",";
    case "pipeDelimited":
      return "|";
    case "spaceDelimited":
      return "%20";
    default:
      return ",";
  }
}, H = (e) => {
  switch (e) {
    case "label":
      return ".";
    case "matrix":
      return ";";
    case "simple":
      return ",";
    default:
      return "&";
  }
}, E = ({ allowReserved: e, explode: t, name: r, style: i, value: l }) => {
  if (!t) {
    let s = (e ? l : l.map((n) => encodeURIComponent(n))).join(L(i));
    switch (i) {
      case "label":
        return `.${s}`;
      case "matrix":
        return `;${r}=${s}`;
      case "simple":
        return s;
      default:
        return `${r}=${s}`;
    }
  }
  let o = M(i), a = l.map((s) => i === "label" || i === "simple" ? e ? s : encodeURIComponent(s) : $({ allowReserved: e, name: r, value: s })).join(o);
  return i === "label" || i === "matrix" ? o + a : a;
}, $ = ({ allowReserved: e, name: t, value: r }) => {
  if (r == null) return "";
  if (typeof r == "object") throw new Error("Deeply-nested arrays/objects aren’t supported. Provide your own `querySerializer()` to handle these.");
  return `${t}=${e ? r : encodeURIComponent(r)}`;
}, k = ({ allowReserved: e, explode: t, name: r, style: i, value: l }) => {
  if (l instanceof Date) return `${r}=${l.toISOString()}`;
  if (i !== "deepObject" && !t) {
    let s = [];
    Object.entries(l).forEach(([g, h]) => {
      s = [...s, g, e ? h : encodeURIComponent(h)];
    });
    let n = s.join(",");
    switch (i) {
      case "form":
        return `${r}=${n}`;
      case "label":
        return `.${n}`;
      case "matrix":
        return `;${r}=${n}`;
      default:
        return n;
    }
  }
  let o = H(i), a = Object.entries(l).map(([s, n]) => $({ allowReserved: e, name: i === "deepObject" ? `${r}[${s}]` : s, value: n })).join(o);
  return i === "label" || i === "matrix" ? o + a : a;
}, F = /\{[^{}]+\}/g, J = ({ path: e, url: t }) => {
  let r = t, i = t.match(F);
  if (i) for (let l of i) {
    let o = !1, a = l.substring(1, l.length - 1), s = "simple";
    a.endsWith("*") && (o = !0, a = a.substring(0, a.length - 1)), a.startsWith(".") ? (a = a.substring(1), s = "label") : a.startsWith(";") && (a = a.substring(1), s = "matrix");
    let n = e[a];
    if (n == null) continue;
    if (Array.isArray(n)) {
      r = r.replace(l, E({ explode: o, name: a, style: s, value: n }));
      continue;
    }
    if (typeof n == "object") {
      r = r.replace(l, k({ explode: o, name: a, style: s, value: n }));
      continue;
    }
    if (s === "matrix") {
      r = r.replace(l, `;${$({ name: a, value: n })}`);
      continue;
    }
    let g = encodeURIComponent(s === "label" ? `.${n}` : n);
    r = r.replace(l, g);
  }
  return r;
}, j = ({ allowReserved: e, array: t, object: r } = {}) => (i) => {
  let l = [];
  if (i && typeof i == "object") for (let o in i) {
    let a = i[o];
    if (a != null) if (Array.isArray(a)) {
      let s = E({ allowReserved: e, explode: !0, name: o, style: "form", value: a, ...t });
      s && l.push(s);
    } else if (typeof a == "object") {
      let s = k({ allowReserved: e, explode: !0, name: o, style: "deepObject", value: a, ...r });
      s && l.push(s);
    } else {
      let s = $({ allowReserved: e, name: o, value: a });
      s && l.push(s);
    }
  }
  return l.join("&");
}, V = (e) => {
  var r;
  if (!e) return "stream";
  let t = (r = e.split(";")[0]) == null ? void 0 : r.trim();
  if (t) {
    if (t.startsWith("application/json") || t.endsWith("+json")) return "json";
    if (t === "multipart/form-data") return "formData";
    if (["application/", "audio/", "image/", "video/"].some((i) => t.startsWith(i))) return "blob";
    if (t.startsWith("text/")) return "text";
  }
}, G = async ({ security: e, ...t }) => {
  for (let r of e) {
    let i = await P(r, t.auth);
    if (!i) continue;
    let l = r.name ?? "Authorization";
    switch (r.in) {
      case "query":
        t.query || (t.query = {}), t.query[l] = i;
        break;
      case "cookie":
        t.headers.append("Cookie", `${l}=${i}`);
        break;
      case "header":
      default:
        t.headers.set(l, i);
        break;
    }
    return;
  }
}, A = (e) => X({ baseUrl: e.baseUrl, path: e.path, query: e.query, querySerializer: typeof e.querySerializer == "function" ? e.querySerializer : j(e.querySerializer), url: e.url }), X = ({ baseUrl: e, path: t, query: r, querySerializer: i, url: l }) => {
  let o = l.startsWith("/") ? l : `/${l}`, a = (e ?? "") + o;
  t && (a = J({ path: t, url: a }));
  let s = r ? i(r) : "";
  return s.startsWith("?") && (s = s.substring(1)), s && (a += `?${s}`), a;
}, R = (e, t) => {
  var i;
  let r = { ...e, ...t };
  return (i = r.baseUrl) != null && i.endsWith("/") && (r.baseUrl = r.baseUrl.substring(0, r.baseUrl.length - 1)), r.headers = _(e.headers, t.headers), r;
}, _ = (...e) => {
  let t = new Headers();
  for (let r of e) {
    if (!r || typeof r != "object") continue;
    let i = r instanceof Headers ? r.entries() : Object.entries(r);
    for (let [l, o] of i) if (o === null) t.delete(l);
    else if (Array.isArray(o)) for (let a of o) t.append(l, a);
    else o !== void 0 && t.set(l, typeof o == "object" ? JSON.stringify(o) : o);
  }
  return t;
}, U = class {
  constructor() {
    C(this, "_fns");
    this._fns = [];
  }
  clear() {
    this._fns = [];
  }
  getInterceptorIndex(e) {
    return typeof e == "number" ? this._fns[e] ? e : -1 : this._fns.indexOf(e);
  }
  exists(e) {
    let t = this.getInterceptorIndex(e);
    return !!this._fns[t];
  }
  eject(e) {
    let t = this.getInterceptorIndex(e);
    this._fns[t] && (this._fns[t] = null);
  }
  update(e, t) {
    let r = this.getInterceptorIndex(e);
    return this._fns[r] ? (this._fns[r] = t, e) : !1;
  }
  use(e) {
    return this._fns = [...this._fns, e], this._fns.length - 1;
  }
}, Y = () => ({ error: new U(), request: new U(), response: new U() }), K = j({ allowReserved: !1, array: { explode: !0, style: "form" }, object: { explode: !0, style: "deepObject" } }), Q = { "Content-Type": "application/json" }, S = (e = {}) => ({ ...B, headers: Q, parseAs: "auto", querySerializer: K, ...e }), Z = (e = {}) => {
  let t = R(S(), e), r = () => ({ ...t }), i = (a) => (t = R(t, a), r()), l = Y(), o = async (a) => {
    let s = { ...t, ...a, fetch: a.fetch ?? t.fetch ?? globalThis.fetch, headers: _(t.headers, a.headers) };
    s.security && await G({ ...s, security: s.security }), s.body && s.bodySerializer && (s.body = s.bodySerializer(s.body)), (s.body === void 0 || s.body === "") && s.headers.delete("Content-Type");
    let n = A(s), g = { redirect: "follow", ...s }, h = new Request(n, g);
    for (let c of l.request._fns) c && (h = await c(h, s));
    let O = s.fetch, d = await O(h);
    for (let c of l.response._fns) c && (d = await c(d, h, s));
    let m = { request: h, response: d };
    if (d.ok) {
      if (d.status === 204 || d.headers.get("Content-Length") === "0") return { data: {}, ...m };
      let c = (s.parseAs === "auto" ? V(d.headers.get("Content-Type")) : s.parseAs) ?? "json";
      if (c === "stream") return { data: d.body, ...m };
      let v = await d[c]();
      return c === "json" && (s.responseValidator && await s.responseValidator(v), s.responseTransformer && (v = await s.responseTransformer(v))), { data: v, ...m };
    }
    let w = await d.text();
    try {
      w = JSON.parse(w);
    } catch {
    }
    let y = w;
    for (let c of l.error._fns) c && (y = await c(w, d, h, s));
    if (y = y || {}, s.throwOnError) throw y;
    return { error: y, ...m };
  };
  return { buildUrl: A, connect: (a) => o({ ...a, method: "CONNECT" }), delete: (a) => o({ ...a, method: "DELETE" }), get: (a) => o({ ...a, method: "GET" }), getConfig: r, head: (a) => o({ ...a, method: "HEAD" }), interceptors: l, options: (a) => o({ ...a, method: "OPTIONS" }), patch: (a) => o({ ...a, method: "PATCH" }), post: (a) => o({ ...a, method: "POST" }), put: (a) => o({ ...a, method: "PUT" }), request: o, setConfig: i, trace: (a) => o({ ...a, method: "TRACE" }) };
};
const x = Z(S({
  baseUrl: "https://localhost:44381"
}));
class T {
  static configuration(t) {
    return ((t == null ? void 0 : t.client) ?? x).get({
      security: [
        {
          scheme: "bearer",
          type: "http"
        }
      ],
      url: "/umbraco/relewisedashboard/api/v1",
      ...t
    });
  }
  static contentExport(t) {
    return ((t == null ? void 0 : t.client) ?? x).post({
      security: [
        {
          scheme: "bearer",
          type: "http"
        }
      ],
      url: "/umbraco/relewisedashboard/api/v1",
      ...t
    });
  }
}
var ee = Object.defineProperty, te = Object.getOwnPropertyDescriptor, b = (e, t, r, i) => {
  for (var l = i > 1 ? void 0 : i ? te(t, r) : t, o = e.length - 1, a; o >= 0; o--)
    (a = e[o]) && (l = (i ? a(t, r, l) : a(l)) || l);
  return i && l && ee(t, r, l), l;
};
let p = class extends W {
  constructor() {
    super(...arguments), this.exportLoading = !1, this.unhandledError = !1, this.relewiseNotAddedToUmbracoBuilder = !1, this.success = null, this.configuration = null, this.errorMessage = null;
  }
  async connectedCallback() {
    super.connectedCallback(), await this.init();
  }
  async exportContent({ permanentlyDelete: e = !1 } = {}) {
    this.exportLoading = !0, this.errorMessage = "", this.success = "";
    try {
      await T.contentExport({ throwOnError: !0, query: { permanentlyDelete: e } }), this.exportLoading = !1, this.success = "Content was successfully exported to Relewise", this.errorMessage = "";
    } catch {
      this.exportLoading = !1, this.success = "", this.errorMessage = "Unexpected error while exporting data happened";
    }
  }
  async exportContentPermanentlyDelete() {
    confirm("Are you sure, you want to perform an full export and remove deleted content items?") && (this.exportLoading = !0, this.errorMessage = "", this.success = "", await this.exportContent({ permanentlyDelete: !0 }));
  }
  async init() {
    try {
      const e = await T.configuration({ throwOnError: !0 });
      console.log("Configuration result", e), e.response.status === 200 ? (this.unhandledError = !1, this.configuration = e.data) : e.response.status === 403 ? this.relewiseNotAddedToUmbracoBuilder = !0 : this.unhandledError = !0;
    } catch (e) {
      console.error("Error fetching Relewise configuration:", e), this.unhandledError = !0;
    }
  }
  render() {
    return u`
      <section id="relewise-dashboard">
          <uui-box headline="Welcome to the Relewise dashboard">
             <span>
                You can perform various operations, e.g. Export all your content to Relewise.<br />
                It's also possible to see the settings configured for Relewise directly here.
            </span>
            <div class="button-box">
                <uui-button 
                    type="button" 
                    label="Export content" 
                    look="primary" 
                    @click=${() => this.exportContent()} 
                    ?disabled=${this.exportLoading || this.unhandledError || this.relewiseNotAddedToUmbracoBuilder || this.configuration && this.configuration.factoryFailed}>
                    Export content
                </uui-button>

                <uui-button 
                    type="button" 
                    label=" Export content and remove old data" 
                    look="primary" color="danger" 
                    @click=${() => this.exportContentPermanentlyDelete()} 
                    ?disabled=${this.exportLoading || this.unhandledError || this.relewiseNotAddedToUmbracoBuilder || this.configuration && this.configuration.factoryFailed}>
                    Export content and remove old data
                </button>
            </div>
            ${this.success ? u`<div class="mt-10" style="color: var(--uui-palette-forest-green);">${this.success}</div>` : ""}
            ${this.errorMessage ? u`<div class="mt-10 relewise-error">${this.errorMessage}</div>` : ""}
          </uui-box>

          <uui-box headline="Settings">

            ${this.unhandledError ? u`<div class="relewise-error">Unexpected error occurred. Please check the response on the XHR request or check logs.</div>` : ""}
            
            ${this.relewiseNotAddedToUmbracoBuilder ? u`
                                <span class="relewise-error">Relewise has not been added to the Umbraco builder. In 'Startup.cs' locate the 'services.AddUmbraco(...)'-method:</span><br />
                <pre>
services.AddUmbraco(_env, _config)
	.AddBackOffice()
	.AddWebsite()
	.AddComposers()
	.AddRelewise()
	.Build();
...
</pre>
                Note the '.AddRelewise()' method-call above - insert that and re-run the site.` : ""}
            
            ${this.configuration && this.configuration.factoryFailed ? u`
                <div>
                    <span class="error">No settings have been configured. Please check your call to the 'services.AddRelewise(options => { /* options goes here */ })'-method in 'Startup.cs':</span><br />
                    <pre>
public void ConfigureServices(IServiceCollection services)
{
	services.AddRelewise(options => options.ReadFromConfiguration(_config));
...			
}</pre>
                    <span>Which then reads from the following configuration in appsettings.json:</span>
                    <pre>
"Relewise": {
    "DatasetId": "&lt;your dataset id here&gt;",
    "ApiKey": "&lt;your master api key here&gt;",
    "ServerUrl": "&lt;your server url here&gt;",
    "Timeout": "00:00:05"
}
</pre>
                    <span>Error message from server:</span>
                    <pre>${this.configuration.errorMessage}</pre>
                    <span>You can read more about configuring Relewise here: <a href="https://github.com/Relewise/relewise-sdk-csharp-extensions" target="_blank">https://github.com/Relewise/relewise-sdk-csharp-extensions</a></span>
                </div>
            ` : ""}

            ${this.configuration && this.configuration.trackedContentTypes ? u`<div>
                    Tracked content types are:
                    ${this.configuration && this.configuration.trackedContentTypes.length > 0 ? u`<strong>${this.configuration.trackedContentTypes.join(", ")}</strong>` : ""}
                    ${this.configuration && this.configuration.trackedContentTypes.length === 0 ? u`<span style="font-style: italic;">No tracked content type have been configured</span>` : ""}
                   
                    <div style="font-style: italic; color: #666;">(Page views on these content types are automatically being tracked to Relewise by the RelewiseContentMiddleware)</div>
                    ${this.configuration && this.configuration.trackedContentTypes.length > 0 && !this.configuration.contentMiddlewareEnabled ? u`
                        <div style="font-style: italic; color: red;">To have Page views being tracked automatically to Relewise, please ensure a call to the 'TrackContentViews()'-method in 'Startup.cs':</div>
                                            <pre>
app.UseUmbraco()
	.WithMiddleware(u =>
	{
		u.UseBackOffice();
		u.UseWebsite();
		u.TrackContentViews();
	})
	.WithEndpoints(u =>
	{
		u.UseInstallerEndpoints();
		u.UseBackOfficeEndpoints();
		u.UseWebsiteEndpoints();
	});</pre>
                </div>` : ""}
    
                    ` : ""}

                  ${this.configuration && this.configuration.exportedContentTypes ? u` <div style="margin-top: 20px;">
                    Exported content types are:
                    ${this.configuration && this.configuration.exportedContentTypes.length > 0 ? u`<strong>${this.configuration.exportedContentTypes.join(", ")}</strong>` : ""}
                    ${this.configuration && this.configuration.exportedContentTypes.length === 0 ? u`<span style="font-style: italic;">No exported content type have been configured</span>` : ""}
                    <div style="font-style: italic; color: #666;">(Any publish on these content types will automatically export the data to Relewise)</div>
                </div>` : ""}

 <div >
     <hr style="margin-top: 30px; margin-bottom: 30px; border-color: var(--uui-color-divider-standalone);" />
     <span>Find more information about the Umbraco integration here: <a href="https://github.com/Relewise/relewise-integrations-umbraco" target="_blank">https://github.com/Relewise/relewise-integrations-umbraco</a></span>
 </div>

          </uui-box>

          ${this.configuration && this.configuration.named ? u`
          <uui-box headline="Clients">

        <uui-table>
            <uui-table-head>
                <uui-table-head-cell>Name</uui-table-head-cell>
                <uui-table-head-cell>Dataset Id</uui-table-head-cell>
                <uui-table-head-cell>Server Url</uui-table-head-cell>
                <uui-table-head-cell>Timeout (seconds)</uui-table-head-cell>
            </uui-table-head>
                ${this.configuration.named.map((e) => u`
                    <uui-table-row>
                        <uui-table-cell>
                            <strong>${e.name}</strong>
                        </uui-table-cell>
                        <uui-table-cell></uui-table-cell>
                        <uui-table-cell></uui-table-cell>
                        <uui-table-cell></uui-table-cell>
                    </uui-table-row>
                    <uui-table-row>
                        <uui-table-cell>
                            <span class="pl-20">Tracker</span>
                        </uui-table-cell>
                        <uui-table-cell>${e.tracker.datasetId}</uui-table-cell>
                        <uui-table-cell>${e.tracker.serverUrl ? e.tracker.serverUrl : "(default)"}</uui-table-cell>
                        <uui-table-cell>${e.tracker.timeout}</uui-table-cell>
                    </uui-table-row>
                    <uui-table-row>
                        <uui-table-cell>
                            <span class="pl-20">Recommender</span>
                        </uui-table-cell>
                        <uui-table-cell>${e.recommender.datasetId}</uui-table-cell>
                        <uui-table-cell>${e.recommender.serverUrl ? e.recommender.serverUrl : "(default)"}</uui-table-cell>
                        <uui-table-cell>${e.recommender.timeout}</uui-table-cell>
                    </uui-table-row>
                    <uui-table-row>
                        <uui-table-cell>
                            <span class="pl-20">Searcher</span>
                        </uui-table-cell>
                        <uui-table-cell>${e.searcher.datasetId}</uui-table-cell>
                        <uui-table-cell>${e.searcher.serverUrl ? e.searcher.serverUrl : "(default)"}</uui-table-cell>
                        <uui-table-cell>${e.searcher.timeout}</uui-table-cell>
                    </uui-table-row>
                    <uui-table-row>
                        <uui-table-cell>
                            <span class="pl-20">Search Administrator</span>
                        </uui-table-cell>
                        <uui-table-cell>${e.searchAdministrator.datasetId}</uui-table-cell>
                        <uui-table-cell>${e.searchAdministrator.serverUrl ? e.searchAdministrator.serverUrl : "(default)"}</uui-table-cell>
                        <uui-table-cell>${e.searchAdministrator.timeout}</uui-table-cell>
                    </uui-table-row>
                    <uui-table-row>
                        <uui-table-cell>
                            <span class="pl-20">Analyzer</span>
                        </uui-table-cell>
                        <uui-table-cell>${e.analyzer.datasetId}</uui-table-cell>
                        <uui-table-cell>${e.analyzer.serverUrl ? e.analyzer.serverUrl : "(default)"}</uui-table-cell>
                        <uui-table-cell>${e.analyzer.timeout}</uui-table-cell>
                    </uui-table-row>
                    <uui-table-row>
                        <uui-table-cell>
                            <span class="pl-20">Data Accessor</span>
                        </uui-table-cell>
                        <uui-table-cell>${e.dataAccessor.datasetId}</uui-table-cell>
                        <uui-table-cell>${e.dataAccessor.serverUrl ? e.dataAccessor.serverUrl : "(default)"}</uui-table-cell>
                        <uui-table-cell>${e.dataAccessor.timeout}</uui-table-cell>
                    </uui-table-row>
            `)}
        </uui-table>
      </uui-box>
      ` : ""}

          
      </section>
    `;
  }
};
p.styles = N`
    #relewise-dashboard {
        display: flex;
        flex-direction: column;
        grid-gap: var(--uui-size-10);
        padding: var(--uui-size-layout-1);
    }

    .button-box {
        margin-top: var(--uui-size-8);
        display: flex;
        justify-content: space-between;
    }

    .mt-10 {
        margin-top: 10px;
    }
    
    .relewise-error {
        color: var(--uui-color-invalid);
    }

  `;
b([
  f()
], p.prototype, "exportLoading", 2);
b([
  f()
], p.prototype, "unhandledError", 2);
b([
  f()
], p.prototype, "relewiseNotAddedToUmbracoBuilder", 2);
b([
  f()
], p.prototype, "success", 2);
b([
  f()
], p.prototype, "configuration", 2);
b([
  f()
], p.prototype, "errorMessage", 2);
p = b([
  D("relewise-dashboard")
], p);
const le = (e) => {
  e.consumeContext(z, (t) => {
    if (t) {
      var r = t.getOpenApiConfiguration();
      x.setConfig({
        auth: r.token,
        baseUrl: r.base,
        credentials: r.credentials
      }), x.interceptors.request.use(async (i, l) => {
        const o = await t.getLatestToken();
        return i.headers.set("Authorization", `Bearer ${o}`), i;
      });
    }
  });
}, oe = (e, t) => {
  console.log("Goodbye from Relewise 👋");
};
export {
  p as RelewiseDashboardElement,
  le as onInit,
  oe as onUnload
};
//# sourceMappingURL=entrypoint-qWhUvznE.js.map
