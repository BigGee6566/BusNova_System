<%@ Page Title="Driver Schedule" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="ReportDriverSchedule.aspx.cs" Inherits="BusNova_System.ReportDriverSchedule" %>
<%@ Import Namespace="BusNova_System.Helpers" %>
<asp:Content ID="Content1" ContentPlaceHolderID="head" runat="server">
    <link href="Styles/Reports.css?v=3" rel="stylesheet" />
</asp:Content>
<asp:Content ID="Content2" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">

    <div class="ops-wrap ops-wrap--wide">

        <%-- The page title is rendered once, by Site.Master's top bar. --%>
        <p class="bn-page-intro">Who is driving what, and when.</p>

        <div class="bn-card">
            <div class="rpt-filters">
                <div class="bn-field">
                    <label for="<%= txtFrom.ClientID %>">From</label>
                    <asp:TextBox ID="txtFrom" runat="server" TextMode="Date" CssClass="form-control" />
                </div>
                <div class="bn-field">
                    <label for="<%= txtTo.ClientID %>">To</label>
                    <asp:TextBox ID="txtTo" runat="server" TextMode="Date" CssClass="form-control" />
                </div>
                <div class="bn-field bn-align-end">
                    <asp:Button ID="btnRun" runat="server" Text="Run report"
                                CssClass="bn-btn bn-btn--primary" OnClick="btnRun_Click" />
                </div>
            </div>

            <div class="ops-actions ops-actions--start">
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
                              EmptyDataText="No trips are scheduled in that period.">
                    <Columns>
                        <asp:BoundField DataField="AssignmentDate" HeaderText="Date" DataFormatString="{0:dd MMM yyyy}" HtmlEncode="false" />
                        <asp:BoundField DataField="DepartureTime" HeaderText="Departs" DataFormatString="{0:hh\:mm}" HtmlEncode="false" />
                        <asp:BoundField DataField="DriverName" HeaderText="Driver" />
                        <asp:BoundField DataField="LicenseNumber" HeaderText="Licence" />
                        <asp:BoundField DataField="RouteName" HeaderText="Route" />
                        <asp:BoundField DataField="BusRegistration" HeaderText="Bus" />
                        <asp:TemplateField HeaderText="Status">
                            <ItemTemplate>
                                <span class='<%# "status-badge " + StatusTone.ForTrip(Eval("TripStatus")) %>'>
                                    <%# Server.HtmlEncode(Convert.ToString(Eval("TripStatus"))) %>
                                </span>
                            </ItemTemplate>
                        </asp:TemplateField>
                        <asp:BoundField DataField="PassengerCount" HeaderText="Passengers" ItemStyle-CssClass="num" HeaderStyle-CssClass="num" />
                    </Columns>
                </asp:GridView>
            </div>
        </div>

    </div>

</asp:Content>
