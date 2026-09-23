<%@ Page Title="Ticket Sales" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="ReportTicketSales.aspx.cs" Inherits="BusNova_System.ReportTicketSales" %>
<asp:Content ID="Content1" ContentPlaceHolderID="head" runat="server">
    <link href="Styles/Reports.css?v=2" rel="stylesheet" />
</asp:Content>
<asp:Content ID="Content2" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">

    <div class="ops-wrap ops-wrap--wide">

        <div class="bn-page-header">
            <h1 class="bn-page-header__title">Ticket Sales</h1>
            <p class="bn-page-header__subtitle">Tickets sold and revenue by day and route. Cancellations and refunds are shown separately rather than netted away.</p>
        </div>

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
                              EmptyDataText="No tickets were sold in that period.">
                    <Columns>
                        <asp:BoundField DataField="SaleDate" HeaderText="Date" DataFormatString="{0:dd MMM yyyy}" HtmlEncode="false" />
                        <asp:BoundField DataField="RouteName" HeaderText="Route" />
                        <asp:BoundField DataField="TicketsSold" HeaderText="Sold" />
                        <asp:BoundField DataField="TicketsCancelled" HeaderText="Cancelled" />
                        <asp:BoundField DataField="GrossRevenue" HeaderText="Gross" DataFormatString="{0:C}" HtmlEncode="false" />
                        <asp:BoundField DataField="Refunded" HeaderText="Refunded" DataFormatString="{0:C}" HtmlEncode="false" />
                        <asp:BoundField DataField="NetRevenue" HeaderText="Net" DataFormatString="{0:C}" HtmlEncode="false" />
                    </Columns>
                </asp:GridView>
            </div>
        </div>

    </div>

</asp:Content>
