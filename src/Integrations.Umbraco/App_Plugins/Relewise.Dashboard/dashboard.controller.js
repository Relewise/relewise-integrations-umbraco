function relewiseDashboardController(relewiseDashboardResources) {
    var vm = this;
    vm.exportLoading = false;
    vm.configurationError = false;
    vm.configuration = null;
    vm.exportContent = function () {
        vm.exportLoading = true;
        vm.errorMessage = "";
        vm.success = "";
        relewiseDashboardResources.exportContent().then(() => {
            vm.exportLoading = false;
            vm.success = "Content was successfully exported to Relewise";
            vm.errorMessage = "";
        }, () => {
            vm.exportLoading = false;
            vm.success = "";
            vm.errorMessage = "Unexpected error while exporting data happened";
        });
    }

    vm.exportContentPermanentlyDelete = function () {
        vm.exportLoading = true;
        vm.errorMessage = "";
        vm.success = "";
        relewiseDashboardResources.exportContentWithDelete().then(() => {
            vm.exportLoading = false;
            vm.success = "Content was successfully exported to Relewise";
            vm.errorMessage = "";
        }, () => {
            vm.exportLoading = false;
            vm.success = "";
            vm.errorMessage = "Unexpected error while exporting data happened";
        });
    }

    function init() {
        relewiseDashboardResources.getConfiguration().then((response) => {
            if (response.status === 200) {
                vm.configurationError = false;
                vm.configuration = response.data;
            } else {
                vm.configurationError = true;
            }
        }, () => {
            vm.configurationError = true;
        });
    }

    init();
};

angular
    .module("umbraco")
    .controller("relewiseDashboardController", relewiseDashboardController);