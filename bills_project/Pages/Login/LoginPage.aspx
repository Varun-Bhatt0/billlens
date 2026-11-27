<%@ Page Title="Sign in" Language="C#" AutoEventWireup="true" CodeBehind="LoginPage.aspx.cs" Inherits="bills_project.Pages.Login.LoginPage" MasterPageFile="~/Site.Master" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <asp:HiddenField ID="CsrfToken" runat="server" />
    <div class="row justify-content-center py-5">
        <div class="col-sm-10 col-md-7 col-lg-5 col-xl-4">
            <div class="card shadow-sm border-0">
                <div class="card-body p-4 p-md-5">
                    <div class="text-center mb-4">
                        <img src="<%: ResolveUrl("~/Content/images/logo.svg") %>" alt="" width="56" height="56" class="mb-2" />
                        <h1 class="h4 mb-1">BillLens</h1>
                        <p class="text-body-secondary mb-0">Bill processing with OCR</p>
                    </div>

                    <asp:Panel ID="MessagePanel" runat="server" Visible="false" CssClass="alert alert-danger" role="alert">
                        <asp:Literal ID="MessageLiteral" runat="server" />
                    </asp:Panel>

                    <div class="mb-3">
                        <asp:Label runat="server" AssociatedControlID="txtEmployeeId" CssClass="form-label">Employee ID</asp:Label>
                        <asp:TextBox ID="txtEmployeeId" runat="server" CssClass="form-control" autocomplete="username" MaxLength="20" />
                        <asp:RequiredFieldValidator ID="rfvEmployeeId" runat="server" ControlToValidate="txtEmployeeId"
                            ErrorMessage="Employee ID is required" CssClass="text-danger small" Display="Dynamic" />
                    </div>
                    <div class="mb-4">
                        <asp:Label runat="server" AssociatedControlID="txtPassword" CssClass="form-label">Password</asp:Label>
                        <asp:TextBox ID="txtPassword" runat="server" CssClass="form-control" TextMode="Password" autocomplete="current-password" />
                        <asp:RequiredFieldValidator ID="rfvPassword" runat="server" ControlToValidate="txtPassword"
                            ErrorMessage="Password is required" CssClass="text-danger small" Display="Dynamic" />
                    </div>
                    <asp:Button ID="btnLogin" runat="server" Text="Sign in" CssClass="btn btn-primary w-100" OnClick="btnLogin_Click" />

                    <asp:PlaceHolder ID="phDemo" runat="server" Visible="false">
                        <div class="alert alert-light border small mt-4 mb-0">
                            <strong>Demo accounts</strong> (local development only)<br />
                            <code>test123</code> / <code>password123</code> &middot; user<br />
                            <code>admin123</code> / <code>admin123</code> &middot; admin
                        </div>
                    </asp:PlaceHolder>
                </div>
            </div>
        </div>
    </div>
</asp:Content>
