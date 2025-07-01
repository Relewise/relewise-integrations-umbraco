import { css as n, customElement as i, html as p } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement as d } from "@umbraco-cms/backoffice/lit-element";
var b = Object.getOwnPropertyDescriptor, c = (t, o, m, a) => {
  for (var e = a > 1 ? void 0 : a ? b(o, m) : o, r = t.length - 1, l; r >= 0; r--)
    (l = t[r]) && (e = l(e) || e);
  return e;
};
let s = class extends d {
  render() {
    return p`
      <uui-box headline="Welcome">
        <p>This is a custom dashboard in Umbraco v15.</p>
      </uui-box>
    `;
  }
};
s.styles = n`
    :host {
      display: block;
      padding: 24px;
    }
  `;
s = c([
  i("my-dashboard")
], s);
export {
  s as MyDashboardElement
};
//# sourceMappingURL=relewise-dashboard.js.map
