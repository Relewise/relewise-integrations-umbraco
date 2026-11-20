import { html, css, customElement, state } from '@umbraco-cms/backoffice/external/lit';
import { UmbLitElement } from '@umbraco-cms/backoffice/lit-element';
import { umbConfirmModal } from '@umbraco-cms/backoffice/modal';
import { ConfigurationViewModel, RelewiseDashboard } from './api';
import type { ClientOptionsViewObject, DashboardMappingViewModel, DashboardSearchProviderViewModel } from './api/types.gen';

@customElement('relewise-dashboard')
export class RelewiseDashboardElement extends UmbLitElement {
    @state() private loading = true;
    @state() private exportOperation: 'full' | 'cleanup' | null = null;
    @state() private configuration: ConfigurationViewModel | null = null;
    @state() private configurationError: string | null = null;
    @state() private configurationHeading = 'Configuration unavailable';
    @state() private result: string | null = null;
    @state() private exportError: string | null = null;
    @state() private expandedMappings = new Set<string>();
    @state() private expandedProviders = new Set<string>();
    private request?: AbortController;
    private readonly docs = 'https://github.com/Relewise/relewise-integrations-umbraco/blob/HEAD/docs/';
    private readonly logoUrl = new URL('./assets/relewise-logo-blue.svg', import.meta.url).href;

    override connectedCallback() {
        super.connectedCallback();
        void this.loadConfiguration();
    }

    override disconnectedCallback() {
        this.request?.abort();
        this.exportOperation = null;
        super.disconnectedCallback();
    }

    private async loadConfiguration() {
        this.request?.abort();
        const request = this.request = new AbortController();
        this.loading = true;
        this.configuration = null;
        this.configurationError = null;
        try {
            const response = await RelewiseDashboard.configuration({ signal: request.signal });
            if (request.signal.aborted)
                return;
            if (!response.response)
                throw new Error('No configuration response');
            if (!response.response.ok) {
                this.configurationHeading = response.response.status === 401 || response.response.status === 403
                    ? 'Access required' : 'Configuration unavailable';
                this.configurationError = response.response.status === 401
                    ? 'Your session has expired. Sign in to Umbraco and try again.'
                    : response.response.status === 403
                        ? 'Your account does not have permission to access the Relewise dashboard API.'
                        : 'Could not load the integration settings. Try again or check the application logs.';
                return;
            }
            const data = response.data;
            if (data && 'factoryFailed' in data) {
                const failure = data as { integrationRegistered?: boolean; errorMessage?: string };
                this.configurationHeading = failure.integrationRegistered === false ? 'Integration not registered' : 'Configuration needs attention';
                this.configurationError = failure.errorMessage ?? 'Check the Relewise registration and application logs.';
                return;
            }
            if (!data)
                throw new Error('Empty configuration response');
            this.configuration = data;
        } catch {
            if (!request.signal.aborted) {
                this.configurationHeading = 'Configuration unavailable';
                this.configurationError = 'Could not load the integration settings. Try again or check the application logs.';
            }
        } finally {
            if (!request.signal.aborted)
                this.loading = false;
        }
    }

    private get exporting() {
        return this.exportOperation !== null;
    }

    private get canExport() {
        return !this.loading && !this.exporting && !!this.configuration?.mappings.some(mapping => mapping.export);
    }

    private async exportEntities(permanentlyDelete = false) {
        if (!this.canExport)
            return;
        this.exportOperation = permanentlyDelete ? 'cleanup' : 'full';
        this.result = null;
        this.exportError = null;
        const request = this.request = new AbortController();
        try {
            const response = await RelewiseDashboard.contentExport({ query: { permanentlyDelete }, signal: request.signal });
            if (request.signal.aborted)
                return;
            if (!response.response?.ok)
                throw new Error('Export request failed');
            this.result = `${permanentlyDelete ? 'Export and cleanup' : 'Full export'} completed at ${new Date().toLocaleTimeString()}.`;
        } catch {
            if (!request.signal.aborted) {
                this.exportError = `${permanentlyDelete ? 'Export and cleanup' : 'Full export'} could not be completed. Some changes may already have been applied in Relewise. Check the application logs before retrying.`;
            }
        } finally {
            if (!request.signal.aborted)
                this.exportOperation = null;
        }
    }

    private cleanupMappings() {
        return this.configuration?.mappings.filter(mapping => mapping.export && mapping.synchronizeLifecycle && !mapping.parentDocumentTypeAlias) ?? [];
    }

    private externallyManagedMappings() {
        return this.configuration?.mappings.filter(mapping => mapping.export && !mapping.synchronizeLifecycle && !mapping.parentDocumentTypeAlias) ?? [];
    }

    private async confirmCleanup() {
        if (!this.canExport || !this.cleanupMappings().length)
            return;
        try {
            await umbConfirmModal(this, {
                headline: 'Export and permanently delete missing entities?',
                content: html`<p>This exports all mapped published data, then permanently deletes missing entities previously exported by this site through the eligible document types below.</p>
                    <p>Eligible document types: <strong>${this.cleanupMappings().map(mapping => mapping.documentTypeAlias).join(', ')}</strong>.</p>
                    <p>Externally managed entities are retained. Child Variants are refreshed with their Product.</p>
                    <p>Deletion in Relewise cannot be undone through Umbraco's Recycle Bin.</p>`,
                color: 'danger', confirmLabel: 'Export and permanently delete', cancelLabel: 'Cancel'
            });
        } catch {
            return;
        }
        await this.exportEntities(true);
    }

    private renderViewStatus(trackViews: boolean, middlewareEnabled: boolean) {
        const color = !trackViews ? 'default' : middlewareEnabled ? 'positive' : 'warning';
        return html`<uui-tag color=${color}>${!trackViews ? 'Not configured' : middlewareEnabled ? 'Enabled' : 'Middleware missing'}</uui-tag>`;
    }

    private renderOverview(configuration: ConfigurationViewModel) {
        const exports = configuration.mappings.filter(mapping => mapping.export).length;
        const views = configuration.mappings.some(mapping => mapping.trackViews);
        const datasetUrl = configuration.connection?.datasetId
            ? `https://my.relewise.com/${encodeURIComponent(configuration.connection.datasetId)}`
            : null;
        return html`<uui-box class="integration-overview" headline="Relewise integration">
            <div class="integration-header">
                <img class="relewise-logo" src=${this.logoUrl} alt="Relewise" width="194" />
                <div class="integration-links">
                    <a href="https://github.com/Relewise/relewise-integrations-umbraco#getting-started" target="_blank" rel="noopener noreferrer" aria-label="Configuration guide (opens in a new tab)">Configuration guide <span aria-hidden="true">↗</span></a>
                    ${datasetUrl ? html`<uui-button href=${datasetUrl} target="_blank" rel="noopener noreferrer" look="primary" label="Open in My Relewise (opens in a new tab)">Open in My Relewise <span aria-hidden="true">↗</span></uui-button>` : ''}
                </div>
            </div>
            <p class="intro">Review entity mappings, search routing and automatic updates for this site.</p>
            <dl class="overview">
                <div><dt>Dataset ID</dt><dd class="identifier">${configuration.connection?.datasetId ?? 'Unavailable'}</dd></div>
                <div><dt>Server</dt><dd>${configuration.connection?.serverUrl ?? 'Relewise default server'}</dd></div>
                <div><dt>Automatic indexing</dt><dd><uui-tag color=${exports ? 'positive' : 'default'}>${exports ? 'Configured' : 'No exported mappings'}</uui-tag></dd></div>
                <div><dt>Automatic view tracking</dt><dd>${this.renderViewStatus(views, configuration.contentMiddlewareEnabled)}</dd></div>
                <div><dt>Default published-content search</dt><dd>${configuration.defaultPublishedContentProviderAlias ?? 'No Relewise default configured'}</dd></div>
            </dl>
            ${views && !configuration.contentMiddlewareEnabled ? html`<p role="status">Automatic views require the tracking middleware. See <a href=${this.docs + 'entity-mapping.md'} target="_blank" rel="noopener noreferrer">tracking setup</a>.</p>` : ''}
        </uui-box>`;
    }

    private toggleMapping(documentTypeAlias: string) {
        const expanded = new Set(this.expandedMappings);
        if (expanded.has(documentTypeAlias)) {
            expanded.delete(documentTypeAlias);
        } else {
            expanded.add(documentTypeAlias);
        }
        this.expandedMappings = expanded;
    }

    private renderMappingDetails(mapping: DashboardMappingViewModel) {
        return html`<div class="configuration-details" role="region" aria-label=${`Mapping details for ${mapping.documentTypeAlias}`}>
            <div class="configuration-details-grid">
                <section><h3>Fields</h3>
                    ${mapping.autoMap ? html`<p>AutoMap converts supported published properties into Data fields using automatic converter selection.</p>` : ''}
                    ${mapping.fields.length ? html`<div class="table-scroll"><uui-table aria-label=${`Field mappings for ${mapping.documentTypeAlias}`}>
                        <uui-table-head>
                            <uui-table-head-cell>Umbraco property alias</uui-table-head-cell>
                            <uui-table-head-cell>Relewise destination</uui-table-head-cell>
                            <uui-table-head-cell>Conversion</uui-table-head-cell>
                        </uui-table-head>
                        ${mapping.fields.map(field => html`<uui-table-row>
                            <uui-table-cell><code>${field.sourcePropertyAlias}</code></uui-table-cell>
                            <uui-table-cell><code>${field.destination}</code></uui-table-cell>
                            <uui-table-cell>${field.conversion}</uui-table-cell>
                        </uui-table-row>`)}
                    </uui-table></div>` : mapping.autoMap ? '' : mapping.customMapper
                        ? html`<p>Application fields are supplied by the custom mapper.</p>`
                        : mapping.synchronizeLifecycle
                            ? html`<p>Uses the integration's built-in defaults, including the document name as DisplayName. No explicit fields are configured.</p>`
                            : html`<p>No application fields are configured. Source metadata is still maintained; existing entity fields are retained.</p>`}
                    ${!mapping.export ? html`<p>Export is not configured for this mapping; these settings do not send entity updates.</p>` : ''}
                </section>
                <section><h3>Relationships</h3>
                    ${mapping.relationships.length
                        ? html`<ul>${mapping.relationships.map(relationship => html`<li>${relationship}</li>`)}</ul>`
                        : html`<p>No declarative relationships are configured.</p>`}
                </section>
                <section><h3>Custom mapping</h3>
                    ${mapping.customMapperTypeName ? html`<p><code>${mapping.customMapperTypeName}</code></p>` : ''}
                    ${mapping.legacyMapper
                        ? html`<p>A legacy Content mapper supplies application fields through the compatibility API.</p>`
                        : mapping.customMapper
                            ? html`<p>A custom entity mapper runs after automatic or explicit fields and configured relationships.</p>`
                            : html`<p>No custom entity mapper is configured.</p>`}
                    ${mapping.customMapper ? html`<p>Custom mapper output is determined during export.</p>` : ''}
                </section>
                <section><h3>Publication and removal</h3>
                    ${mapping.parentDocumentTypeAlias
                        ? html`<p>Variants are exported with their parent Product. Its mapping determines whether missing Variants are removed.</p>`
                        : mapping.synchronizeLifecycle
                            ? html`<p>Umbraco manages availability and removal in Relewise, following publication, unpublication and deletion of the source document.</p>`
                            : html`<p>Umbraco updates entity data but does not enable, disable or delete the entity. These actions remain under external control.</p>`}
                </section>
            </div>
        </div>`;
    }

    private renderMappings(configuration: ConfigurationViewModel) {
        return html`<uui-box headline="Entity mappings">
            <p>Published data is exported using these mappings. IDs are shared by export, entity references and tracking.</p>
            ${configuration.mappings.length ? html`<div class="table-scroll"><table class="configuration-table" aria-label="Entity mappings">
                <thead><tr>
                    <th scope="col">Document type alias</th>
                    <th scope="col">Relewise entity</th>
                    <th scope="col">ID source</th>
                    <th scope="col">Automatic indexing</th>
                    <th scope="col">Automatic view tracking</th>
                    <th scope="col"><span class="visually-hidden">Mapping details</span></th>
                </tr></thead>
                <tbody>${configuration.mappings.map(mapping => {
                    const expanded = this.expandedMappings.has(mapping.documentTypeAlias);
                    const detailsId = `configuration-details-${encodeURIComponent(mapping.documentTypeAlias)}`;
                    return html`<tr>
                        <th scope="row"><code>${mapping.documentTypeAlias}</code></th>
                        <td>${mapping.parentDocumentTypeAlias ? html`Variant of Product<br /><small>Parent: ${mapping.parentDocumentTypeAlias}</small>` : mapping.entityType}</td>
                        <td>${mapping.idSource}</td>
                        <td><uui-tag color=${mapping.export ? 'positive' : 'default'}>${mapping.export ? 'Configured' : 'Not configured'}</uui-tag></td>
                        <td>${this.renderViewStatus(mapping.trackViews, configuration.contentMiddlewareEnabled)}</td>
                        <td><button class="detail-toggle" type="button" aria-label=${`${expanded ? 'Hide' : 'Show'} mapping details for ${mapping.documentTypeAlias}`} aria-expanded=${expanded} aria-controls=${detailsId} @click=${() => this.toggleMapping(mapping.documentTypeAlias)}>${expanded ? 'Hide details' : 'Details'}</button></td>
                    </tr>
                    <tr ?hidden=${!expanded}><td colspan="6" id=${detailsId}>${expanded ? this.renderMappingDetails(mapping) : ''}</td></tr>`;
                })}</tbody>
            </table></div>` : html`<p>No entity mappings are configured.</p>`}
            <p><a href=${this.docs + 'entity-mapping.md'} target="_blank" rel="noopener noreferrer" aria-label="Entity mapping guide (opens in a new tab)">Entity mapping guide <span aria-hidden="true">↗</span></a></p>
        </uui-box>`;
    }

    private toggleProvider(providerAlias: string) {
        const expanded = new Set(this.expandedProviders);
        if (expanded.has(providerAlias)) {
            expanded.delete(providerAlias);
        } else {
            expanded.add(providerAlias);
        }
        this.expandedProviders = expanded;
    }

    private renderProviderDetails(provider: DashboardSearchProviderViewModel) {
        const unrestrictedContent = provider.entityType === 'Content' && provider.documentTypeAliases.length === 0;
        return html`<div class="configuration-details" role="region" aria-label=${`Provider details for ${provider.alias}`}>
            <dl class="provider-details-grid">
                <div><dt>Document type aliases</dt>
                    <dd>${provider.resolvedDocumentTypeAliases.length ? html`<code>${provider.resolvedDocumentTypeAliases.join(', ')}</code>` : 'No exported document types.'}
                        ${unrestrictedContent ? html`<p>Includes all exported Content mappings.</p>` : ''}
                    </dd>
                </div>
                <div><dt>Published Umbraco page</dt>
                    <dd>${provider.requirePublishedPage
                        ? 'Required.'
                        : 'Optional. Products without a published page may be returned; the caller must handle missing pages.'}</dd>
                </div>
                <div><dt>Displayed at location</dt>
                    <dd><code>${provider.displayedAtLocation}</code>
                        <p>${provider.usesFallbackLocation ? 'Uses the integration\'s fallback label.' : 'Uses an explicitly configured label.'}</p>
                    </dd>
                </div>
                <div><dt>Request customization</dt>
                    <dd>${provider.customRequest
                        ? 'Configured. It may change filters, currency and analytics location for each execution.'
                        : 'Not configured.'}</dd>
                </div>
                <div><dt>Segment-specific queries</dt><dd>${provider.ignoreSegments
                        ? 'Ignore segment selection and execute the ordinary query.'
                        : 'Reject segment-specific queries.'}</dd></div>
            </dl>
        </div>`;
    }

    private renderSearch(configuration: ConfigurationViewModel) {
        return html`<uui-box headline="Search providers">
            <p>Search providers query the same Relewise Dataset. Indexing runs independently through one shared pipeline.</p>
            ${configuration.searchProviders.length ? html`<div class="table-scroll"><table class="configuration-table" aria-label="Search providers">
                <thead><tr>
                    <th scope="col">Provider alias</th>
                    <th scope="col">Entity</th>
                    <th scope="col">Document type aliases</th>
                    <th scope="col">Default published-content search</th>
                    <th scope="col"><span class="visually-hidden">Provider details</span></th>
                </tr></thead>
                <tbody>${configuration.searchProviders.map(provider => {
                    const expanded = this.expandedProviders.has(provider.alias);
                    const detailsId = `provider-details-${encodeURIComponent(provider.alias)}`;
                    const isDefault = configuration.defaultPublishedContentProviderAlias === provider.alias;
                    return html`<tr>
                        <th scope="row"><code>${provider.alias}</code></th>
                        <td>${provider.entityType}</td>
                        <td>${provider.entityType === 'Content' && provider.documentTypeAliases.length === 0
                            ? 'All exported Content types' : html`<code>${provider.resolvedDocumentTypeAliases.join(', ')}</code>`}</td>
                        <td><uui-tag color=${isDefault ? 'positive' : 'default'}>${isDefault ? 'Default' : 'Named only'}</uui-tag></td>
                        <td><button class="detail-toggle" type="button" aria-label=${`${expanded ? 'Hide' : 'Show'} provider details for ${provider.alias}`} aria-expanded=${expanded} aria-controls=${detailsId} @click=${() => this.toggleProvider(provider.alias)}>${expanded ? 'Hide details' : 'Details'}</button></td>
                    </tr>
                    <tr ?hidden=${!expanded}><td colspan="5" id=${detailsId}>${expanded ? this.renderProviderDetails(provider) : ''}</td></tr>`;
                })}</tbody>
            </table></div>` : html`<p>No Relewise search providers are configured. Indexing and tracking can still be used.</p>`}
            ${!configuration.defaultPublishedContentProviderAlias ? html`<p>Ordinary published-content queries retain the site's existing search provider.</p>` : ''}
            <p><a href=${this.docs + 'search.md'} target="_blank" rel="noopener noreferrer" aria-label="Search provider guide (opens in a new tab)">Search provider guide <span aria-hidden="true">↗</span></a></p>
            <aside class="index-statistics-note" role="note" aria-label="Index statistics in Umbraco">
                <uui-icon name="icon-info" aria-hidden="true"></uui-icon>
                <div>
                    <p>Please note: Umbraco's Search dashboard does not receive live index statistics from Relewise.
                        The Relewise status (“Unknown”) and document count (0) are placeholders.
                        They do not indicate that your Dataset is empty or that an export has failed.</p>
                    <a href="/umbraco/section/settings/workspace/search-root">Open Umbraco Search dashboard</a>
                </div>
            </aside>
        </uui-box>`;
    }

    private renderExport() {
        return html`<uui-box headline="Export">
            <p>Export all published mapped data to Relewise. Use this after changing mappings or to refresh existing data. Publication changes are exported automatically.</p>
            <p>Previously exported entities missing from published data are disabled when Umbraco manages their availability. Externally managed entities keep their enabled/disabled state.</p>
            <p>Configured Variants are refreshed with their parent Product. Products managed by Umbraco may also remove missing child Variants during either export.</p>
            <uui-button look="primary" label="Export all mapped entities" .state=${this.exportOperation === 'full' ? 'waiting' : undefined} ?disabled=${!this.canExport} @click=${() => this.exportEntities()}>Export all mapped entities</uui-button>
            ${this.exporting ? html`<p role="status">${this.exportOperation === 'cleanup' ? 'Export and cleanup requested.' : 'Full export requested.'} This can take a while for larger sites. Keep this dashboard open until it finishes.</p>` : ''}
            <div role="status" aria-live="polite">${this.result ? html`<p class="success">${this.result}</p>` : ''}</div>
            ${this.exportError ? html`<p role="alert" class="error">${this.exportError}</p>` : ''}
            ${!this.configuration?.mappings.some(mapping => mapping.export) ? html`<p>Configure an exported entity mapping to enable export.</p>` : ''}
            <details><summary>Maintenance</summary>
                <p>Run a full export and permanently delete entities missing from the published data. Cleanup only applies to entities previously exported by this site through the eligible document types below; it does not clear the Dataset.</p>
                ${this.cleanupMappings().length ? html`<p>Eligible document types: <code>${this.cleanupMappings().map(mapping => mapping.documentTypeAlias).join(', ')}</code>.</p>` : html`<p>No exported mappings permit permanent deletion.</p>`}
                ${this.externallyManagedMappings().length ? html`<p>Retained under external control: <code>${this.externallyManagedMappings().map(mapping => mapping.documentTypeAlias).join(', ')}</code>.</p>` : ''}
                <uui-button look="secondary" color="danger" label="Export and permanently delete missing entities" .state=${this.exportOperation === 'cleanup' ? 'waiting' : undefined} ?disabled=${!this.canExport || !this.cleanupMappings().length} @click=${() => this.confirmCleanup()}>Export and permanently delete missing entities</uui-button>
            </details>
        </uui-box>`;
    }

    private renderConnectionTable(rows: ReadonlyArray<{ configuration: string; name: string; options: ClientOptionsViewObject }>, label: string, showConnection = true) {
        return html`<div class="table-scroll"><uui-table aria-label=${label}>
            <uui-table-head>
                ${showConnection ? html`<uui-table-head-cell>Connection</uui-table-head-cell>` : ''}
                <uui-table-head-cell>Client</uui-table-head-cell>
                <uui-table-head-cell>Dataset ID</uui-table-head-cell>
                <uui-table-head-cell>Server</uui-table-head-cell>
                <uui-table-head-cell>Timeout (seconds)</uui-table-head-cell>
            </uui-table-head>
            ${rows.map(row => html`<uui-table-row>
                ${showConnection ? html`<uui-table-cell>${row.configuration}</uui-table-cell>` : ''}
                <uui-table-cell>${row.name}</uui-table-cell>
                <uui-table-cell class="identifier">${row.options.datasetId}</uui-table-cell>
                <uui-table-cell>${row.options.serverUrl ?? 'Relewise default server'}</uui-table-cell>
                <uui-table-cell>${row.options.timeout}</uui-table-cell>
            </uui-table-row>`)}
        </uui-table></div>`;
    }

    private renderConnections(configuration: ConfigurationViewModel) {
        const connections = configuration.named.flatMap(named => {
            const configurationName = named.isDefault ? 'Default connection' : named.name;
            return [
                { name: 'Tracker', options: named.tracker },
                { name: 'Searcher', options: named.searcher },
                { name: 'Recommender', options: named.recommender },
                { name: 'Data accessor', options: named.dataAccessor },
                { name: 'Search administrator', options: named.searchAdministrator },
                { name: 'Analyzer', options: named.analyzer }
            ].map(client => ({ ...client, configuration: configurationName, isDefault: named.isDefault }));
        });
        const defaultConnections = connections.filter(connection => connection.isDefault);
        const namedConnections = connections.filter(connection => !connection.isDefault);

        return html`<uui-box headline="Connection details">
            <p>These settings are configured through application settings and C# registration. Restart the site after changing them.</p>
            <h3>Default connection</h3>
            <p>Used when application code requests a Relewise client without specifying a name.</p>
            ${defaultConnections.length
                ? this.renderConnectionTable(defaultConnections, 'Default connection', false)
                : html`<p>No default connection is configured.</p>`}
            <h3>Named connections</h3>
            <p>Used when application code requests a Relewise client by its registered name. Each connection inherits default settings unless overridden.</p>
            ${namedConnections.length
                ? this.renderConnectionTable(namedConnections, 'Named connections')
                : html`<p>No named connections are configured.</p>`}
            <details><summary>Connections used by the Umbraco integration</summary>
                <p>These resolved connections are used for export and views (Tracker), search (Searcher), and entity reads (Data accessor). Integration-specific settings can override the default connection, including a longer Tracker timeout for exports.</p>
                ${this.renderConnectionTable(configuration.integrationClients.map(client => ({ ...client, configuration: 'Umbraco integration' })), 'Umbraco integration connections', false)}
            </details>
            <p><a href="https://github.com/Relewise/relewise-sdk-csharp-extensions#reading-configuration" target="_blank" rel="noopener noreferrer" aria-label="SDK configuration (opens in a new tab)">SDK configuration <span aria-hidden="true">↗</span></a> · <a href=${this.docs + 'validation-and-troubleshooting.md'} target="_blank" rel="noopener noreferrer" aria-label="Troubleshooting (opens in a new tab)">Troubleshooting <span aria-hidden="true">↗</span></a></p>
        </uui-box>`;
    }

    private helpLinks() {
        return html`<p><a href="https://github.com/Relewise/relewise-integrations-umbraco#readme" target="_blank" rel="noopener noreferrer">Integration documentation</a> · <a href=${this.docs + 'entity-mapping.md'} target="_blank" rel="noopener noreferrer">Entity mapping and tracking</a></p>`;
    }

    override render() {
        return html`<section id="relewise-dashboard" aria-label="Relewise dashboard">
            ${this.loading ? html`<uui-box headline="Relewise integration"><p role="status"><uui-loader></uui-loader> Loading configuration…</p></uui-box>` : ''}
            ${this.configurationError ? html`<uui-box headline=${this.configurationHeading}><p role="alert" class="error">${this.configurationError}</p><p>Settings are configured in code and application configuration.</p><uui-button look="secondary" label="Reload configuration" @click=${() => this.loadConfiguration()}>Reload configuration</uui-button>${this.helpLinks()}</uui-box>` : ''}
            ${this.configuration ? html`${this.renderOverview(this.configuration)}${this.renderMappings(this.configuration)}${this.renderSearch(this.configuration)}${this.renderExport()}${this.renderConnections(this.configuration)}` : ''}
        </section>`;
    }

    static override styles = css`
        :host { display: block; }
        #relewise-dashboard { display: grid; gap: var(--uui-size-space-5); padding: var(--uui-size-layout-1); }
        p { line-height: 1.6; }
        .integration-overview { border-top: 3px solid #3764e4; }
        .integration-header { display: flex; align-items: center; justify-content: space-between; flex-wrap: wrap; gap: var(--uui-size-space-5); margin-bottom: var(--uui-size-space-5); }
        .relewise-logo { width: 194px; max-width: 100%; height: auto; }
        .integration-links { display: flex; align-items: center; flex-wrap: wrap; gap: var(--uui-size-space-5); }
        .intro { margin-top: 0; }
        .overview { display: grid; grid-template-columns: repeat(auto-fit, minmax(260px, 1fr)); gap: var(--uui-size-space-5); }
        dt { font-weight: 600; margin-bottom: var(--uui-size-space-2); }
        dd { margin: 0; overflow-wrap: anywhere; }
        small { color: var(--uui-color-text-alt); }
        .identifier { overflow-wrap: anywhere; }
        .table-scroll { overflow-x: auto; }
        uui-table { width: 100%; }
        uui-box { min-width: 0; }
        uui-table-cell { vertical-align: top; overflow-wrap: anywhere; }
        details { margin-top: var(--uui-size-space-5); border-top: 1px solid var(--uui-color-border); padding-top: var(--uui-size-space-4); }
        summary { cursor: pointer; font-weight: 600; }
        .configuration-table { width: 100%; border-collapse: separate; border-spacing: 0; border: 1px solid var(--uui-color-divider-standalone); border-radius: var(--uui-border-radius-3); }
        .configuration-table th, .configuration-table td { text-align: left; vertical-align: top; padding: var(--uui-size-space-5); border-bottom: 1px solid var(--uui-color-divider-standalone); }
        .configuration-table thead th { font-weight: 600; }
        .configuration-table tbody th { font-weight: normal; }
        .configuration-table tr[hidden] { display: none; }
        .configuration-table tr:last-child td { border-bottom: 0; }
        .configuration-details { padding: var(--uui-size-space-3); background: var(--uui-color-surface-alt); }
        .configuration-details-grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: var(--uui-size-space-5); }
        .configuration-details-grid section:first-child { grid-column: 1 / -1; }
        .provider-details-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(250px, 1fr)); gap: var(--uui-size-space-5); margin: 0; }
        .provider-details-grid p:last-child { margin-bottom: 0; }
        .index-statistics-note { display: flex; align-items: flex-start; gap: var(--uui-size-space-4); padding: var(--uui-size-space-5); border-left: 3px solid #3764e4; background: var(--uui-color-surface-alt); }
        .index-statistics-note uui-icon { flex-shrink: 0; margin-top: 2px; color: var(--uui-color-interactive); }
        .index-statistics-note p { margin: 0 0 var(--uui-size-space-3); }
        .configuration-details h3 { margin-top: 0; }
        .detail-toggle { font: inherit; cursor: pointer; border: 1px solid var(--uui-color-border); border-radius: var(--uui-border-radius); padding: var(--uui-size-space-3) var(--uui-size-space-4); color: var(--uui-color-interactive); background: var(--uui-color-surface); white-space: nowrap; }
        .detail-toggle:hover { background: var(--uui-color-surface-alt); }
        .detail-toggle:focus-visible { outline: 2px solid var(--uui-color-focus); outline-offset: 2px; }
        .configuration-details li { margin-bottom: var(--uui-size-space-3); line-height: 1.6; }
        .visually-hidden { position: absolute; width: 1px; height: 1px; padding: 0; overflow: hidden; clip-path: inset(50%); white-space: nowrap; }
        @media (max-width: 900px) { .configuration-details-grid { grid-template-columns: minmax(0, 1fr); } }
        h3 { font-size: var(--uui-type-default-size); }
        code { overflow-wrap: anywhere; }
        .error { color: var(--uui-color-danger); }
        .success { color: var(--uui-color-positive); }
        a { color: var(--uui-color-interactive); }
    `;
}
