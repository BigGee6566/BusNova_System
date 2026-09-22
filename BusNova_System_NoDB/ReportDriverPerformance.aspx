<%@ Page Title="Driver Performance" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="ReportDriverPerformance.aspx.cs" Inherits="BusNova_System.ReportDriverPerformance" %>
<asp:Content ID="Content1" ContentPlaceHolderID="head" runat="server">
    <link href="Styles/style.css?v=6" rel="stylesheet" />
    <link href="Styles/Ops.css?v=6" rel="stylesheet" />
    <link href="Styles/Reports.css?v=1" rel="stylesheet" />
</asp:Content>
<asp:Content ID="Content2" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">

    <div class="ops-page">
    <div class="ops-wrap ops-wrap--wide">

        <div class="ops-box">

            <h3 class="ops-title">Driver performance</h3>
            <div class="ops-subtitle">
                Trips run, punctuality, passengers carried and faults reported, per driver,
                over a chosen period. Licence expiry is shown alongside, because a driver
                who cannot legally drive next month is a scheduling problem this month.
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
                    <div class="rpt-kpi__value"><asp:Literal ID="litDrivers" runat="server" /></div>
                    <div class="rpt-kpi__label">Active drivers</div>
                </div>
                <div class="rpt-kpi rpt-kpi--good">
                    <div class="rpt-kpi__value"><asp:Literal ID="litCompleted" runat="server" /></div>
                    <div class="rpt-kpi__label">Trips completed</div>
                </div>
                <div class="rpt-kpi">
                    <div class="rpt-kpi__value"><asp:Literal ID="litPassengers" runat="server" /></div>
                    <div class="rpt-kpi__label">Passengers carried</div>
                </div>
                <div class="rpt-kpi rpt-kpi--warn">
                    <div class="rpt-kpi__value"><asp:Literal ID="litExpiring" runat="server" /></div>
                    <div class="rpt-kpi__label">Licences expiring</div>
                    <div class="rpt-kpi__note">within 60 days</div>
                </div>
            </div>

            <asp:Panel ID="pnlEmpty" runat="server" Visible="false" CssClass="rpt-empty">
                <div class="rpt-empty__icon"><i class="fas fa-id-card" aria-hidden="true"></i></div>
                <div class="rpt-empty__title">No drivers on record</div>
                <div>Create driver accounts on the Manage Users page.</div>
            </asp:Panel>

            <asp:Panel ID="pnlResults" runat="server" Visible="false">
                <div class="ops-grid-wrap">
                    <asp:GridView ID="gvDrivers" runat="server" CssClass="ops-grid"
                                  AutoGenerateColumns="false" GridLines="None"
                                  OnRowDataBound="gvDrivers_RowDataBound">
                        <Columns>
                            <asp:TemplateField HeaderText="Driver">
                                <ItemTemplate>
                                    <%# Server.HtmlEncode(Convert.ToString(Eval("DriverName"))) %>
                                    <%# ActiveFlag(Eval("IsActive")) %>
                                </ItemTemplate>
                            </asp:TemplateField>
                            <asp:BoundField DataField="TripsAssigned" HeaderText="Assigned"
                                            ItemStyle-CssClass="rpt-num" HeaderStyle-CssClass="rpt-num" />
                            <asp:BoundField DataField="TripsCompleted" HeaderText="Completed"
                                            ItemStyle-CssClass="rpt-num" HeaderStyle-CssClass="rpt-num" />
                            <asp:BoundField DataField="TripsCancelled" HeaderText="Cancelled"
                                            ItemStyle-CssClass="rpt-num" HeaderStyle-CssClass="rpt-num" />
                            <asp:TemplateField HeaderText="Completion" ItemStyle-CssClass="rpt-num" HeaderStyle-CssClass="rpt-num">
                                <ItemTemplate><%# FormatRate(Eval("CompletionRate"), Eval("TripsAssigned")) %></ItemTemplate>
                            </asp:TemplateField>
                            <asp:TemplateField HeaderText="Punctuality" ItemStyle-CssClass="rpt-num" HeaderStyle-CssClass="rpt-num">
                                <ItemTemplate><%# FormatPunctuality(Eval("PunctualityRate")) %></ItemTemplate>
                            </asp:TemplateField>
                            <asp:BoundField DataField="PassengersCarried" HeaderText="Passengers" DataFormatString="{0:N0}"
                                            ItemStyle-CssClass="rpt-num" HeaderStyle-CssClass="rpt-num" />
                            <asp:BoundField DataField="FaultsReported" HeaderText="Faults"
                                            ItemStyle-CssClass="rpt-num" HeaderStyle-CssClass="rpt-num" />
                            <asp:TemplateField HeaderText="Licence">
                                <ItemTemplate><%# FormatLicence(Eval("LicenseExpiryDate"), Eval("DaysUntilLicenceExpiry")) %></ItemTemplate>
                            </asp:TemplateField>
                        </Columns>
                    </asp:GridView>
                </div>
            </asp:Panel>

            <div class="rpt-method">
                <strong>How these figures are calculated.</strong>
                <em>Completion</em> is trips completed divided by trips assigned in the period.
                <em>Punctuality</em> is the share of started trips that began within
                <asp:Literal ID="litGrace" runat="server" /> minutes of their scheduled departure; a
                few seconds of lateness is not lateness a passenger would notice, and scoring it as a
                failure would make the figure meaningless. Trips that never started are excluded from
                punctuality rather than counted as late, so the two figures answer different
                questions and neither hides the other.
                <em>Passengers</em> is the count each driver recorded when completing a trip.
                Drivers with no trips in the period are still listed, showing zeros &mdash; an idle
                driver is itself worth seeing.
            </div>

        </div>
    </div>
    </div>

</asp:Content>
