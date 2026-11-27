<%@ Page Title="Home" Language="C#" AutoEventWireup="true" CodeBehind="HomeOptions.aspx.cs" Inherits="bills_project.Pages.Home.HomeOptions" MasterPageFile="~/Site.Master" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <div class="mb-4">
        <h1 class="h3 mb-1">Welcome, <asp:Label ID="lblUserName" runat="server" /></h1>
        <p class="text-body-secondary mb-0">Upload a bill and BillLens reads the bill number, date and total for you.</p>
    </div>

    <div class="row g-3 mb-4">
        <div class="col-md-4">
            <div class="card stat-card h-100">
                <div class="card-body">
                    <div class="text-body-secondary small">Bills submitted</div>
                    <div class="fs-3 fw-semibold"><asp:Literal ID="litBillCount" runat="server" Text="0" /></div>
                </div>
            </div>
        </div>
        <div class="col-md-4">
            <div class="card stat-card h-100">
                <div class="card-body">
                    <div class="text-body-secondary small">Total amount</div>
                    <div class="fs-3 fw-semibold">&#8377;<asp:Literal ID="litTotalAmount" runat="server" Text="0.00" /></div>
                </div>
            </div>
        </div>
        <div class="col-md-4">
            <div class="card stat-card h-100">
                <div class="card-body">
                    <div class="text-body-secondary small">Last submitted</div>
                    <div class="fs-3 fw-semibold"><asp:Literal ID="litLastSubmitted" runat="server" Text="&mdash;" /></div>
                </div>
            </div>
        </div>
    </div>

    <div class="row g-3">
        <div class="col-md-4">
            <a class="card action-card h-100 text-decoration-none" href="<%: ResolveUrl("~/Pages/Upload/UploadBill.aspx") %>">
                <div class="card-body">
                    <i class="fas fa-file-arrow-up fa-2x text-primary mb-3"></i>
                    <h2 class="h5 text-body">Upload a bill</h2>
                    <p class="text-body-secondary mb-0">PDF, JPG or PNG. The details are read automatically and you check them before saving.</p>
                </div>
            </a>
        </div>
        <div class="col-md-4">
            <a class="card action-card h-100 text-decoration-none" href="<%: ResolveUrl("~/Pages/ManualEntry/ManualEntry.aspx") %>">
                <div class="card-body">
                    <i class="fas fa-keyboard fa-2x text-success mb-3"></i>
                    <h2 class="h5 text-body">Enter manually</h2>
                    <p class="text-body-secondary mb-0">No file at hand? Type in the bill number, date and amount.</p>
                </div>
            </a>
        </div>
        <div class="col-md-4">
            <a class="card action-card h-100 text-decoration-none" href="<%: ResolveUrl("~/Pages/View/ViewBills.aspx") %>">
                <div class="card-body">
                    <i class="fas fa-receipt fa-2x text-info mb-3"></i>
                    <h2 class="h5 text-body">My bills</h2>
                    <p class="text-body-secondary mb-0">Everything you have submitted, with status and totals.</p>
                </div>
            </a>
        </div>
    </div>
</asp:Content>
