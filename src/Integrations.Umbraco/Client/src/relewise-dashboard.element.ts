import { html, css, customElement, state } from '@umbraco-cms/backoffice/external/lit';
import { UmbLitElement } from '@umbraco-cms/backoffice/lit-element';
import { RelewiseDashboardService } from './api';

@customElement('relewise-dashboard')
export class RelewiseDashboardElement extends UmbLitElement {

    @state()
    exportLoading: boolean = false;

    @state()
    unhandledError: boolean = false;

    @state()
    relewiseNotAddedToUmbracoBuilder: boolean = false;

    @state()
    success: string | null = null;

    @state()
    configuration: any | null = null;

    @state()
    errorMessage: string | null = null;

    async connectedCallback() {
        super.connectedCallback();

        await this.init();
    }

    async exportContent({ permanentlyDelete = false }: { permanentlyDelete?: boolean } = {}) {
        this.exportLoading = true;
        this.errorMessage = "";
        this.success = "";
        try {
            await RelewiseDashboardService.contentExport({ throwOnError: true, query: { permanentlyDelete } });

            this.exportLoading = false;
            this.success = "Content was successfully exported to Relewise";
            this.errorMessage = "";
        }
        catch (error) {
            this.exportLoading = false;
            this.success = "";
            this.errorMessage = "Unexpected error while exporting data happened";
        }
    }

    async exportContentPermanentlyDelete() {
        const confirmed = confirm("Are you sure, you want to perform an full export and remove deleted content items?");
        if (!confirmed) {
            return;
        }

        this.exportLoading = true;
        this.errorMessage = "";
        this.success = "";

        await this.exportContent({ permanentlyDelete: true });
    }

    async init() {
        try {
            const result = await RelewiseDashboardService.configuration({ throwOnError: true });
            console.log("Configuration result", result);
            if (result.response.status === 200) {
                this.unhandledError = false;
                this.configuration = result.data;
            } else if (result.response.status === 403) {
                this.relewiseNotAddedToUmbracoBuilder = true;
            } else {
                this.unhandledError = true;
            }
        } catch (error) {
            console.error("Error fetching Relewise configuration:", error);
            this.unhandledError = true;
        }
    }

    override render() {
        return html`
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
                    ?disabled=${this.exportLoading || this.unhandledError || this.relewiseNotAddedToUmbracoBuilder || (this.configuration && this.configuration.factoryFailed)}>
                    Export content
                </uui-button>

                <uui-button 
                    type="button" 
                    label=" Export content and remove old data" 
                    look="primary" color="danger" 
                    @click=${() => this.exportContentPermanentlyDelete()} 
                    ?disabled=${this.exportLoading || this.unhandledError || this.relewiseNotAddedToUmbracoBuilder || (this.configuration && this.configuration.factoryFailed)}>
                    Export content and remove old data
                </button>
            </div>
            ${this.success ? html`<div class="mt-10" style="color: var(--uui-palette-forest-green);">${this.success}</div>` : ''}
            ${this.errorMessage ? html`<div class="mt-10 relewise-error">${this.errorMessage}</div>` : ''}
          </uui-box>

          <uui-box headline="Settings">

            ${this.unhandledError ? html`<div class="relewise-error">Unexpected error occurred. Please check the response on the XHR request or check logs.</div>` : ''}
            
            ${this.relewiseNotAddedToUmbracoBuilder ? html`
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
                Note the '.AddRelewise()' method-call above - insert that and re-run the site.`
                : ''}
            
            ${this.configuration && this.configuration.factoryFailed ? html`
                <div>
                    <span class="error">No settings have been configured. Please check your call to the 'services.AddRelewise(options => { /* options goes here */ })'-method in 'Program.cs':</span><br />
                    <pre>
builder.Services.AddRelewise(options => options.ReadFromConfiguration(builder.Configuration));
</pre>
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
            ` : ''}

            ${this.configuration && this.configuration.trackedContentTypes ? html`<div>
                    Tracked content types are:
                    ${this.configuration && this.configuration.trackedContentTypes.length > 0 ? html`<strong>${this.configuration.trackedContentTypes.join(', ')}</strong>` : ''}
                    ${this.configuration && this.configuration.trackedContentTypes.length === 0 ? html`<span style="font-style: italic;">No tracked content type have been configured</span>` : ''}
                   
                    <div style="font-style: italic; color: #666;">(Page views on these content types are automatically being tracked to Relewise by the RelewiseContentMiddleware)</div>
                    ${this.configuration && this.configuration.trackedContentTypes.length > 0 && !this.configuration.contentMiddlewareEnabled ? html`
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
                </div>` : ''}
    
                    ` : ''}

                  ${this.configuration && this.configuration.exportedContentTypes ? html` <div style="margin-top: 20px;">
                    Exported content types are:
                    ${this.configuration && this.configuration.exportedContentTypes.length > 0 ? html`<strong>${this.configuration.exportedContentTypes.join(', ')}</strong>` : ''}
                    ${this.configuration && this.configuration.exportedContentTypes.length === 0 ? html`<span style="font-style: italic;">No exported content type have been configured</span>` : ''}
                    <div style="font-style: italic; color: #666;">(Any publish on these content types will automatically export the data to Relewise)</div>
                </div>` : ''}

 <div >
     <hr style="margin-top: 30px; margin-bottom: 30px; border-color: var(--uui-color-divider-standalone);" />
     <span>Find more information about the Umbraco integration here: <a href="https://github.com/Relewise/relewise-integrations-umbraco" target="_blank">https://github.com/Relewise/relewise-integrations-umbraco</a></span>
 </div>

          </uui-box>

          ${this.configuration && this.configuration.named ? html`
          <uui-box headline="Clients">

        <uui-table>
            <uui-table-head>
                <uui-table-head-cell>Name</uui-table-head-cell>
                <uui-table-head-cell>Dataset Id</uui-table-head-cell>
                <uui-table-head-cell>Server Url</uui-table-head-cell>
                <uui-table-head-cell>Timeout (seconds)</uui-table-head-cell>
            </uui-table-head>
                ${this.configuration.named.map((named: any) => html`
                    <uui-table-row>
                        <uui-table-cell>
                            <strong>${named.name}</strong>
                        </uui-table-cell>
                        <uui-table-cell></uui-table-cell>
                        <uui-table-cell></uui-table-cell>
                        <uui-table-cell></uui-table-cell>
                    </uui-table-row>
                    <uui-table-row>
                        <uui-table-cell>
                            <span class="pl-20">Tracker</span>
                        </uui-table-cell>
                        <uui-table-cell>${named.tracker.datasetId}</uui-table-cell>
                        <uui-table-cell>${named.tracker.serverUrl ? named.tracker.serverUrl : "(default)"}</uui-table-cell>
                        <uui-table-cell>${named.tracker.timeout}</uui-table-cell>
                    </uui-table-row>
                    <uui-table-row>
                        <uui-table-cell>
                            <span class="pl-20">Recommender</span>
                        </uui-table-cell>
                        <uui-table-cell>${named.recommender.datasetId}</uui-table-cell>
                        <uui-table-cell>${named.recommender.serverUrl ? named.recommender.serverUrl : "(default)"}</uui-table-cell>
                        <uui-table-cell>${named.recommender.timeout}</uui-table-cell>
                    </uui-table-row>
                    <uui-table-row>
                        <uui-table-cell>
                            <span class="pl-20">Searcher</span>
                        </uui-table-cell>
                        <uui-table-cell>${named.searcher.datasetId}</uui-table-cell>
                        <uui-table-cell>${named.searcher.serverUrl ? named.searcher.serverUrl : "(default)"}</uui-table-cell>
                        <uui-table-cell>${named.searcher.timeout}</uui-table-cell>
                    </uui-table-row>
                    <uui-table-row>
                        <uui-table-cell>
                            <span class="pl-20">Search Administrator</span>
                        </uui-table-cell>
                        <uui-table-cell>${named.searchAdministrator.datasetId}</uui-table-cell>
                        <uui-table-cell>${named.searchAdministrator.serverUrl ? named.searchAdministrator.serverUrl : "(default)"}</uui-table-cell>
                        <uui-table-cell>${named.searchAdministrator.timeout}</uui-table-cell>
                    </uui-table-row>
                    <uui-table-row>
                        <uui-table-cell>
                            <span class="pl-20">Analyzer</span>
                        </uui-table-cell>
                        <uui-table-cell>${named.analyzer.datasetId}</uui-table-cell>
                        <uui-table-cell>${named.analyzer.serverUrl ? named.analyzer.serverUrl : "(default)"}</uui-table-cell>
                        <uui-table-cell>${named.analyzer.timeout}</uui-table-cell>
                    </uui-table-row>
                    <uui-table-row>
                        <uui-table-cell>
                            <span class="pl-20">Data Accessor</span>
                        </uui-table-cell>
                        <uui-table-cell>${named.dataAccessor.datasetId}</uui-table-cell>
                        <uui-table-cell>${named.dataAccessor.serverUrl ? named.dataAccessor.serverUrl : "(default)"}</uui-table-cell>
                        <uui-table-cell>${named.dataAccessor.timeout}</uui-table-cell>
                    </uui-table-row>
            `)}
        </uui-table>
      </uui-box>
      ` : ''}

          
      </section>
    `;
    }

    static override styles = css`
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
}