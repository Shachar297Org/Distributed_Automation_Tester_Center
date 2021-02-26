/*
Template Name: Skote - Responsive Bootstrap 4 Admin Dashboard
Author: Themesbrand
Version: 2.0
Website: https://themesbrand.com/
Contact: themesbrand@gmail.com
File: Apex Chart init js
*/

//  line chart datalabel
   
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
    series: [{
      name: "CPUUtilization",
      data: [26, 24, 32, 36, 33, 31, 33]
    },
    {
      name: "MemoryUtilization",
      data: [14, 11, 16, 12, 17, 13, 12]
    }
    ],
    title: {
      text: 'Max CPU & Memory utilization',
      align: 'left',
      style: {
        fontWeight:  '500',
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
        categories: ['12:01', '12:02', '12:03', '12:04', '12:05', '12:06', '12:07'],
      title: {
        text: 'Time'
      }
    },
    yaxis: {
      title: {
        text: 'Percent %'
      },
      min: 5,
      max: 40
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
  
  var chart = new ApexCharts(
    document.querySelector("#line_chart_datalabel"),
    options
  );
  
  chart.render();

