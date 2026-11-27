using System;
using bills_project.Infrastructure;

namespace bills_project.Pages.Audit
{
    // The most recent audit entries. Admins only (enforced by SecurePage).
    public partial class AuditLog : SecurePage
    {
        protected override bool RequiresAdmin => true;

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack)
            {
                AuditLogGrid.DataSource = Db.GetAuditLog(500);
                AuditLogGrid.DataBind();
            }
        }
    }
}
