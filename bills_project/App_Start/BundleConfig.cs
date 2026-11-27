using System.Web.Optimization;
using System.Web.UI;

namespace bills_project
{
    public static class BundleConfig
    {
        public const string JQueryVersion = "3.8.0";

        public static void RegisterBundles(BundleCollection bundles)
        {
            RegisterJQuery();

            // Bootstrap 5.3 + site styles, combined and fingerprinted (?v=hash) by ASP.NET bundling
            bundles.Add(new StyleBundle("~/Content/css").Include(
                "~/Content/lib/bootstrap/bootstrap.min.css",
                "~/Content/Site.css"));

#if !DEBUG
            BundleTable.EnableOptimizations = true;
#endif
        }

        // ASP.NET validators use jQuery in unobtrusive mode; the ScriptManager finds it under the name "jquery".
        // Served locally (no CDN), which also keeps the Content-Security-Policy at 'self'.
        private static void RegisterJQuery()
        {
            ScriptManager.ScriptResourceMapping.AddDefinition("jquery", new ScriptResourceDefinition
            {
                Path = $"~/Scripts/lib/jquery-{JQueryVersion}.min.js",
                DebugPath = $"~/Scripts/lib/jquery-{JQueryVersion}.js"
            });
        }
    }
}
