using System.Web.Routing;

namespace bills_project
{
    public static class RouteConfig
    {
        public static void RegisterRoutes(RouteCollection routes)
        {
            routes.MapPageRoute("HomeRoute", "", "~/Pages/Home/HomeOptions.aspx");
            routes.MapPageRoute("LoginRoute", "login", "~/Pages/Login/LoginPage.aspx");
            routes.MapPageRoute("UploadRoute", "upload", "~/Pages/Upload/UploadBill.aspx");
            routes.MapPageRoute("ReviewRoute", "review", "~/Pages/Review/ReviewBill.aspx");
            routes.MapPageRoute("BillsRoute", "bills", "~/Pages/View/ViewBills.aspx");
        }
    }
}
