export const manifests: Array<UmbExtensionManifest> = [
  {
    name: "Custom Welcome Dashboard Entrypoint",
    alias: "CustomWelcomeDashboard.Entrypoint",
    type: "backofficeEntryPoint",
    js: () => import("./entrypoint.js"),
  },
];
