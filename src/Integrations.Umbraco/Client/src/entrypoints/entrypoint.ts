import type {
  UmbEntryPointOnInit,
  UmbEntryPointOnUnload,
} from "@umbraco-cms/backoffice/extension-api";
import { UMB_AUTH_CONTEXT } from '@umbraco-cms/backoffice/auth';
import { client } from '../api/client.gen';

export * from '../relewise-dashboard.element';

// load up the manifests here
export const onInit: UmbEntryPointOnInit = (_host) => {

	_host.consumeContext(UMB_AUTH_CONTEXT, (_auth) => {
		if (!_auth) return;

		var config = _auth.getOpenApiConfiguration();

		client.setConfig({
			auth: config.token,
			baseUrl: config.base,
			credentials: config.credentials,
		});

		client.interceptors.request.use(async (request, _options) => {

			const token = await _auth.getLatestToken();
			request.headers.set('Authorization', `Bearer ${token}`);
			return request;
		});
	});
};
export const onUnload: UmbEntryPointOnUnload = (_host, _extensionRegistry) => {
  console.log("Goodbye from Relewise 👋");
};
