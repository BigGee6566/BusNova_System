<%@ Page Title="Fuel Efficiency" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="ReportFuelEfficiency.aspx.cs" Inherits="BusNova_System.ReportFuelEfficiency" %>
<asp:Content ID="Content1" ContentPlaceHolderID="head" runat="server">
    <link href="Styles/style.css?v=6" rel="stylesheet" />
    <link href="Styles/Ops.css?v=6" rel="stylesheet" />
    <link href="Styles/Reports.css?v=1" rel="stylesheet" />
</asp:Content>
<asp:Content ID="Content2" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">

    <div class="ops-page">
    <div class="ops-wrap" style="max-width: 1180px;">

        <div class="ops-box">

            <h3 class="ops-title">Fuel efficiency</h3>
            <div class="ops-subtitle">
                Consumption and running cost per bus over a chosen period, so a vehicle
                that is drinking more than it should shows up before the fuel bill does.
            </div>

            <div class="rpt-filters">
                <div class="rpt-field">
                    <label for="<%= txtFrom.ClientID %>">From</label>
                    <asp:TextBox ID="txtFrom" runat="server" TextMode="Date" />
                </div>
                <div class="rpt-field">
                    <label for="<%= txtTo.ClientID %>">To</label>
                    <asp:TextBox ID="txtTo" runat="server" TextMode="Date" />
                </div>
                <asp:Button ID="btnRun" runat="server" Text="Run report"
                            CssClass="btn-ops-primary" OnClick="btnRun_Click" />
                <asp:Button ID="btnLast90" runat="server" Text="Last 90 days"
                            CssClass="btn-ops-secondary" OnClick="btnLast90_Click" CausesValidation="false" />
            </div>

            <asp:Label ID="lblMessage" runat="server" CssClass="ops-message" />

            <div class="rpt-kpis">
                <div class="rpt-kpi">
                    <div class="rpt-kpi__value"><asp:Literal ID="litLitres" runat="server" /></div>
                    <div class="rpt-kpi__label">Litres purchased</div>
                </div>
                <div class="rpt-kpi">
                    <div class="rpt-kpi__value"><asp:Literal ID="litCost" runat="server" /></div>
                    <div class="rpt-kpi__label">Total fuel spend</div>
                </div>
                <div class="rpt-kpi">
                    <div class="rpt-kpi__value"><asp:Literal ID="litDistance" runat="server" /></div>
                    <div class="rpt-kpi__label">Distance covered</div>
                </div>
                <div class="rpt-kpi rpt-kpi--good">
                    <div class="rpt-kpi__value"><asp:Literal ID="litFleetKmL" runat="server" /></div>
                    <div class="rpt-kpi__label">Fleet average</div>
                    <div class="rpt-kpi__note">kilometres per litre</div>
                </div>
            </div>

            <asp:Panel ID="pnlEmpty" runat="server" Visible="false" CssClass="rpt-empty">
                <div class="rpt-empty__icon"><i class="fas fa-gas-pump" aria-hidden="true"></i></div>
                <div class="rpt-empty__title">No fuel logged in this period</div>
                <div>Widen the date range, or ask drivers to record refuelling on the Log Fuel page.</div>
            </asp:Panel>

            <asp:Panel ID="pnlResults" runat="server" Visible="false">
                <div class="ops-grid-wrap">
                    <asp:GridView ID="gvFuel" runat="server" CssClass="ops-grid"
                                  AutoGenerateColumns="false" GridLines="None"
                                  OnRowDataBound="gvFuel_RowDataBound">
                        <Columns>
                            <asp:BoundField DataField="BusRegistration" HeaderText="Bus" />
                            <asp:BoundField DataField="Model" HeaderText="Model" />
                            <asp:BoundField DataField="FillCount" HeaderText="Fills"
                                            ItemStyle-CssClass="rpt-num" HeaderStyle-CssClass="rpt-num" />
                            <asp:BoundField DataField="TotalLitres" HeaderText="Litres" DataFormatString="{0:N2}"
                                            ItemStyle-CssClass="rpt-num" HeaderStyle-CssClass="rpt-num" />
                            <asp:TemplateField HeaderText="Distance" ItemStyle-CssClass="rpt-num" HeaderStyle-CssClass="rpt-num">
                                <ItemTemplate><%# Convert.ToInt32(Eval("DistanceKm")).ToString("N0") %> km</ItemTemplate>
                            </asp:TemplateField>
                            <asp:TemplateField HeaderText="km / L" ItemStyle-CssClass="rpt-num" HeaderStyle-CssClass="rpt-num">
                                <ItemTemplate><%# FormatEfficiency(Eval("KmPerLitre")) %></ItemTemplate>
                            </asp:TemplateField>
                            <asp:TemplateField HeaderText="Cost / km" ItemStyle-CssClass="rpt-num" HeaderStyle-CssClass="rpt-num">
                                <ItemTemplate><%# FormatMoneyOrDash(Eval("CostPerKm")) %></ItemTemplate>
                            </asp:TemplateField>
                            <asp:TemplateField HeaderText="Cost / L" ItemStyle-CssClass="rpt-num" HeaderStyle-CssClass="rpt-num">
                                <ItemTemplate><%# FormatMoneyOrDash(Eval("CostPerLitre")) %></ItemTemplate>
                            </asp:TemplateField>
                            <asp:TemplateField HeaderText="Total cost" ItemStyle-CssClass="rpt-num" HeaderStyle-CssClass="rpt-num">
                                <ItemTemplate><%# FormatMoney(Eval("TotalCost")) %></ItemTemplate>
                            </asp:TemplateField>
                        </Columns>
                    </asp:GridView>
                </div>
            </asp:Panel>

            <div class="rpt-method">
                <strong>How these figures are calculated.</strong>
                Distance is the highest odometer reading recorded in the period minus the lowest.
                Litres used for the km/L figure deliberately <em>exclude the earliest fill in the
                period</em>, because that fuel was burned covering distance before the period began &mdash;
                counting it would make every bus look thirstier than it is, and the distortion is
                worst on short periods. A bus with only one fill therefore shows a distance but no
                km/L: one fill is not enough to measure consumption between two points.
                <em>Cost per litre</em> uses every fill, since that is a purchase price rather than a
                consumption measure.
            </div>

        </div>
    </div>
    </div>

</asp:Content>
