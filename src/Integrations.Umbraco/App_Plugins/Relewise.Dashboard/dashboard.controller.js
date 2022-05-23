function relewiseDashboardController(relewiseDashboardResources) {
    var vm = this;
    vm.exportLoading = false;
    vm.exportContent = function () {
        vm.exportLoading = true;
        vm.errorMessage = "";
        vm.success = "";
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
    }

    init();
};

angular
    .module("umbraco")
    .controller("relewiseDashboardController", relewiseDashboardController);