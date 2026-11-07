<%@ Page Title="Maintenance Schedule" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="ReportMaintenanceSchedule.aspx.cs" Inherits="BusNova_System.ReportMaintenanceSchedule" %>
<%@ Import Namespace="BusNova_System.Helpers" %>
<asp:Content ID="Content1" ContentPlaceHolderID="head" runat="server">
    <link href="Styles/Reports.css?v=3" rel="stylesheet" />
</asp:Content>
<asp:Content ID="Content2" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">

    <div class="ops-wrap ops-wrap--wide">

        <%-- The page title is rendered once, by Site.Master's top bar. --%>
        <p class="bn-page-intro">Preventive service schedules, overdue first.</p>

        <div class="bn-card bn-mb-3" runat="server" id="exportActions">
            <div class="ops-actions ops-actions--start">
                <asp:Button ID="btnCsv" runat="server" Text="Export CSV"
                            CssClass="bn-btn bn-btn--secondary" OnClick="btnCsv_Click" />
                <asp:Button ID="btnExcel" runat="server" Text="Export Excel"
                            CssClass="bn-btn bn-btn--secondary" OnClick="btnExcel_Click" />
            </div>
        </div>

        <div class="bn-card">
            <div class="ops-grid-wrap">
                <asp:GridView ID="gvReport" runat="server" AutoGenerateColumns="false"
                              CssClass="ops-grid" GridLines="None" UseAccessibleHeader="true"
                              OnRowDataBound="gvReport_RowDataBound"
                              >
                    <EmptyDataTemplate>
                        <div class="bn-empty">
                            <i class="fas fa-calendar-check bn-empty__icon" aria-hidden="true"></i>
                            <div class="bn-empty__title">No preventive schedules yet</div>
                            <p class="bn-empty__hint">
                                A schedule is a repeating service rule for a bus, by distance, by
                                date, or both. Managers add them on the Manage Maintenance page.
                                Single next service dates are on the Maintenance Due report.
                            </p>
                        </div>
                    </EmptyDataTemplate>
                    <Columns>
                        <asp:BoundField DataField="BusRegistration" HeaderText="Bus" />
                        <asp:BoundField DataField="ScheduleName" HeaderText="Schedule" />
                        <asp:BoundField DataField="Interval" HeaderText="Interval" />
                        <asp:BoundField DataField="CurrentOdometer" HeaderText="Odometer" DataFormatString="{0:N0}" HtmlEncode="false" ItemStyle-CssClass="num" HeaderStyle-CssClass="num" />
                        <asp:BoundField DataField="LastServiced" HeaderText="Last serviced" />
                        <asp:TemplateField HeaderText="Status">
                            <ItemTemplate>
                                <span class='<%# "status-badge " + StatusTone.ForDueness(Eval("Status")) %>'>
                                    <%# Server.HtmlEncode(BusNova_System.Helpers.StatusTone.Label(Eval("Status"))) %>
                                </span>
                            </ItemTemplate>
                        </asp:TemplateField>
                        <asp:BoundField DataField="Detail" HeaderText="Detail" />
                    </Columns>
                </asp:GridView>
            </div>
        </div>

    </div>

</asp:Content>
