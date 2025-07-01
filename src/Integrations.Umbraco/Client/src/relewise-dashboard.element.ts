import { html, css, customElement } from '@umbraco-cms/backoffice/external/lit';
import { UmbLitElement } from '@umbraco-cms/backoffice/lit-element';

@customElement('my-dashboard')
export class MyDashboardElement extends UmbLitElement {
  override render() {
    return html`
      <uui-box headline="Welcome">
        <p>This is a custom dashboard in Umbraco v15.</p>
      </uui-box>
    `;
  }

  static override styles = css`
    :host {
      display: block;
      padding: 24px;
    }
  `;
}