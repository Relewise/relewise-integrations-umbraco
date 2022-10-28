function relewiseDashboardController(relewiseDashboardResources) {
    var vm = this;
    vm.exportLoading = false;
    vm.relewiseNotAddedToUmbracoBuilder = false;
    vm.unhandledError = false;
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
        const confirmed = confirm("Are you sure, you want to perform an full export and remove deleted content items?");
        if (!confirmed) {
            return;
        }

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
                vm.unhandledError = false;
                vm.configuration = response.data;
            } else if (response.status === 403) {
                vm.relewiseNotAddedToUmbracoBuilder = true;
            } else {
                vm.unhandledError = true;
            }
        }, (response) => {
            if (response && response.status === 403) {
                vm.relewiseNotAddedToUmbracoBuilder = true;
            } else {
                vm.unhandledError = true;
            }
        });
    }

    init();
};

angular
    .module("umbraco")
    .controller("relewiseDashboardController", relewiseDashboardController);