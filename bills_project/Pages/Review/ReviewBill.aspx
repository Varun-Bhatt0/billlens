<%@ Page Title="Check the bill" Language="C#" AutoEventWireup="true" CodeBehind="ReviewBill.aspx.cs" Inherits="bills_project.Pages.ReviewBill" MasterPageFile="~/Site.Master" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <div class="row justify-content-center">
        <div class="col-lg-8 col-xl-7">
            <h1 class="h3 mb-1">Check the bill</h1>
            <p class="text-body-secondary">These values were read from your file. Fields marked <span class="badge text-bg-warning">check</span> were hard to read.</p>

            <asp:Panel ID="pnlAlert" runat="server" Visible="false" role="status">
                <asp:Literal ID="litAlert" runat="server" />
            </asp:Panel>

            <div class="card shadow-sm border-0">
                <div class="card-body p-4">
                    <div class="mb-3">
                        <span class="form-label d-block">Bill type</span>
                        <asp:Label ID="lblBillType" runat="server" CssClass="fw-semibold" />
                    </div>
                    <div class="mb-3">
                        <asp:Label runat="server" AssociatedControlID="txtBillNo" CssClass="form-label">Bill number <asp:Literal ID="litNoHint" runat="server" /></asp:Label>
                        <asp:TextBox ID="txtBillNo" runat="server" CssClass="form-control" MaxLength="50" />
                        <asp:RequiredFieldValidator ID="rfvBillNo" runat="server" ControlToValidate="txtBillNo"
                            ErrorMessage="Bill number is required" CssClass="text-danger small" Display="Dynamic" />
                        <asp:RegularExpressionValidator ID="revBillNo" runat="server" ControlToValidate="txtBillNo"
                            ValidationExpression="^[A-Za-z0-9][A-Za-z0-9/\-]{0,49}$"
                            ErrorMessage="Use letters, digits, / and - only" CssClass="text-danger small" Display="Dynamic" />
                    </div>
                    <div class="row g-3 mb-4">
                        <div class="col-sm-6">
                            <asp:Label runat="server" AssociatedControlID="txtBillDate" CssClass="form-label">Bill date <asp:Literal ID="litDateHint" runat="server" /></asp:Label>
                            <asp:TextBox ID="txtBillDate" runat="server" TextMode="Date" CssClass="form-control" />
                            <asp:RequiredFieldValidator ID="rfvBillDate" runat="server" ControlToValidate="txtBillDate"
                                ErrorMessage="Bill date is required" CssClass="text-danger small" Display="Dynamic" />
                        </div>
                        <div class="col-sm-6">
                            <asp:Label runat="server" AssociatedControlID="txtAmount" CssClass="form-label">Amount (&#8377;) <asp:Literal ID="litAmtHint" runat="server" /></asp:Label>
                            <asp:TextBox ID="txtAmount" runat="server" CssClass="form-control" TextMode="Number" step="0.01" min="0" placeholder="0.00" />
                            <asp:RequiredFieldValidator ID="rfvAmount" runat="server" ControlToValidate="txtAmount"
                                ErrorMessage="Amount is required" CssClass="text-danger small" Display="Dynamic" />
                        </div>
                    </div>
                    <div class="d-flex gap-2">
                        <asp:Button ID="btnSave" runat="server" CssClass="btn btn-primary" Text="Save bill" OnClick="btnSave_Click" />
                        <asp:HyperLink ID="lnkBack" runat="server" NavigateUrl="~/Pages/Upload/UploadBill.aspx" CssClass="btn btn-outline-secondary">Upload a different file</asp:HyperLink>
                    </div>
                </div>
            </div>
        </div>
    </div>
</asp:Content>
