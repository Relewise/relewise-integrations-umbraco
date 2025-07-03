export const manifests: Array<UmbExtensionManifest> = [
  {
    name: "Relewise Dashboard Entrypoint",
    alias: "RelewiseDashboard.Entrypoint",
    type: "backofficeEntryPoint",
    js: () => import("./entrypoint.js"),
  },
];
