<%@ Page Title="Dashboard" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Dashboard.aspx.cs" Inherits="BusNova_System.Dashboard" %>
<asp:Content ID="Content1" ContentPlaceHolderID="head" runat="server">
    <link href="Styles/style.css?v=6" rel="stylesheet" />
    <link href="Styles/Ops.css?v=7" rel="stylesheet" />
    <%-- busnova-dashboard.css is loaded by Site.Master for every page. --%>
</asp:Content>
<asp:Content ID="Content2" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">

    <div class="dash-page">
    <div class="dash-wrap">

        <div class="dash-welcome">
            <h2><asp:Literal ID="litHeading" runat="server" /></h2>
            <p><asp:Literal ID="litSubheading" runat="server" /></p>
        </div>

        <%-- ================= MANAGER =================
             Folds in what used to be three separate dashboards
             (Administrator, Fleet Manager, Ticket Conductor). --%>
        <asp:PlaceHolder ID="phManager" runat="server" Visible="false">

            <div class="stat-grid">
                <div class="stat-card">
                    <span class="stat-value"><asp:Literal ID="litStaffCount" runat="server" /></span>
                    <span class="stat-caption">Staff accounts</span>
                </div>
                <div class="stat-card">
                    <span class="stat-value"><asp:Literal ID="litPassengerCount" runat="server" /></span>
                    <span class="stat-caption">Passenger accounts</span>
                </div>
                <div class="stat-card warn">
                    <span class="stat-value"><asp:Literal ID="litDeactivatedCount" runat="server" /></span>
                    <span class="stat-caption">Deactivated accounts</span>
                </div>
                <div class="stat-card">
                    <span class="stat-value"><asp:Literal ID="litBusCount" runat="server" /></span>
                    <span class="stat-caption">Buses in the fleet</span>
                </div>
                <div class="stat-card">
                    <span class="stat-value"><asp:Literal ID="litRouteCount" runat="server" /></span>
                    <span class="stat-caption">Active routes</span>
                </div>
                <div class="stat-card">
                    <span class="stat-value"><asp:Literal ID="litTripsToday" runat="server" /></span>
                    <span class="stat-caption">Trips scheduled today</span>
                </div>
                <div class="stat-card">
                    <span class="stat-value"><asp:Literal ID="litCompletedToday" runat="server" /></span>
                    <span class="stat-caption">Completed today</span>
                </div>
                <div class="stat-card">
                    <span class="stat-value"><asp:Literal ID="litConductorTickets" runat="server" /></span>
                    <span class="stat-caption">Tickets issued today</span>
                </div>
                <div class="stat-card">
                    <span class="stat-value"><asp:Literal ID="litRevenueToday" runat="server" /></span>
                    <span class="stat-caption">Revenue today</span>
                </div>
                <div class="stat-card">
                    <span class="stat-value"><asp:Literal ID="litRevenueMonth" runat="server" /></span>
                    <span class="stat-caption">Revenue, last 30 days</span>
                </div>
                <%-- The definition is on the page, not just in the code. A
                     percentage a manager cannot interrogate is a percentage
                     they will either over-trust or ignore. --%>
                <div class="stat-card"
                     title="Seats sold divided by seats offered, across all non-cancelled trips in the last 30 days. Seats sold counts Valid and Used tickets; cancelled tickets are excluded because they did not occupy a seat. Trips whose bus has no recorded capacity are left out of both sides rather than counted as zero.">
                    <span class="stat-value"><asp:Literal ID="litOccupancy" runat="server" /></span>
                    <span class="stat-caption">
                        Seat occupancy
                        <span class="bn-note"><asp:Literal ID="litOccupancyDetail" runat="server" /></span>
                    </span>
                </div>
                <asp:Panel ID="pnlOverdue" runat="server" CssClass="stat-card warn" Visible="false">
                    <span class="stat-value"><asp:Literal ID="litOverdue" runat="server" /></span>
                    <span class="stat-caption">Buses overdue for service</span>
                </asp:Panel>
            </div>

            <div class="dash-grid">
                <a class="dash-tile" href="ManageUsers.aspx">
                    Manage Users
                    <span class="dash-tile-desc">Create staff accounts, assign roles, activate or deactivate</span>
                </a>
                <a class="dash-tile" href="RegisterBus.aspx">
                    Register Bus
                    <span class="dash-tile-desc">Add a new bus to the fleet</span>
                </a>
                <a class="dash-tile" href="CreateRoute.aspx">
                    Create Route
                    <span class="dash-tile-desc">Define a new bus route</span>
                </a>
                <a class="dash-tile" href="AssignRoute.aspx">
                    Assign Route
                    <span class="dash-tile-desc">Assign a bus and driver to a trip</span>
                </a>
                <a class="dash-tile" href="IssueTicket.aspx">
                    Issue Ticket
                    <span class="dash-tile-desc">Sell a seat to a walk-up passenger, or look up a ticket</span>
                </a>
            </div>

            <div class="dash-section-title">Needs attention</div>
            <div class="dash-panel">
                <asp:GridView ID="gvAttention" runat="server" AutoGenerateColumns="false"
                              CssClass="ops-grid" GridLines="None" UseAccessibleHeader="true"
                              OnRowDataBound="gvAttention_RowDataBound"
                              EmptyDataText="Nothing needs attention right now.">
                    <Columns>
                        <asp:BoundField DataField="Category" HeaderText="Area" />
                        <asp:BoundField DataField="Subject" HeaderText="What" />
                        <asp:BoundField DataField="Detail" HeaderText="Why" />
                        <asp:TemplateField HeaderText="Priority">
                            <ItemTemplate>
                                <span class='<%# "status-badge " + SeverityCss(Eval("Severity")) %>'>
                                    <%# Server.HtmlEncode(Convert.ToString(Eval("Severity")) == "danger" ? "Act now" : "Soon") %>
                                </span>
                            </ItemTemplate>
                        </asp:TemplateField>
                    </Columns>
                </asp:GridView>
            </div>

            <div class="dash-section-title">Fleet</div>
            <div class="dash-panel">
<div class="ops-grid-wrap">
                    <asp:GridView ID="gvBuses" runat="server" CssClass="ops-grid"
                                  AutoGenerateColumns="false" GridLines="None"
                                  OnRowDataBound="gvBuses_RowDataBound"
                                  EmptyDataText="No buses registered yet. Use Register Bus to add the first one.">
                        <Columns>
                            <asp:BoundField DataField="RegistrationNumber" HeaderText="Registration" />
                            <asp:BoundField DataField="Model" HeaderText="Model" />
                            <asp:BoundField DataField="Manufacturer" HeaderText="Manufacturer" />
                            <asp:BoundField DataField="SeatingCapacity" HeaderText="Seats" />
                            <asp:BoundField DataField="FuelType" HeaderText="Fuel" />
                            <asp:BoundField DataField="YearOfManufacture" HeaderText="Year" />
                            <asp:TemplateField HeaderText="Status">
                                <ItemTemplate>
                                    <span class='<%# "status-badge " + StatusCss(Eval("Status")) %>'>
                                        <%# StatusText(Eval("Status")) %>
                                    </span>
                                </ItemTemplate>
                            </asp:TemplateField>
                            <asp:TemplateField HeaderText="Next service">
                                <ItemTemplate>
                                    <%# FormatServiceDate(Eval("NextServiceDueDate")) %>
                                </ItemTemplate>
                            </asp:TemplateField>
                        </Columns>
                    </asp:GridView>
                </div>
            </div>

            <div class="dash-section-title">Routes</div>
            <div class="dash-panel">
                <div class="ops-grid-wrap">
                    <asp:GridView ID="gvRoutes" runat="server" CssClass="ops-grid"
                                  AutoGenerateColumns="false" GridLines="None"
                                  OnRowDataBound="gvRoutes_RowDataBound"
                                  EmptyDataText="No routes created yet. Use Create Route to add the first one.">
                        <Columns>
                            <asp:BoundField DataField="RouteCode" HeaderText="Code" />
                            <asp:BoundField DataField="RouteName" HeaderText="Name" />
                            <asp:BoundField DataField="Origin" HeaderText="Origin" />
                            <asp:BoundField DataField="Destination" HeaderText="Destination" />
                            <asp:BoundField DataField="DistanceKm" HeaderText="Distance (km)" DataFormatString="{0:N2}" />
                            <asp:BoundField DataField="EstimatedDurationMinutes" HeaderText="Duration (min)" />
                        </Columns>
                    </asp:GridView>
                </div>
            </div>

            <div class="dash-section-title">Trips</div>
            <div class="dash-panel">
                <div class="ops-grid-wrap">
                    <asp:GridView ID="gvAssignments" runat="server" CssClass="ops-grid"
                                  AutoGenerateColumns="false" GridLines="None"
                                  OnRowDataBound="gvAssignments_RowDataBound"
                                  EmptyDataText="No trips assigned yet. Use Assign Route to schedule one.">
                        <Columns>
                            <asp:BoundField DataField="AssignmentId" HeaderText="#" />
                            <asp:BoundField DataField="RouteName" HeaderText="Route" />
                            <asp:BoundField DataField="BusRegistration" HeaderText="Bus" />
                            <asp:BoundField DataField="DriverName" HeaderText="Driver" />
                            <asp:BoundField DataField="AssignmentDate" HeaderText="Date" DataFormatString="{0:dd MMM yyyy}" />
                            <asp:TemplateField HeaderText="Departure">
                                <ItemTemplate><%# FormatTime(Eval("DepartureTime")) %></ItemTemplate>
                            </asp:TemplateField>
                            <asp:BoundField DataField="PassengerCount" HeaderText="Passengers" />
                            <asp:TemplateField HeaderText="Status">
                                <ItemTemplate>
                                    <span class='<%# "status-badge " + TripCss(Eval("TripStatus")) %>'>
                                        <%# StatusText(Eval("TripStatus")) %>
                                    </span>
                                </ItemTemplate>
                            </asp:TemplateField>
                        </Columns>
                    </asp:GridView>
                </div>
            </div>

        </asp:PlaceHolder>

        <%-- ================= DRIVER ================= --%>
        <asp:PlaceHolder ID="phDriver" runat="server" Visible="false">

            <div class="stat-grid">
                <div class="stat-card">
                    <span class="stat-value"><asp:Literal ID="litDriverUpcoming" runat="server" /></span>
                    <span class="stat-caption">Upcoming trips</span>
                </div>
            </div>

            <div class="dash-grid">
                <a class="dash-tile" href="ViewAssignedRoutes.aspx">
                    My Schedule
                    <span class="dash-tile-desc">View your assigned routes and start or complete a trip</span>
                </a>
                <a class="dash-tile" href="LogFuel.aspx">
                    Log Fuel
                    <span class="dash-tile-desc">Record a refuelling against a trip or bus</span>
                </a>
                <a class="dash-tile" href="ReportIssue.aspx">
                    Report Issue
                    <span class="dash-tile-desc">Tell maintenance about a problem with a bus</span>
                </a>
            </div>

            <div class="dash-section-title">Next trips</div>
            <div class="dash-panel">
                <div class="ops-grid-wrap">
                    <asp:GridView ID="gvDriverTrips" runat="server" CssClass="ops-grid"
                                  AutoGenerateColumns="false" GridLines="None"
                                  OnRowDataBound="gvDriverTrips_RowDataBound"
                                  EmptyDataText="No routes assigned yet.">
                        <Columns>
                            <asp:BoundField DataField="RouteName" HeaderText="Route" />
                            <asp:BoundField DataField="BusRegistration" HeaderText="Bus" />
                            <asp:BoundField DataField="AssignmentDate" HeaderText="Date" DataFormatString="{0:dd MMM yyyy}" />
                            <asp:TemplateField HeaderText="Departure">
                                <ItemTemplate><%# FormatTime(Eval("DepartureTime")) %></ItemTemplate>
                            </asp:TemplateField>
                            <asp:TemplateField HeaderText="Status">
                                <ItemTemplate>
                                    <span class='<%# "status-badge " + TripCss(Eval("TripStatus")) %>'>
                                        <%# StatusText(Eval("TripStatus")) %>
                                    </span>
                                </ItemTemplate>
                            </asp:TemplateField>
                        </Columns>
                    </asp:GridView>
                </div>
            </div>

        </asp:PlaceHolder>

        <%-- ================= MAINTENANCE STAFF ================= --%>
        <asp:PlaceHolder ID="phMaintenance" runat="server" Visible="false">

            <div class="stat-grid">
                <div class="stat-card warn">
                    <span class="stat-value"><asp:Literal ID="litMaintOverdue" runat="server" /></span>
                    <span class="stat-caption">Overdue for service</span>
                </div>
                <div class="stat-card">
                    <span class="stat-value"><asp:Literal ID="litMaintDueSoon" runat="server" /></span>
                    <span class="stat-caption">Due within 14 days</span>
                </div>
            </div>

            <div class="dash-grid">
                <a class="dash-tile" href="MaintenanceWork.aspx">
                    Maintenance
                    <span class="dash-tile-desc">View reported faults, start work, and log completed service</span>
                </a>
            </div>

            <div class="dash-section-title">Buses needing attention</div>
            <div class="dash-panel">
                <div class="ops-grid-wrap">
                    <asp:GridView ID="gvMaintenance" runat="server" CssClass="ops-grid"
                                  AutoGenerateColumns="false" GridLines="None"
                                  OnRowDataBound="gvMaintenance_RowDataBound"
                                  EmptyDataText="No buses are due for service in the next 14 days.">
                        <Columns>
                            <asp:BoundField DataField="RegistrationNumber" HeaderText="Registration" />
                            <asp:BoundField DataField="Model" HeaderText="Model" />
                            <asp:BoundField DataField="OdometerReading" HeaderText="Odometer" DataFormatString="{0:N0}" />
                            <asp:TemplateField HeaderText="Status">
                                <ItemTemplate>
                                    <span class='<%# "status-badge " + StatusCss(Eval("Status")) %>'>
                                        <%# StatusText(Eval("Status")) %>
                                    </span>
                                </ItemTemplate>
                            </asp:TemplateField>
                            <asp:TemplateField HeaderText="Next service">
                                <ItemTemplate><%# FormatServiceDate(Eval("NextServiceDueDate")) %></ItemTemplate>
                            </asp:TemplateField>
                        </Columns>
                    </asp:GridView>
                </div>
                <p class="empty-note bn-mt-4">
                    Use Maintenance above to start work or log a completed service.
                </p>
            </div>

        </asp:PlaceHolder>

        <%-- ================= PASSENGER ================= --%>
        <asp:PlaceHolder ID="phPassenger" runat="server" Visible="false">

            <div class="dash-grid">
                <a class="dash-tile" href="BookTrip.aspx">
                    Book Trip
                    <span class="dash-tile-desc">Search trips and book up to 5 seats, with simulated payment</span>
                </a>
                <a class="dash-tile" href="MyTickets.aspx">
                    My Tickets
                    <span class="dash-tile-desc">View or cancel your bookings</span>
                </a>
            </div>

            <div class="dash-section-title">Upcoming trips you can travel on</div>
            <div class="dash-panel">
                <div class="ops-grid-wrap">
                    <asp:GridView ID="gvPassengerTrips" runat="server" CssClass="ops-grid"
                                  AutoGenerateColumns="false" GridLines="None"
                                  OnRowDataBound="gvPassengerTrips_RowDataBound"
                                  EmptyDataText="There are no scheduled trips at the moment.">
                        <Columns>
                            <asp:BoundField DataField="RouteName" HeaderText="Route" />
                            <asp:BoundField DataField="Origin" HeaderText="From" />
                            <asp:BoundField DataField="Destination" HeaderText="To" />
                            <asp:BoundField DataField="AssignmentDate" HeaderText="Date" DataFormatString="{0:dd MMM yyyy}" />
                            <asp:TemplateField HeaderText="Departure">
                                <ItemTemplate><%# FormatTime(Eval("DepartureTime")) %></ItemTemplate>
                            </asp:TemplateField>
                            <asp:BoundField DataField="SeatsAvailable" HeaderText="Seats free" />
                        </Columns>
                    </asp:GridView>
                </div>
                <p class="empty-note bn-mt-4">
                    Use Buy Ticket above to book a seat on any of these trips.
                </p>
            </div>

        </asp:PlaceHolder>

    </div>
    </div>

</asp:Content>
