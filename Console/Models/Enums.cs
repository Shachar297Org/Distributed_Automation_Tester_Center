using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace TestCenterConsole.Models
{

    public enum DeviceData
    {
        commands,
        events
    }

    public enum BiotEnvironment
    {
        INT,
        DEV,
        STAGE
    }

    public enum InsertionStrategy
    {
        [Display(Name = "Only insert new")]
        union,

        [Display(Name = "Delete all & insert new")]
        all_new,

        [Display(Name = "Delete only missing & insert new")]
        intersect
    }

    public enum AWSServices
    {
        Device,
        Facade,
        Processing
    }

    public enum Stage
    {
        NOT_STARTED,
        INIT,
        AGENTS_CONNECT,
        DISTRIBUTE_DEVICES,
        RUN_DEVICES,
        GET_RESULTS,
        FINISHED
    }
}
