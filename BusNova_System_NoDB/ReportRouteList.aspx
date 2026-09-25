<%@ Page Title="Route List" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="ReportRouteList.aspx.cs" Inherits="BusNova_System.ReportRouteList" %>
<asp:Content ID="Content1" ContentPlaceHolderID="head" runat="server">
    <link href="Styles/Reports.css?v=3" rel="stylesheet" />
</asp:Content>
<asp:Content ID="Content2" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">

    <div class="ops-wrap ops-wrap--wide">

        <%-- The page title is rendered once, by Site.Master's top bar. --%>
        <p class="bn-page-intro">Every route on file, with how many trips have actually been scheduled on it.</p>

        <div class="bn-card">
            <div class="rpt-filters">
                <div class="rpt-field rpt-field--grow">
                    <label for="routeSearch">Search</label>
                    <input type="text" id="routeSearch" placeholder="Code, name, origin or destination" autocomplete="off" />
                </div>
                <div class="rpt-field">
                    <label for="routeStatusFilter">Status</label>
                    <select id="routeStatusFilter">
                        <option value="all">All</option>
                        <option value="active">Active</option>
                        <option value="retired">Retired</option>
                    </select>
                </div>
            </div>

            <div class="ops-actions ops-actions--start" runat="server" id="exportActions">
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
                        <asp:TemplateField HeaderText="Code">
                            <ItemTemplate>
                                <span class="code-badge"><%# Server.HtmlEncode(Convert.ToString(Eval("RouteCode"))) %></span>
                            </ItemTemplate>
                        </asp:TemplateField>
                        <asp:BoundField DataField="RouteName" HeaderText="Route" />
                        <asp:BoundField DataField="Origin" HeaderText="From" />
                        <asp:BoundField DataField="Destination" HeaderText="To" />
                        <asp:BoundField DataField="DistanceKm" HeaderText="Distance (km)" DataFormatString="{0:N1}" HtmlEncode="false" ItemStyle-CssClass="num" HeaderStyle-CssClass="num" />
                        <asp:BoundField DataField="EstimatedDurationMinutes" HeaderText="Duration (min)" ItemStyle-CssClass="num" HeaderStyle-CssClass="num" />
                        <asp:BoundField DataField="TripsScheduled" HeaderText="Trips" ItemStyle-CssClass="num" HeaderStyle-CssClass="num" />
                        <asp:TemplateField HeaderText="Status">
                            <ItemTemplate>
                                <span class='<%# "status-badge " + (((bool)Eval("IsActive")) ? "status-completed" : "status-muted") %>'>
                                    <%# ((bool)Eval("IsActive")) ? "Active" : "Retired" %>
                                </span>
                            </ItemTemplate>
                        </asp:TemplateField>
                    </Columns>
                </asp:GridView>
            </div>
        </div>

    </div>

    <script>
        (function () {
            'use strict';
            // Client-side only: the report already sends every row to the
            // browser, so filtering a list this size again on the server
            // would be a round trip for something the DOM can already see.
            var search = document.getElementById('routeSearch');
            var status = document.getElementById('routeStatusFilter');
            var table = document.querySelector('.ops-grid');
            if (!search || !status || !table) return;

            var rows = Array.prototype.slice.call(table.querySelectorAll('tbody tr'))
                .filter(function (tr) { return tr.cells.length > 1; });

            function apply() {
                var term = search.value.trim().toLowerCase();
                var wantActive = status.value;

                rows.forEach(function (tr) {
                    var text = tr.textContent.toLowerCase();
                    var matchesSearch = term === '' || text.indexOf(term) !== -1;

                    var statusCell = tr.querySelector('.status-badge');
                    var isActive = statusCell ? statusCell.textContent.trim() === 'Active' : true;
                    var matchesStatus = wantActive === 'all' ||
                        (wantActive === 'active' && isActive) ||
                        (wantActive === 'retired' && !isActive);

                    tr.hidden = !(matchesSearch && matchesStatus);
                });
            }

            search.addEventListener('input', apply);
            status.addEventListener('change', apply);
        })();
    </script>

</asp:Content>
