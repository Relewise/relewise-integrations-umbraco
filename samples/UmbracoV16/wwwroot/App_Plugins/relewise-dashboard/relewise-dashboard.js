import { css as m, customElement as n, html as p } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement as c } from "@umbraco-cms/backoffice/lit-element";
var d = Object.getOwnPropertyDescriptor, u = (t, o, i, l) => {
  for (var e = l > 1 ? void 0 : l ? d(o, i) : o, r = t.length - 1, a; r >= 0; r--)
    (a = t[r]) && (e = a(e) || e);
  return e;
};
let s = class extends c {
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
s = u([
  n("relewise-dashboard")
], s);
export {
  s as RelewiseElement
};
//# sourceMappingURL=relewise-dashboard.js.map
