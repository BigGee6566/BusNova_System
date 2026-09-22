<%@ Page Title="Route List" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="ReportRouteList.aspx.cs" Inherits="BusNova_System.ReportRouteList" %>
<asp:Content ID="Content1" ContentPlaceHolderID="head" runat="server">
    <link href="Styles/Reports.css?v=2" rel="stylesheet" />
</asp:Content>
<asp:Content ID="Content2" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">

    <div class="ops-wrap ops-wrap--wide">

        <div class="bn-page-header">
            <h1 class="bn-page-header__title">Route List</h1>
            <p class="bn-page-header__subtitle">Every route on file, with how many trips have actually been scheduled on it.</p>
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
            <asp:GridView ID="gvReport" runat="server" AutoGenerateColumns="false"
                          CssClass="ops-grid" GridLines="None" UseAccessibleHeader="true"
                          OnRowDataBound="gvReport_RowDataBound"
                          EmptyDataText="No routes have been created yet.">
                <Columns>
                    <asp:BoundField DataField="RouteCode" HeaderText="Code" />
                    <asp:BoundField DataField="RouteName" HeaderText="Route" />
                    <asp:BoundField DataField="Origin" HeaderText="From" />
                    <asp:BoundField DataField="Destination" HeaderText="To" />
                    <asp:BoundField DataField="DistanceKm" HeaderText="Distance (km)" DataFormatString="{0:N1}" HtmlEncode="false" />
                    <asp:BoundField DataField="EstimatedDurationMinutes" HeaderText="Duration (min)" />
                    <asp:BoundField DataField="TripsScheduled" HeaderText="Trips" />
                </Columns>
            </asp:GridView>
        </div>

    </div>

</asp:Content>
