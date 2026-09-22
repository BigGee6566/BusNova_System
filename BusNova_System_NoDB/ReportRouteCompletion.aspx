<%@ Page Title="Route Completion" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="ReportRouteCompletion.aspx.cs" Inherits="BusNova_System.ReportRouteCompletion" %>
<asp:Content ID="Content1" ContentPlaceHolderID="head" runat="server">
    <link href="Styles/style.css?v=6" rel="stylesheet" />
    <link href="Styles/Ops.css?v=6" rel="stylesheet" />
    <link href="Styles/Reports.css?v=1" rel="stylesheet" />
</asp:Content>
<asp:Content ID="Content2" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">

    <div class="ops-page">
    <div class="ops-wrap ops-wrap--wide">

        <div class="ops-box">

            <h3 class="ops-title">Daily route completion</h3>
            <div class="ops-subtitle">
                Every trip scheduled for one day and how it actually ran &mdash; whether it
                was completed, how late it left, and how full the bus was.
            </div>

            <div class="rpt-filters">
                <div class="rpt-field">
                    <label for="<%= txtDate.ClientID %>">Date</label>
                    <asp:TextBox ID="txtDate" runat="server" TextMode="Date" />
                </div>
                <asp:Button ID="btnRun" runat="server" Text="Run report"
                            CssClass="btn-ops-primary" OnClick="btnRun_Click" />
                <asp:Button ID="btnToday" runat="server" Text="Today"
                            CssClass="btn-ops-secondary" OnClick="btnToday_Click" CausesValidation="false" />
            </div>

            <asp:Label ID="lblMessage" runat="server" CssClass="ops-message" />

            <div class="rpt-kpis">
                <div class="rpt-kpi">
                    <div class="rpt-kpi__value"><asp:Literal ID="litTotal" runat="server" /></div>
                    <div class="rpt-kpi__label">Trips scheduled</div>
                </div>
                <div class="rpt-kpi rpt-kpi--good">
                    <div class="rpt-kpi__value"><asp:Literal ID="litCompleted" runat="server" /></div>
                    <div class="rpt-kpi__label">Completed</div>
                    <div class="rpt-kpi__note"><asp:Literal ID="litCompletionRate" runat="server" /></div>
                </div>
                <div class="rpt-kpi rpt-kpi--warn">
                    <div class="rpt-kpi__value"><asp:Literal ID="litOutstanding" runat="server" /></div>
                    <div class="rpt-kpi__label">Scheduled or in progress</div>
                </div>
                <div class="rpt-kpi">
                    <div class="rpt-kpi__value"><asp:Literal ID="litOccupancy" runat="server" /></div>
                    <div class="rpt-kpi__label">Occupancy</div>
                    <div class="rpt-kpi__note"><asp:Literal ID="litOccupancyNote" runat="server" /></div>
                </div>
            </div>

            <asp:Panel ID="pnlEmpty" runat="server" Visible="false" CssClass="rpt-empty">
                <div class="rpt-empty__icon"><i class="fas fa-calendar-day" aria-hidden="true"></i></div>
                <div class="rpt-empty__title">No trips scheduled for this date</div>
                <div>Pick another date, or assign a bus and driver on the Assign Route page.</div>
            </asp:Panel>

            <asp:Panel ID="pnlResults" runat="server" Visible="false">
                <div class="ops-grid-wrap">
                    <asp:GridView ID="gvTrips" runat="server" CssClass="ops-grid"
                                  AutoGenerateColumns="false" GridLines="None"
                                  OnRowDataBound="gvTrips_RowDataBound">
                        <Columns>
                            <asp:TemplateField HeaderText="Departure">
                                <ItemTemplate><%# FormatTime(Eval("DepartureTime")) %></ItemTemplate>
                            </asp:TemplateField>
                            <asp:BoundField DataField="RouteName" HeaderText="Route" />
                            <asp:TemplateField HeaderText="From / To">
                                <ItemTemplate><%# Server.HtmlEncode(Convert.ToString(Eval("Origin"))) %> &rarr; <%# Server.HtmlEncode(Convert.ToString(Eval("Destination"))) %></ItemTemplate>
                            </asp:TemplateField>
                            <asp:BoundField DataField="BusRegistration" HeaderText="Bus" />
                            <asp:BoundField DataField="DriverName" HeaderText="Driver" />
                            <asp:TemplateField HeaderText="Status">
                                <ItemTemplate>
                                    <span class='<%# "status-badge " + StatusCss(Eval("TripStatus")) %>'><%# Server.HtmlEncode(Convert.ToString(Eval("TripStatus"))) %></span>
                                </ItemTemplate>
                            </asp:TemplateField>
                            <asp:TemplateField HeaderText="Started" ItemStyle-CssClass="rpt-num" HeaderStyle-CssClass="rpt-num">
                                <ItemTemplate><%# FormatDelay(Eval("StartDelayMinutes")) %></ItemTemplate>
                            </asp:TemplateField>
                            <asp:TemplateField HeaderText="Seats sold" ItemStyle-CssClass="rpt-num" HeaderStyle-CssClass="rpt-num">
                                <ItemTemplate><%# Eval("SeatsSold") %> / <%# Eval("SeatingCapacity") %></ItemTemplate>
                            </asp:TemplateField>
                            <asp:TemplateField HeaderText="Occupancy" ItemStyle-CssClass="rpt-num" HeaderStyle-CssClass="rpt-num">
                                <ItemTemplate><%# Eval("OccupancyPercent") %>%</ItemTemplate>
                            </asp:TemplateField>
                            <asp:BoundField DataField="PassengerCount" HeaderText="Carried"
                                            ItemStyle-CssClass="rpt-num" HeaderStyle-CssClass="rpt-num" />
                        </Columns>
                    </asp:GridView>
                </div>
            </asp:Panel>

            <div class="rpt-method">
                <strong>How these figures are calculated.</strong>
                Occupancy is seats sold divided by the bus's seating capacity, as a percentage &mdash;
                for example 45 sold on a 60-seat bus is 75%. A seat counts as sold when its ticket is
                not cancelled, the same rule the booking screen uses to decide which seats are free,
                so this report and the seat map can never disagree.
                <em>Started</em> is the difference between the scheduled departure and the moment the
                driver actually started the trip; it is blank until a trip starts.
                <em>Carried</em> is the passenger count the driver recorded on completion, which may
                differ from seats sold if someone did not board.
            </div>

        </div>
    </div>
    </div>

</asp:Content>
