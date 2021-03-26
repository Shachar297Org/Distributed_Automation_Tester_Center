// Please see documentation at https://docs.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

facadeData = [{
    name: "CPUUtilization",
    data: []
},
{
    name: "MemoryUtilization",
    data: []
}];

deviceData = [{
    name: "CPUUtilization",
    data: []
},
{
    name: "MemoryUtilization",
    data: []
}];

processingData = [{
    name: "CPUUtilization",
    data: []
},
{
    name: "MemoryUtilization",
    data: []
}];

var options = {
    chart: {
        height: 380,
        type: 'line',
        zoom: {
            enabled: false
        },
        toolbar: {
            show: false
        }
    },
    colors: ['#556ee6', '#34c38f'],
    dataLabels: {
        enabled: false,
    },
    stroke: {
        width: [3, 3],
        curve: 'straight'
    },
    series: deviceData,
    title: {
        text: 'Max CPU & Memory utilization',
        align: 'left',
        style: {
            fontWeight: '500',
        },
    },
    grid: {
        row: {
            colors: ['transparent', 'transparent'], // takes an array which will be repeated on columns
            opacity: 0.2
        },
        borderColor: '#f1f1f1'
    },
    markers: {
        style: 'inverted',
        size: 6
    },
    xaxis: {       
        //type: "datetime",
        categories: [],
        title: {
            text: 'Time'
        },
        labels: {
            format: 'HH:mm',
        }
    },
    yaxis: {
        title: {
            text: 'Load Percent %'
        },
        min: 0,
        max: 120
    },
    legend: {
        position: 'top',
        horizontalAlign: 'right',
        floating: true,
        offsetY: -25,
        offsetX: -5
    },
    responsive: [{
        breakpoint: 600,
        options: {
            chart: {
                toolbar: {
                    show: false
                }
            },
            legend: {
                show: false
            },
        }
    }]
}

var chartExists = document.querySelector('#line_chart_datalabel') !== null;

if (chartExists) {
    var chart = new ApexCharts(
        document.querySelector("#line_chart_datalabel"),
        options
    );

    chart.render();
} else {

    var deviceOptions = JSON.parse(JSON.stringify(options));    
    deviceOptions.series = deviceData;

    var deviceChart = new ApexCharts(
        document.querySelector("#device_chart"),
        deviceOptions
    );

    deviceChart.render();

    var facadeOptions = JSON.parse(JSON.stringify(options));
    facadeOptions.series = facadeData;

    var facadeChart = new ApexCharts(
        document.querySelector("#facade_chart"),
        facadeOptions
    );

    facadeChart.render();

    var processingOptions = JSON.parse(JSON.stringify(options));
    processingOptions.series = processingData;

    var processingChart = new ApexCharts(
        document.querySelector("#processing_chart"),
        processingOptions
    );

    processingChart.render();
}


// The following sample code uses TypeScript and must be compiled to JavaScript
// before a browser can execute it.
var __awaiter = (this && this.__awaiter) || function (thisArg, _arguments, P, generator) {
    return new (P || (P = Promise))(function (resolve, reject) {
        function fulfilled(value) { try { step(generator.next(value)); } catch (e) { reject(e); } }
        function rejected(value) { try { step(generator["throw"](value)); } catch (e) { reject(e); } }
        function step(result) { result.done ? resolve(result.value) : new P(function (resolve) { resolve(result.value); }).then(fulfilled, rejected); }
        step((generator = generator.apply(thisArg, _arguments || [])).next());
    });
};

const connection = new signalR.HubConnectionBuilder()
    .withUrl("/tcHub")
    .build();

function getHHmm(timeStr) {
    var d = new Date(timeStr);
    
    var m = timeStr.split('T')[1];

    var h = m.split(':')[0]
    var m = m.split(':')[1]

    var label = h + ':' + m;

    return label;
}

var times = [];

function updateChartData(message, num) {
    var label = getHHmm(message.cpuUtilization[0].time);

    deviceData[0].data.push(message.cpuUtilization[0].value);
    deviceData[1].data.push(message.memoryUtilization[0].value);

    facadeData[0].data.push(message.cpuUtilization[1].value);
    facadeData[1].data.push(message.memoryUtilization[1].value);

    processingData[0].data.push(message.cpuUtilization[2].value);
    processingData[1].data.push(message.memoryUtilization[2].value);

    if (chartExists) {
        
        chart.opts.xaxis.categories.push(label);

        chart.updateOptions({
            xaxis: {
                categories: chart.opts.xaxis.categories
            }
        });

        if (num == 0) {
            chart.updateSeries(deviceData);
        } else if (num == 1) {
            chart.updateSeries(facadeData);
        } else {
            chart.updateSeries(processingData);
        }
    } else {
        
        deviceChart.opts.xaxis.categories.push(label);
        deviceChart.updateOptions({
            xaxis: {
                categories: deviceChart.opts.xaxis.categories
            }
        });
        deviceChart.updateSeries(deviceData);

        facadeChart.opts.xaxis.categories.push(label);
        facadeChart.updateOptions({
            xaxis: {
                categories: facadeChart.opts.xaxis.categories
            }
        });
        facadeChart.updateSeries(facadeData);

        processingChart.opts.xaxis.categories.push(label);
        processingChart.updateOptions({
            xaxis: {
                categories: processingChart.opts.xaxis.categories
            }
        });
        processingChart.updateSeries(processingData);
    }


}

connection.on('awsData', function (message) {

    var num = 0;
    $('.nav').children("li").each(function (index) {
        if ($(this).find('a').is('a.active')) {
            num = index;
        }
    });

    if (message.cpuUtilization != null) {
        updateChartData(message, num);
    }

    $('#eventsInRds').text(message.eventsInRDS);

});

connection.on('stageData', function (message) {

    $('#stage').text(message.stage);
    $('#devicesFinished').text(message.devicesNumberFinished);

    if (!chartExists && message.time != undefined && message.time != null) {
        dateToCheck = new Date(message.time)
        if (dateToCheck.getFullYear() == 1 && message.stage == 'GET_RESULTS') {
            return;
        }

        var time = getHHmm(message.time);

        var textSelector = '#timeBlock' + message.stageIdx + ' #stageTime';
        $(textSelector).text(time);

        var blockSelector = '#timeBlock' + message.stageIdx;
        $(blockSelector).css('display', 'inline-block');

        $('.verti-timeline li .active').removeClass('active');

        var selector = '.verti-timeline li:eq(' + (message.stageIdx-1) + ')';
        $(selector).addClass('active');
    }


});

connection.on('agentsData', function (agentsData) {

    for (var i = 0; i < agentsData.length; i++) {
        var agentClass = '.agent-' + i;
        $(agentClass + " #agentClientsRunning").text(agentsData[i].clientsNumber);
        $(agentClass + " #agentsServersRunning").text(agentsData[i].serversNumber);
        $(agentClass + " #agentStatus").text(agentsData[i].status);

        $(agentClass + " #agentDevicesNumber").text(agentsData[i].devices.length);    

    }

});


// We need an async function in order to use await, but we want this code to run immediately,
// so we use an "immediately-executed async function"
(() => __awaiter(this, void 0, void 0, function* () {
    try {
        yield connection.start();
    }
    catch (e) {
        console.error(e.toString());
    }
}))();


$(document).ready(function () {

    var awsData = model.progressData.awsMetricsData;
    for (var i = 0; i < awsData.length; i++) {
        updateChartData(awsData[i], 0);        
    } 

    $('#resetCenter').on('click', function () {

        $.get("/ui/Reset/",
            function (data) {
                window.location.reload(true);
            });
        
    });

    
    $('#startScenario').on('click', function () {        

        var scenarioName = $(this).closest('tr').find(".scenarioName").text();
        $('#progressScenarioName').text(scenarioName);

        $(this).closest('tr').find(".scenarioStatus").text('EXECUTING');
        $('#progressScenarioName').text(scenarioName);

        var sid = $("#scenarios tr").index($(this).closest('tr')) - 1; // table row ID 
        $.get("/ui/StartScenario/" + sid);

        //window.location.reload(true);
    });

    $('#stopScenario').on('click', function () {

        var sid = $("#scenarios tr").index($(this).closest('tr')) - 1; // table row ID
        $.get("/ui/StopScenario",
            function (data) {
                window.location.reload(true);
            });       
    }); 

    $('#stopExecution').on('click', function () {

        var sid = $("#scenarios tr").index($(this).closest('tr')) - 1; // table row ID
        $.get("/ui/StopScenario?action=Progress",
            function (data) {
                window.location.reload(true);
            });
        
    });

    $('#center').on('click', function () {
        
        $.get("/ui/ShowCenterSettings",
            function (data) {
                $('#centerSettingsModal .modal-body').html(data);
            });

        $("#centerSettingsModal").modal("show");
    });


    $('#addAgentButton').on('click', function () {
        $.get("/ui/ShowAddAgent",
            function (data) {
                $('#addAgent .modal-body').html(data);
            });

        $("#addAgent").modal("show");
    });

    $('#agentSettings').on('click', function () {
        var trid = $("#agents tr").index($(this).closest('tr')) - 1; // table row ID 
        $.get("/ui/ShowAddAgent/" + trid,
            function (data) {
                $('#addAgent .modal-body').html(data);
            });

        $("#addAgent").modal("show");
    });

    $('#addScenarioButton').on('click', function () {
        $.get("/ui/ShowAddScenario",
            function (data) {
                $('#addScenario .modal-body').html(data);
            });

        $("#addScenario").modal("show");
    });

    $('#scenarioSettings').on('click', function () {
        var trid = $("#scenarios tr").index($(this).closest('tr')) - 1; // table row ID 
        $.get("/ui/ShowAddScenario/" + trid,
            function (data) {
                $('#addScenario .modal-body').html(data);
            });

        $("#addScenario").modal("show");
    });

    $("#StopAws").click(function () {
        if ($(this).is(':checked')) {
            $('#MinutesServicesStopped').removeAttr("disabled");
            $('#Services').removeAttr("disabled");
        } else {
            $('#MinutesServicesStopped').attr("disabled");
            $('#Services').attr("disabled");
        }
        
    });


    $("#deviceView").click(function () {
        chart.updateSeries(deviceData);

        $('.nav .nav-item a.active').removeClass('active');
        $(this).addClass('active');
    });

    $("#facadeView").click(function () {
        chart.updateSeries(facadeData);

        $('.nav .nav-item a.active').removeClass('active');
        $(this).addClass('active');
    });

    $("#processingView").click(function () {
        chart.updateSeries(processingData);

        $('.nav .nav-item a.active').removeClass('active');
        $(this).addClass('active');
    });
    
});
