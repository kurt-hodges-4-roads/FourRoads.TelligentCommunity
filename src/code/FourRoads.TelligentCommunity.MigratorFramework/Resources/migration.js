﻿(function (j, global) {
    var spinner = '<span class="ui-loading" width="48" height="48"></span>',
        api = {
            register: function(options) {
                var processingTemplate = j.telligent.evolution.template.compile(options.processingTemplate);
                var startTemplate = j.telligent.evolution.template.compile(options.startTemplate);

                function updateStatus() {

                    j.telligent.evolution.post({
                        url: options.statusUrl,
                        data: null,
                        success: function(response) {
                            j('#' + options.titleId).text(response.State);
                            switch (response.State) {
                            case 'Running':
                            case 'Finished':
                                {
                                    var rowsProcessingTimeAvg = parseFloat(response.RowsProcessingTimeAvg);
                                    if (rowsProcessingTimeAvg > 0) {
                                        response.RowsProcessingTimeAvg =
                                            (1 / (rowsProcessingTimeAvg / 10000000) * 60).toFixed(3);

                                        response["Action"] = response.State == 'Running' ? 'Cancel' : 'Reset';

                                        j('#' + options.processingArea)
                                            .html(processingTemplate({ response: response }));

                                        j('#' + options.actionLink).click(function(e) {
                                            e.preventDefault();
                                            j(this).hide();

                                            var cancelUrl = options.cancelCurrentJobUrl;
                                            var resetUrl = options.resetUrl;

                                            j('#' + options.processingArea).html(j(spinner));

                                            j.telligent.evolution.post({
                                                url: response.State == 'Running' ? cancelUrl : resetUrl,
                                                data: null
                                            });
                                        });
                                    }
                                }
                                break;

                            case 'Ready':
                                {
                                    j('#' + options.processingArea)
                                        .html(startTemplate({ response: { Action: 'Start' } }));

                                    j('#' + options.actionLink).click(function(e) {
                                        e.preventDefault();

                                        j(this).hide();
                                        j('#' + options.processingArea).html(j(spinner));

                                        j.telligent.evolution.post({
                                            url: options.startUrl,
                                            data: null
                                        });
                                    });
                                }
                                break;

                            default:
                                {
                                    j('#' + options.processingArea).html(j(spinner));
                                }
                                break;
                            }
                        }
                    });
                }

                setInterval(updateStatus, 10000);

                updateStatus();
            }
        };

    $.fourroads = $.fourroads || {};
    $.fourroads.migration = api;

})(jQuery, window);