angular.module("umbraco.resources")
    .factory("relewiseDashboardResources",
        function ($http) {
            return {
                exportContent: function () {
                    return $http.post("/umbraco/backoffice/Relewise/DashboardApi/ContentExport");
                },
                getConfiguration: function () {
                    return $http.get("/umbraco/backoffice/Relewise/DashboardApi/Configuration");
                }
            };
        });