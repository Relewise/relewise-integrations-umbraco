function relewiseDashboardController(relewiseDashboardResources) {
    var vm = this;
    vm.exportLoading = false;
    vm.errorMessage = "";
    vm.success = "";
    vm.exportContent = function () {
        vm.exportLoading = true;
        relewiseDashboardResources.exportContent().then(() => {
            vm.exportLoading = false;
            vm.success = "Content was successfully exported to Relewise";
            vm.errorMessage = "";
        }, () => {
            vm.success = "";
            vm.errorMessage = "Unexpected error while exporting data happend";
        });
    }

    function init() {
        relewiseDashboardResources.getConfiguration().then(response => {
            if (response.status === 200) {
                vm.configuration = response.data;
            }
        });
    }

    init();
};

angular
    .module("umbraco")
    .controller("relewiseDashboardController", relewiseDashboardController);