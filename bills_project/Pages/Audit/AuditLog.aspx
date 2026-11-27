<%@ Page Title="Audit log" Language="C#" AutoEventWireup="true" CodeBehind="AuditLog.aspx.cs" Inherits="bills_project.Pages.Audit.AuditLog" MasterPageFile="~/Site.Master" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <h1 class="h3 mb-1">Audit log</h1>
    <p class="text-body-secondary">The 500 most recent sign-ins, sign-outs, lockouts, saved bills and history views.</p>

    <div class="card shadow-sm border-0">
        <div class="card-body p-0">
            <div class="table-responsive">
                <asp:GridView ID="AuditLogGrid" runat="server" CssClass="table table-sm table-hover align-middle mb-0" GridLines="None"
                    AutoGenerateColumns="False" UseAccessibleHeader="true" EmptyDataText="No audit records yet.">
                    <Columns>
                        <asp:BoundField DataField="log_time" HeaderText="Time" DataFormatString="{0:yyyy-MM-dd HH:mm:ss}" />
                        <asp:BoundField DataField="employee_id" HeaderText="User" />
                        <asp:BoundField DataField="action" HeaderText="Action" />
                        <asp:BoundField DataField="details" HeaderText="Details" />
                    </Columns>
                </asp:GridView>
            </div>
        </div>
    </div>
</asp:Content>
