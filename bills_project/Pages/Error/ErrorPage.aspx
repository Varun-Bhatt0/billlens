<%@ Page Title="Something went wrong" Language="C#" AutoEventWireup="true" CodeBehind="ErrorPage.aspx.cs" Inherits="bills_project.Pages.Error.ErrorPage" MasterPageFile="~/Site.Master" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <div class="row justify-content-center py-5">
        <div class="col-md-8 col-lg-6 text-center">
            <i class="fas fa-triangle-exclamation fa-3x text-warning mb-3"></i>
            <h1 class="h4">Something went wrong</h1>
            <p class="mb-2"><asp:Label ID="lblErrorMessage" runat="server" /></p>
            <p class="text-body-secondary small"><asp:Label ID="lblErrorCode" runat="server" /></p>
            <asp:HyperLink ID="lnkHome" runat="server" CssClass="btn btn-primary" NavigateUrl="~/Pages/Home/HomeOptions.aspx">Back to home</asp:HyperLink>
        </div>
    </div>
</asp:Content>
