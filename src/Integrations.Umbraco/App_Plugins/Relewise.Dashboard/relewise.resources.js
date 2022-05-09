angular.module("umbraco.resources")
    .factory("relewiseDashboardResources",
        function ($http) {
            return {
                getConfiguration: function () {
                    return $http.get("/umbraco/backoffice/Relewise/DashboardApi/GetConfiguration");
                },
                exportContent: function () {
                    return $http.post("/umbraco/backoffice/Relewise/DashboardApi/ContentExport");
                }
            };
        });