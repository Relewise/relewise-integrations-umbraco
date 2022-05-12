angular.module("umbraco.resources")
    .factory("relewiseDashboardResources",
        function ($http) {
            return {
                exportContent: function () {
                    return $http.post("/umbraco/backoffice/Relewise/DashboardApi/ContentExport");
                }
            };
        });