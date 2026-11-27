<%@ Page Title="My bills" Language="C#" AutoEventWireup="true" CodeBehind="ViewBills.aspx.cs" Inherits="bills_project.Pages.ViewBills.ViewBills" MasterPageFile="~/Site.Master" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <div class="d-flex flex-wrap justify-content-between align-items-center gap-2 mb-3">
        <div>
            <h1 class="h3 mb-1">My bills</h1>
            <p class="text-body-secondary mb-0">Everything you have submitted, newest first.</p>
        </div>
        <asp:HyperLink ID="lnkAddBill" runat="server" NavigateUrl="~/Pages/Upload/UploadBill.aspx" CssClass="btn btn-primary">
            <i class="fas fa-plus me-1"></i>Add a bill
        </asp:HyperLink>
    </div>

    <asp:Panel ID="pnlSaved" runat="server" CssClass="alert alert-success" Visible="false" role="status">
        <i class="fas fa-check me-1"></i>Bill saved.
    </asp:Panel>

    <asp:Panel ID="pnlSummary" runat="server" CssClass="row g-3 mb-3" Visible="false">
        <div class="col-sm-4">
            <div class="card stat-card"><div class="card-body">
                <div class="text-body-secondary small">Bills</div>
                <div class="fs-4 fw-semibold"><asp:Literal ID="litTotalBills" runat="server" /></div>
            </div></div>
        </div>
        <div class="col-sm-4">
            <div class="card stat-card"><div class="card-body">
                <div class="text-body-secondary small">Total amount</div>
                <div class="fs-4 fw-semibold">&#8377;<asp:Literal ID="litTotalAmount" runat="server" /></div>
            </div></div>
        </div>
        <div class="col-sm-4">
            <div class="card stat-card"><div class="card-body">
                <div class="text-body-secondary small">Last submitted</div>
                <div class="fs-4 fw-semibold"><asp:Literal ID="litLatestSubmission" runat="server" /></div>
            </div></div>
        </div>
    </asp:Panel>

    <div class="card shadow-sm border-0">
        <div class="card-body p-0">
            <div class="table-responsive">
                <asp:GridView ID="gvBills" runat="server" CssClass="table table-hover align-middle mb-0" GridLines="None"
                    AutoGenerateColumns="False" AllowPaging="True" PageSize="10" UseAccessibleHeader="true"
                    OnPageIndexChanging="gvBills_PageIndexChanging" PagerStyle-CssClass="grid-pager">
                    <Columns>
                        <asp:BoundField DataField="ApplicationNo" HeaderText="#" />
                        <asp:BoundField DataField="BillType" HeaderText="Type" />
                        <asp:BoundField DataField="BillNumber" HeaderText="Bill number" />
                        <asp:BoundField DataField="BillDate" HeaderText="Bill date" DataFormatString="{0:yyyy-MM-dd}" />
                        <asp:BoundField DataField="BillAmount" HeaderText="Amount" DataFormatString="₹{0:N2}" ItemStyle-CssClass="text-end" HeaderStyle-CssClass="text-end" />
                        <asp:TemplateField HeaderText="Status">
                            <ItemTemplate>
                                <span class="badge text-bg-secondary"><%#: Eval("Status") %></span>
                            </ItemTemplate>
                        </asp:TemplateField>
                        <asp:BoundField DataField="SubmittedOn" HeaderText="Submitted" DataFormatString="{0:yyyy-MM-dd}" />
                    </Columns>
                    <EmptyDataTemplate>
                        <div class="text-center text-body-secondary p-5">
                            <i class="fas fa-receipt fa-2x mb-2"></i>
                            <div>No bills yet. <a href="<%: ResolveUrl("~/Pages/Upload/UploadBill.aspx") %>">Upload your first one.</a></div>
                        </div>
                    </EmptyDataTemplate>
                </asp:GridView>
            </div>
        </div>
    </div>
</asp:Content>
