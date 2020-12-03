using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;

namespace TestCenterApp
{
    public class RouteConfig
    {
        public static void RegisterRoutes(RouteCollection routes)
        {
            routes.IgnoreRoute("{resource}.axd/{*pathInfo}");

            routes.MapRoute(
                name: "Default",
                url: "{controller}/{action}/{id}",
                defaults: new { controller = "Center", action = "Index", id = UrlParameter.Optional },
                constraints: new {httpMethod=new HttpMethodConstraint("GET")}
            );
           
            routes.MapRoute(
                name: "Connect",
                url: "{controller}/{action}/{id}",
                defaults: new { controller = "Center", action = "Connect", id = UrlParameter.Optional },
                constraints: new { httpMethod = new HttpMethodConstraint("GET") }
            );

            routes.MapRoute(
               name: "AgentReady",
               url: "{controller}/{action}/{id}",
               defaults: new { controller = "Center", action = "AgentReady", id = UrlParameter.Optional },
               constraints: new { httpMethod = new HttpMethodConstraint("GET") }
           );

            routes.MapRoute(
                name: "GetActivationResults",
                url: "{controller}/{action}/{id}",
                defaults: new { controller = "Center", action = "GetActivationResults", id = UrlParameter.Optional },
                constraints: new { httpMethod = new HttpMethodConstraint("POST") }
            );

            routes.MapRoute(
                name: "GetComparisonResults",
                url: "{controller}/{action}/{id}",
                defaults: new { controller = "Center", action = "GetComparisonResults", id = UrlParameter.Optional },
                constraints: new { httpMethod = new HttpMethodConstraint("POST") }
            );
        }
    }
}
