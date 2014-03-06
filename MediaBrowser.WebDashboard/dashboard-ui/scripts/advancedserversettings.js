﻿(function ($, document, window) {

    function loadPage(page, config, systemInfo) {

        if (systemInfo.SupportsNativeWebSocket) {

            $('#fldWebSocketPortNumber', page).hide();
        } else {
            $('#fldWebSocketPortNumber', page).show();
        }

        $('#txtWebSocketPortNumber', page).val(config.LegacyWebSocketPortNumber);

        $('#txtPortNumber', page).val(config.HttpServerPortNumber);

        $('#txtDdns', page).val(config.WanDdns || '');

        $('#chkEnableUpnp', page).checked(config.EnableUPnP).checkboxradio('refresh');

        Dashboard.hideLoadingMsg();
    }

    $(document).on('pageshow', "#advancedServerSettingsPage", function () {

        Dashboard.showLoadingMsg();

        var page = this;

        var promise1 = ApiClient.getServerConfiguration();

        var promise2 = ApiClient.getSystemInfo();

        $.when(promise1, promise2).done(function (response1, response2) {

            loadPage(page, response1[0], response2[0]);

        });

    });

    window.AdvancedServerSettingsPage = {

        onSubmit: function () {
            Dashboard.showLoadingMsg();

            var form = this;

            ApiClient.getServerConfiguration().done(function (config) {

                config.LegacyWebSocketPortNumber = $('#txtWebSocketPortNumber', form).val();
                config.HttpServerPortNumber = $('#txtPortNumber', form).val();
                config.EnableUPnP = $('#chkEnableUpnp', form).checked();

                config.WanDdns = $('#txtDdns', form).val();

                ApiClient.updateServerConfiguration(config).done(Dashboard.processServerConfigurationUpdateResult);
            });

            // Disable default form submission
            return false;
        }

    };

})(jQuery, document, window);
