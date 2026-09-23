<%@ Page Title="Maintenance Schedule" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="ReportMaintenanceSchedule.aspx.cs" Inherits="BusNova_System.ReportMaintenanceSchedule" %>
<asp:Content ID="Content1" ContentPlaceHolderID="head" runat="server">
    <link href="Styles/Reports.css?v=2" rel="stylesheet" />
</asp:Content>
<asp:Content ID="Content2" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">

    <div class="ops-wrap ops-wrap--wide">

        <div class="bn-page-header">
            <h1 class="bn-page-header__title">Maintenance Schedule</h1>
            <p class="bn-page-header__subtitle">Preventive service schedules, overdue first. Due-ness is worked out in one place - see MaintenanceScheduleService.</p>
        </div>

        <div class="bn-card">
            <div class="ops-actions">
                <asp:Button ID="btnCsv" runat="server" Text="Export CSV"
                            CssClass="bn-btn bn-btn--secondary" OnClick="btnCsv_Click" />
                <asp:Button ID="btnExcel" runat="server" Text="Export Excel"
                            CssClass="bn-btn bn-btn--secondary" OnClick="btnExcel_Click" />
            </div>
            <asp:Label ID="lblMessage" runat="server" />
        </div>

        <div class="bn-card bn-mt-3">
            <div class="ops-grid-wrap">
                <asp:GridView ID="gvReport" runat="server" AutoGenerateColumns="false"
                              CssClass="ops-grid" GridLines="None" UseAccessibleHeader="true"
                              OnRowDataBound="gvReport_RowDataBound"
                              EmptyDataText="No preventive schedules have been set up yet.">
                    <Columns>
                        <asp:BoundField DataField="BusRegistration" HeaderText="Bus" />
                        <asp:BoundField DataField="ScheduleName" HeaderText="Schedule" />
                        <asp:BoundField DataField="Interval" HeaderText="Interval" />
                        <asp:BoundField DataField="CurrentOdometer" HeaderText="Odometer" DataFormatString="{0:N0}" HtmlEncode="false" />
                        <asp:BoundField DataField="LastServiced" HeaderText="Last serviced" />
                        <asp:BoundField DataField="Status" HeaderText="Status" />
                        <asp:BoundField DataField="Detail" HeaderText="Detail" />
                    </Columns>
                </asp:GridView>
            </div>
        </div>

    </div>

</asp:Content>
