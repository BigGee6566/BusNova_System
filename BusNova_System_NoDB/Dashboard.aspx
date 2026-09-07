<%@ Page Title="Dashboard" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Dashboard.aspx.cs" Inherits="BusNova_System.Dashboard" %>
<asp:Content ID="Content1" ContentPlaceHolderID="head" runat="server">
    <link href="Styles/style.css?v=5" rel="stylesheet" />
    <link href="Styles/Ops.css?v=5" rel="stylesheet" />
    <style>
        .dash-page {
            min-height: calc(100vh - 200px);
            padding-top: 90px;
            background-color: #F5F7FB;
        }

        .dash-wrap {
            max-width: 1040px;
            margin: 0 auto;
            padding: 30px 20px 60px;
        }

        .dash-welcome { margin-bottom: 26px; }
        .dash-welcome h2 { margin: 0 0 6px; color: #14213D; }
        .dash-welcome p { color: #55607A; margin: 0; }

        /* Key operational numbers, shown before the actions. */
        .stat-grid {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(160px, 1fr));
            gap: 14px;
            margin-bottom: 26px;
        }

        .stat-card {
            background: #FFFFFF;
            border: 1px solid rgba(20,33,61,0.16);
            border-left: 3px solid #2563eb;
            border-radius: 12px;
            padding: 18px;
        }

        .stat-card .stat-value {
            display: block;
            font-size: 1.7rem;
            font-weight: 700;
            color: #14213D;
            line-height: 1.1;
        }

        .stat-card .stat-caption {
            display: block;
            font-size: 0.78rem;
            color: #6B7488;
            margin-top: 4px;
        }

        .stat-card.warn { border-left-color: #C0392B; }
        .stat-card.warn .stat-value { color: #C0392B; }

        .dash-grid {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(210px, 1fr));
            gap: 16px;
        }

        .dash-tile {
            display: block;
            padding: 22px;
            border-radius: 12px;
            border: 1px solid rgba(20,33,61,0.18);
            background: #FFFFFF;
            box-shadow: 0 4px 14px rgba(20,33,61,0.10);
            text-decoration: none !important;
            color: #14213D !important;
            font-weight: 600;
            transition: transform .15s ease, box-shadow .15s ease;
        }

        .dash-tile:hover {
            transform: translateY(-2px);
            box-shadow: 0 8px 20px rgba(20,33,61,0.12);
        }

        .dash-tile .dash-tile-desc {
            display: block;
            font-weight: 400;
            font-size: 0.82rem;
            color: #6B7488;
            margin-top: 4px;
        }

        .dash-section-title {
            margin: 34px 0 12px;
            font-size: 1.05rem;
            font-weight: 700;
            color: #14213D;
        }

        .dash-panel {
            background: #FFFFFF;
            border: 1px solid rgba(20,33,61,0.16);
            border-radius: 12px;
            padding: 18px;
        }

        .empty-note {
            color: #6B7488;
            font-size: 0.85rem;
            margin: 0;
        }
    </style>
</asp:Content>
<asp:Content ID="Content2" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">

    <div class="dash-page">
    <div class="dash-wrap">

        <div class="dash-welcome">
            <h2><asp:Literal ID="litHeading" runat="server" /></h2>
            <p><asp:Literal ID="litSubheading" runat="server" /></p>
        </div>

        <%-- ================= FLEET MANAGER ================= --%>
        <asp:PlaceHolder ID="phFleetManager" runat="server" Visible="false">

            <div class="stat-grid">
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
                <asp:Panel ID="pnlOverdue" runat="server" CssClass="stat-card warn" Visible="false">
                    <span class="stat-value"><asp:Literal ID="litOverdue" runat="server" /></span>
                    <span class="stat-caption">Buses overdue for service</span>
                </asp:Panel>
            </div>

            <div class="dash-grid">
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
            </div>

            <div class="dash-section-title">Fleet</div>
            <div class="dash-panel">
                <div class="ops-grid-wrap">
                    <asp:GridView ID="gvBuses" runat="server" CssClass="ops-grid"
                                  AutoGenerateColumns="false" GridLines="None"
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
                                        <%# Server.HtmlEncode(Convert.ToString(Eval("Status"))) %>
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
                                        <%# Server.HtmlEncode(Convert.ToString(Eval("TripStatus"))) %>
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
            </div>

            <div class="dash-section-title">Next trips</div>
            <div class="dash-panel">
                <div class="ops-grid-wrap">
                    <asp:GridView ID="gvDriverTrips" runat="server" CssClass="ops-grid"
                                  AutoGenerateColumns="false" GridLines="None"
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
                                        <%# Server.HtmlEncode(Convert.ToString(Eval("TripStatus"))) %>
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

            <div class="dash-section-title">Buses needing attention</div>
            <div class="dash-panel">
                <div class="ops-grid-wrap">
                    <asp:GridView ID="gvMaintenance" runat="server" CssClass="ops-grid"
                                  AutoGenerateColumns="false" GridLines="None"
                                  EmptyDataText="No buses are due for service in the next 14 days.">
                        <Columns>
                            <asp:BoundField DataField="RegistrationNumber" HeaderText="Registration" />
                            <asp:BoundField DataField="Model" HeaderText="Model" />
                            <asp:BoundField DataField="OdometerReading" HeaderText="Odometer" DataFormatString="{0:N0}" />
                            <asp:TemplateField HeaderText="Status">
                                <ItemTemplate>
                                    <span class='<%# "status-badge " + StatusCss(Eval("Status")) %>'>
                                        <%# Server.HtmlEncode(Convert.ToString(Eval("Status"))) %>
                                    </span>
                                </ItemTemplate>
                            </asp:TemplateField>
                            <asp:TemplateField HeaderText="Next service">
                                <ItemTemplate><%# FormatServiceDate(Eval("NextServiceDueDate")) %></ItemTemplate>
                            </asp:TemplateField>
                        </Columns>
                    </asp:GridView>
                </div>
                <p class="empty-note" style="margin-top:14px;">
                    Recording inspections and repairs arrives in the next iteration.
                    This view is read-only for now.
                </p>
            </div>

        </asp:PlaceHolder>

        <%-- ================= PASSENGER ================= --%>
        <asp:PlaceHolder ID="phPassenger" runat="server" Visible="false">

            <div class="dash-section-title">Upcoming trips you can travel on</div>
            <div class="dash-panel">
                <div class="ops-grid-wrap">
                    <asp:GridView ID="gvPassengerTrips" runat="server" CssClass="ops-grid"
                                  AutoGenerateColumns="false" GridLines="None"
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
                <p class="empty-note" style="margin-top:14px;">
                    Buying a ticket arrives in the next iteration. This view is read-only for now.
                </p>
            </div>

        </asp:PlaceHolder>

    </div>
    </div>

</asp:Content>
