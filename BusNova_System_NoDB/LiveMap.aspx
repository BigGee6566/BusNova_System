<%@ Page Title="Live Map" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="LiveMap.aspx.cs" Inherits="BusNova_System.LiveMap" %>
<asp:Content ID="Content1" ContentPlaceHolderID="head" runat="server">
    <link href="Styles/Ops.css?v=8" rel="stylesheet" />
    <link href="Styles/LiveMap.css?v=3" rel="stylesheet" />

    <%-- Leaflet and OpenStreetMap, from cdnjs, with subresource integrity.
         Approved as an exception to the no-new-dependency rule; see the
         WP-10 notes in ROADMAP.md. If the CDN is unreachable the map area
         stays empty and the table below it still works, which is why the
         table is not merely a fallback but the primary presentation. --%>
    <link rel="stylesheet"
          href="https://cdnjs.cloudflare.com/ajax/libs/leaflet/1.9.4/leaflet.min.css"
          integrity="sha512-h9FcoyWjHcOcmEVkxOfTLnmZFWIH0iZhZT1H2TbOq55xssQGEJHEaIm+PgoUaZbRvQTNTluNOEfb1ZRy6D3BOw=="
          crossorigin="anonymous" referrerpolicy="no-referrer" />
    <script src="https://cdnjs.cloudflare.com/ajax/libs/leaflet/1.9.4/leaflet.min.js"
            integrity="sha512-puJW3E/qXDqYp9IfhAI54BJEaWIfloJ7JWs7OeD5i6ruC9JZL1gERT1wjtwXFlh7CjE7ZJ+/vcRZRkIYIb6p4g=="
            crossorigin="anonymous" referrerpolicy="no-referrer"></script>
    <script src="Scripts/live-map.js?v=1" defer></script>
</asp:Content>
<asp:Content ID="Content2" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">

    <div class="ops-wrap ops-wrap--wide">

        <%-- The page title is rendered once, by Site.Master's top bar. --%>
        <p class="bn-page-intro">
            Every trip currently under way, with its last reported position.
        </p>

        <div class="bn-card">
            <p class="bn-note">
                Arrival times are a <strong>range</strong>, not a time. The distance shown is a
                straight line &mdash; this system has no road network &mdash; so it is widened by a
                percentage for real roads, and the assumption is stated on every row. A single confident-looking
                time that is twenty minutes out is worse than an honest range.
            </p>
            <div class="ops-actions ops-actions--start">
                <asp:Button ID="btnRefresh" runat="server" Text="Refresh"
                            CssClass="bn-btn bn-btn--primary" OnClick="btnRefresh_Click" />
                <asp:Button ID="btnSimulate" runat="server" Text="Simulate positions"
                            CssClass="bn-btn bn-btn--secondary" OnClick="btnSimulate_Click"
                            ToolTip="Records a simulated position for each trip under way. There is no GPS hardware in this build." />
            </div>
            <asp:Label ID="lblMessage" runat="server" />
        </div>

        <%-- The map is progressive enhancement. The table below is the
             real presentation and works with no JavaScript at all. --%>
        <div class="bn-card bn-mt-3">
            <div id="bn-map" class="lm-map" data-role="live-map"
                 data-positions='<%= PositionsJson %>'
                 role="img" aria-label="Map of buses currently under way"></div>
            <noscript>
                <p class="bn-note">The map needs JavaScript. The table below has the same information.</p>
            </noscript>
        </div>

        <div class="bn-card bn-mt-3">
            <div class="ops-grid-wrap">
                <asp:GridView ID="gvTrips" runat="server" AutoGenerateColumns="false"
                              CssClass="ops-grid" GridLines="None" UseAccessibleHeader="true"
                              OnRowDataBound="gvTrips_RowDataBound"
                              EmptyDataText="No trips are under way right now.">
                    <Columns>
                        <asp:BoundField DataField="RouteName" HeaderText="Route" />
                        <asp:BoundField DataField="BusRegistration" HeaderText="Bus" />
                        <asp:BoundField DataField="DriverName" HeaderText="Driver" />
                        <asp:TemplateField HeaderText="Last fix">
                            <ItemTemplate><%# FixAge(Eval("FixAgeMinutes"), Eval("HasFix")) %></ItemTemplate>
                        </asp:TemplateField>
                        <asp:TemplateField HeaderText="Remaining">
                            <ItemTemplate><%# Remaining(Eval("RemainingKm")) %></ItemTemplate>
                        </asp:TemplateField>
                        <asp:TemplateField HeaderText="Arrives">
                            <ItemTemplate><%# Eta(Eval("EtaEarliest"), Eval("EtaLatest"), Eval("EtaConfidence")) %></ItemTemplate>
                        </asp:TemplateField>
                        <asp:TemplateField HeaderText="Basis">
                            <ItemTemplate><span class="lm-basis"><%# Server.HtmlEncode(Convert.ToString(Eval("EtaExplanation"))) %></span></ItemTemplate>
                        </asp:TemplateField>
                    </Columns>
                </asp:GridView>
            </div>
        </div>

    </div>

</asp:Content>
