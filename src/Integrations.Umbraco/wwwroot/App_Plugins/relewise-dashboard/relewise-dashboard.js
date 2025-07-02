import { css as m, customElement as n, html as p } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement as d } from "@umbraco-cms/backoffice/lit-element";
var b = Object.getOwnPropertyDescriptor, c = (t, o, i, l) => {
  for (var e = l > 1 ? void 0 : l ? b(o, i) : o, r = t.length - 1, a; r >= 0; r--)
    (a = t[r]) && (e = a(e) || e);
  return e;
};
let s = class extends d {
  render() {
    return p`
      <uui-box headline="Welcome">
        <p>This is a custom dashboard in Umbraco v16.</p>
      </uui-box>
    `;
  }
};
s.styles = m`
    :host {
      display: block;
      padding: 24px;
    }
  `;
s = c([
  n("relewise-dashboard")
], s);
export {
  s as RelewiseDashboardElement
};
//# sourceMappingURL=relewise-dashboard.js.map
