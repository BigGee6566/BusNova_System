<%@ Page Title="Route List" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="ReportRouteList.aspx.cs" Inherits="BusNova_System.ReportRouteList" %>
<asp:Content ID="Content1" ContentPlaceHolderID="head" runat="server">
    <link href="Styles/Reports.css?v=3" rel="stylesheet" />
</asp:Content>
<asp:Content ID="Content2" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">

    <div class="ops-wrap ops-wrap--wide">

        <%-- The page title is rendered once, by Site.Master's top bar. --%>
        <p class="bn-page-intro">Every route on file, with how many trips have actually been scheduled on it.</p>

        <div class="bn-card">
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
                              EmptyDataText="No routes have been created yet.">
                    <Columns>
                        <asp:BoundField DataField="RouteCode" HeaderText="Code" />
                        <asp:BoundField DataField="RouteName" HeaderText="Route" />
                        <asp:BoundField DataField="Origin" HeaderText="From" />
                        <asp:BoundField DataField="Destination" HeaderText="To" />
                        <asp:BoundField DataField="DistanceKm" HeaderText="Distance (km)" DataFormatString="{0:N1}" HtmlEncode="false" ItemStyle-CssClass="num" HeaderStyle-CssClass="num" />
                        <asp:BoundField DataField="EstimatedDurationMinutes" HeaderText="Duration (min)" ItemStyle-CssClass="num" HeaderStyle-CssClass="num" />
                        <asp:BoundField DataField="TripsScheduled" HeaderText="Trips" ItemStyle-CssClass="num" HeaderStyle-CssClass="num" />
                    </Columns>
                </asp:GridView>
            </div>
        </div>

    </div>

</asp:Content>
