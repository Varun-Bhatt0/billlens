<%@ Page Title="Upload a bill" Language="C#" Async="true" AsyncTimeout="120" AutoEventWireup="true" CodeBehind="UploadBill.aspx.cs" Inherits="bills_project.Pages.Upload.UploadBill" MasterPageFile="~/Site.Master" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <div class="row justify-content-center">
        <div class="col-lg-8 col-xl-7">
            <h1 class="h3 mb-1">Upload a bill</h1>
            <p class="text-body-secondary">BillLens reads the bill number, date and total. You can check and correct them on the next page before anything is saved.</p>

            <div class="card shadow-sm border-0">
                <div class="card-body p-4">
                    <div class="mb-3">
                        <asp:Label runat="server" AssociatedControlID="ddlBillType" CssClass="form-label">Bill type</asp:Label>
                        <asp:DropDownList ID="ddlBillType" runat="server" CssClass="form-select" />
                        <asp:RequiredFieldValidator ID="rfvBillType" runat="server" ControlToValidate="ddlBillType" InitialValue=""
                            ErrorMessage="Please choose a bill type" CssClass="text-danger small" Display="Dynamic" />
                    </div>

                    <div class="mb-3">
                        <asp:Label runat="server" AssociatedControlID="fuBill" CssClass="form-label">Bill file</asp:Label>
                        <asp:FileUpload ID="fuBill" runat="server" CssClass="form-control" accept=".pdf,.jpg,.jpeg,.png,application/pdf,image/jpeg,image/png" />
                        <div class="form-text">PDF, JPG or PNG, up to <asp:Literal ID="litMaxSize" runat="server" /> MB. For photos: flat, well lit, the whole bill in frame.</div>
                        <asp:RequiredFieldValidator ID="rfvFile" runat="server" ControlToValidate="fuBill"
                            ErrorMessage="Please choose a file" CssClass="text-danger small" Display="Dynamic" />
                        <asp:RegularExpressionValidator ID="revFile" runat="server" ControlToValidate="fuBill"
                            ValidationExpression=".*\.([jJ][pP][eE]?[gG]|[pP][nN][gG]|[pP][dD][fF])$"
                            ErrorMessage="Only PDF, JPG and PNG files are accepted" CssClass="text-danger small" Display="Dynamic" />
                    </div>

                    <asp:Panel ID="pnlError" runat="server" CssClass="alert alert-danger" Visible="false" role="alert">
                        <asp:Literal ID="litError" runat="server" />
                    </asp:Panel>

                    <div class="d-flex gap-2">
                        <asp:Button ID="btnProcessOCR" runat="server" CssClass="btn btn-primary" Text="Read bill"
                            OnClick="btnProcessOCR_Click" OnClientClick="return billlens.busy(this, 'Reading bill…');" />
                        <asp:HyperLink runat="server" NavigateUrl="~/Pages/ManualEntry/ManualEntry.aspx" CssClass="btn btn-outline-secondary">Enter manually instead</asp:HyperLink>
                    </div>
                </div>
            </div>
        </div>
    </div>
    <script>
        // Disable the button and show progress while the bill is uploaded and read (only once validation passes).
        window.billlens = window.billlens || {
            busy: function (button, label) {
                if (typeof Page_ClientValidate === 'function' && !Page_ClientValidate()) return false;
                setTimeout(function () { button.disabled = true; button.value = label; }, 0);
                return true;
            }
        };
    </script>
</asp:Content>
