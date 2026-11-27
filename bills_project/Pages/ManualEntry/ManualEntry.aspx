<%@ Page Title="Enter a bill" Language="C#" AutoEventWireup="true" CodeBehind="ManualEntry.aspx.cs" Inherits="bills_project.Pages.ManualEntry.ManualEntry" MasterPageFile="~/Site.Master" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <div class="row justify-content-center">
        <div class="col-lg-8 col-xl-7">
            <h1 class="h3 mb-1">Enter a bill</h1>
            <p class="text-body-secondary">Type in the details from the bill.</p>

            <div class="card shadow-sm border-0">
                <div class="card-body p-4">
                    <asp:Panel ID="pnlError" runat="server" CssClass="alert alert-danger" Visible="false" role="alert">
                        <asp:Literal ID="litError" runat="server" />
                    </asp:Panel>

                    <asp:Panel ID="pnlForm" runat="server" DefaultButton="btnSave">
                        <div class="mb-3">
                            <asp:Label runat="server" AssociatedControlID="ddlBillType" CssClass="form-label">Bill type</asp:Label>
                            <asp:DropDownList ID="ddlBillType" runat="server" CssClass="form-select" />
                            <asp:RequiredFieldValidator ID="rfvBillType" runat="server" ControlToValidate="ddlBillType" InitialValue=""
                                ErrorMessage="Please choose a bill type" CssClass="text-danger small" Display="Dynamic" />
                        </div>
                        <div class="mb-3">
                            <asp:Label runat="server" AssociatedControlID="txtBillNumber" CssClass="form-label">Bill number</asp:Label>
                            <asp:TextBox ID="txtBillNumber" runat="server" CssClass="form-control" MaxLength="50" />
                            <asp:RequiredFieldValidator ID="rfvBillNumber" runat="server" ControlToValidate="txtBillNumber"
                                ErrorMessage="Bill number is required" CssClass="text-danger small" Display="Dynamic" />
                            <asp:RegularExpressionValidator ID="revBillNumber" runat="server" ControlToValidate="txtBillNumber"
                                ValidationExpression="^[A-Za-z0-9][A-Za-z0-9/\-]{0,49}$"
                                ErrorMessage="Use letters, digits, / and - only" CssClass="text-danger small" Display="Dynamic" />
                        </div>
                        <div class="row g-3 mb-4">
                            <div class="col-sm-6">
                                <asp:Label runat="server" AssociatedControlID="txtBillDate" CssClass="form-label">Bill date</asp:Label>
                                <asp:TextBox ID="txtBillDate" runat="server" TextMode="Date" CssClass="form-control" />
                                <asp:RequiredFieldValidator ID="rfvBillDate" runat="server" ControlToValidate="txtBillDate"
                                    ErrorMessage="Bill date is required" CssClass="text-danger small" Display="Dynamic" />
                            </div>
                            <div class="col-sm-6">
                                <asp:Label runat="server" AssociatedControlID="txtBillAmt" CssClass="form-label">Amount (&#8377;)</asp:Label>
                                <asp:TextBox ID="txtBillAmt" runat="server" CssClass="form-control" TextMode="Number" step="0.01" min="0" placeholder="0.00" />
                                <asp:RequiredFieldValidator ID="rfvBillAmt" runat="server" ControlToValidate="txtBillAmt"
                                    ErrorMessage="Amount is required" CssClass="text-danger small" Display="Dynamic" />
                            </div>
                        </div>
                        <asp:Button ID="btnSave" runat="server" CssClass="btn btn-primary" Text="Save bill" OnClick="btnSave_Click" />
                    </asp:Panel>
                </div>
            </div>
        </div>
    </div>
</asp:Content>
