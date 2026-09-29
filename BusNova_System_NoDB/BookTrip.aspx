<%@ Page Title="Book Trip" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="BookTrip.aspx.cs" Inherits="BusNova_System.BookTrip" %>
<asp:Content ID="Content1" ContentPlaceHolderID="head" runat="server">
    <link href="Styles/style.css?v=8" rel="stylesheet" />
    <link href="Styles/Ops.css?v=9" rel="stylesheet" />
    <link href="Styles/SeatMap.css?v=3" rel="stylesheet" />
    <script src="Scripts/seat-map.js?v=2"></script>
</asp:Content>
<asp:Content ID="Content2" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">

    <div class="ops-page">
    <div class="ops-wrap ops-wrap--default">

        <div class="ops-box">

            <h2 class="ops-title">Book a Trip</h2>
            <div class="ops-subtitle">
                Search for a scheduled trip, then pick a seat for yourself and up to four
                other travellers. You'll review everything on a payment summary before anything is charged.
            </div>

            <div class="row">
                <div class="col-md-4 mb-3">
                    <label for="<%= txtOrigin.ClientID %>">From</label>
                    <asp:TextBox ID="txtOrigin" runat="server" CssClass="form-control" placeholder="Any origin" />
                </div>
                <div class="col-md-4 mb-3">
                    <label for="<%= txtDestination.ClientID %>">To</label>
                    <asp:TextBox ID="txtDestination" runat="server" CssClass="form-control" placeholder="Any destination" />
                </div>
                <div class="col-md-4 mb-3">
                    <label for="<%= txtTravelDate.ClientID %>">Travel date</label>
                    <asp:TextBox ID="txtTravelDate" runat="server" TextMode="Date" CssClass="form-control" />
                </div>
            </div>

            <div class="ops-actions bn-mb-6">
                <asp:Button ID="btnSearch" runat="server" Text="Search trips"
                            CssClass="btn-ops-primary" CausesValidation="false"
                            OnClick="btnSearch_Click" />
                <asp:Button ID="btnClearSearch" runat="server" Text="Clear"
                            CssClass="btn-ops-secondary" CausesValidation="false"
                            OnClick="btnClearSearch_Click" />
            </div>

            <asp:Label ID="lblMessage" runat="server" CssClass="ops-message" />

            <div class="ops-grid-wrap">
                <asp:GridView
                    ID="gvTrips"
                    runat="server"
                    CssClass="ops-grid"
                    AutoGenerateColumns="false"
                    GridLines="None"
                    DataKeyNames="AssignmentId"
                    EmptyDataText="No bookable trips match that search."
                    OnRowCommand="gvTrips_RowCommand"
                    OnRowDataBound="gvTrips_RowDataBound">
                    <Columns>
                        <asp:BoundField DataField="RouteName" HeaderText="Route" />
                        <asp:TemplateField HeaderText="From / To">
                            <ItemTemplate>
                                <%# Server.HtmlEncode(Convert.ToString(Eval("Origin"))) %>
                                &rarr;
                                <%# Server.HtmlEncode(Convert.ToString(Eval("Destination"))) %>
                            </ItemTemplate>
                        </asp:TemplateField>
                        <asp:BoundField DataField="BusRegistration" HeaderText="Bus" />
                        <asp:BoundField DataField="AssignmentDate" HeaderText="Date" DataFormatString="{0:dd MMM yyyy}" />
                        <asp:TemplateField HeaderText="Departure">
                            <ItemTemplate><%# FormatTime(Eval("DepartureTime")) %></ItemTemplate>
                        </asp:TemplateField>
                        <asp:BoundField DataField="SeatsAvailable" HeaderText="Seats free" />
                        <asp:TemplateField HeaderText="Action">
                            <ItemTemplate>
                                <asp:LinkButton
                                    ID="btnBuy"
                                    runat="server"
                                    CommandName="BookTrip"
                                    CommandArgument='<%# Eval("AssignmentId") %>'
                                    CssClass="btn-ops-secondary"
                                    CausesValidation="false"
                                    Visible='<%# Convert.ToInt32(Eval("SeatsAvailable")) > 0 %>'
                                    Text="Book this trip" />
                                <asp:Label
                                    ID="lblFull"
                                    runat="server"
                                    Text="Fully booked"
                                    Visible='<%# Convert.ToInt32(Eval("SeatsAvailable")) <= 0 %>' />
                            </ItemTemplate>
                        </asp:TemplateField>
                    </Columns>
                </asp:GridView>
            </div>

            <%-- Shown once a trip has been picked, so the passenger can choose seats and enter traveller details. --%>
            <asp:Panel CssClass="bn-callout bn-mt-6" ID="pnlSeat" runat="server" Visible="false" data-role="seat-form"
                      >

                <h3 class="bn-subheading bn-subheading--sm">Choose seats</h3>
                <div class="ops-subtitle bn-mb-4">
                    <asp:Literal ID="litSeatTrip" runat="server" />
                    Select one seat per traveller (up to five in total).
                </div>

                <div class="seat-summary">
                    <div class="seat-summary-item">
                        <span class="seat-summary-value"><asp:Literal ID="litSeatCapacity" runat="server" /></span>
                        <span class="seat-summary-label">Capacity</span>
                    </div>
                    <div class="seat-summary-item">
                        <span class="seat-summary-value"><asp:Literal ID="litSeatBooked" runat="server" /></span>
                        <span class="seat-summary-label">Booked</span>
                    </div>
                    <div class="seat-summary-item seat-summary-selected">
                        <span class="seat-summary-value" data-role="seat-selected-count">0</span>
                        <span class="seat-summary-label">Selected</span>
                    </div>
                    <div class="seat-summary-item">
                        <span class="seat-summary-value" data-role="seat-available-count"><asp:Literal ID="litSeatAvailable" runat="server" /></span>
                        <span class="seat-summary-label">Available</span>
                    </div>
                </div>

                <div class="seat-legend">
                    <span class="seat-legend-item"><span class="seat-legend-swatch legend-available"></span> Available</span>
                    <span class="seat-legend-item"><span class="seat-legend-swatch legend-selected"></span> Selected</span>
                    <span class="seat-legend-item"><span class="seat-legend-swatch legend-booked"></span> Booked / unavailable</span>
                    <span class="seat-legend-item"><span class="seat-legend-swatch legend-driver"></span> Driver (not selectable)</span>
                </div>

                <div class="seat-map-wrap">
                    <div id="divSeatContainer" runat="server" data-role="seat-container">
                        <asp:Literal ID="litSeatMap" runat="server" Mode="PassThrough" />
                    </div>
                </div>

                <hr />

                <h3 class="bn-subheading bn-subheading--sm">Traveller details</h3>

                <%-- Five fixed rows - simpler and more in keeping with this app's
                     declarative WebForms style than generating a variable-length
                     list, and TicketService.MaxTravellersPerBooking is a hard
                     cap of 5 anyway, so there is never a sixth row to render.
                     seat-map.js shows/hides each row and fills in its seat
                     label as seats are picked above. --%>
                <asp:Panel ID="pnlTraveller1" runat="server" CssClass="traveller-row" data-role="traveller-row" data-index="1">
                    <div class="traveller-row-title">Traveller 1 &mdash; seat <span data-role="traveller-seat-label"></span></div>
                    <div class="row">
                        <div class="col-md-7 mb-2">
                            <label for="<%= txtTraveller1Name.ClientID %>">Full name<span class="required-mark">*</span></label>
                            <asp:TextBox ID="txtTraveller1Name" runat="server" CssClass="form-control" MaxLength="100" />
                        </div>
                        <div class="col-md-5 mb-2">
                            <label for="<%= txtTraveller1Reference.ClientID %>">Student / ID number <span class="empty-note">(optional)</span></label>
                            <asp:TextBox ID="txtTraveller1Reference" runat="server" CssClass="form-control" MaxLength="30" />
                        </div>
                    </div>
                </asp:Panel>

                <asp:Panel ID="pnlTraveller2" runat="server" CssClass="traveller-row" data-role="traveller-row" data-index="2">
                    <div class="traveller-row-title">Traveller 2 &mdash; seat <span data-role="traveller-seat-label"></span></div>
                    <div class="row">
                        <div class="col-md-7 mb-2">
                            <label for="<%= txtTraveller2Name.ClientID %>">Full name<span class="required-mark">*</span></label>
                            <asp:TextBox ID="txtTraveller2Name" runat="server" CssClass="form-control" MaxLength="100" />
                        </div>
                        <div class="col-md-5 mb-2">
                            <label for="<%= txtTraveller2Reference.ClientID %>">Student / ID number <span class="empty-note">(optional)</span></label>
                            <asp:TextBox ID="txtTraveller2Reference" runat="server" CssClass="form-control" MaxLength="30" />
                        </div>
                    </div>
                </asp:Panel>

                <asp:Panel ID="pnlTraveller3" runat="server" CssClass="traveller-row" data-role="traveller-row" data-index="3">
                    <div class="traveller-row-title">Traveller 3 &mdash; seat <span data-role="traveller-seat-label"></span></div>
                    <div class="row">
                        <div class="col-md-7 mb-2">
                            <label for="<%= txtTraveller3Name.ClientID %>">Full name<span class="required-mark">*</span></label>
                            <asp:TextBox ID="txtTraveller3Name" runat="server" CssClass="form-control" MaxLength="100" />
                        </div>
                        <div class="col-md-5 mb-2">
                            <label for="<%= txtTraveller3Reference.ClientID %>">Student / ID number <span class="empty-note">(optional)</span></label>
                            <asp:TextBox ID="txtTraveller3Reference" runat="server" CssClass="form-control" MaxLength="30" />
                        </div>
                    </div>
                </asp:Panel>

                <asp:Panel ID="pnlTraveller4" runat="server" CssClass="traveller-row" data-role="traveller-row" data-index="4">
                    <div class="traveller-row-title">Traveller 4 &mdash; seat <span data-role="traveller-seat-label"></span></div>
                    <div class="row">
                        <div class="col-md-7 mb-2">
                            <label for="<%= txtTraveller4Name.ClientID %>">Full name<span class="required-mark">*</span></label>
                            <asp:TextBox ID="txtTraveller4Name" runat="server" CssClass="form-control" MaxLength="100" />
                        </div>
                        <div class="col-md-5 mb-2">
                            <label for="<%= txtTraveller4Reference.ClientID %>">Student / ID number <span class="empty-note">(optional)</span></label>
                            <asp:TextBox ID="txtTraveller4Reference" runat="server" CssClass="form-control" MaxLength="30" />
                        </div>
                    </div>
                </asp:Panel>

                <asp:Panel ID="pnlTraveller5" runat="server" CssClass="traveller-row" data-role="traveller-row" data-index="5">
                    <div class="traveller-row-title">Traveller 5 &mdash; seat <span data-role="traveller-seat-label"></span></div>
                    <div class="row">
                        <div class="col-md-7 mb-2">
                            <label for="<%= txtTraveller5Name.ClientID %>">Full name<span class="required-mark">*</span></label>
                            <asp:TextBox ID="txtTraveller5Name" runat="server" CssClass="form-control" MaxLength="100" />
                        </div>
                        <div class="col-md-5 mb-2">
                            <label for="<%= txtTraveller5Reference.ClientID %>">Student / ID number <span class="empty-note">(optional)</span></label>
                            <asp:TextBox ID="txtTraveller5Reference" runat="server" CssClass="form-control" MaxLength="30" />
                        </div>
                    </div>
                </asp:Panel>

                <div class="ops-actions">
                    <%-- Left enabled server-side on purpose - see IssueTicket.aspx
                         for why. seat-map.js disables it client-side until a
                         seat is chosen, purely as a UX cue. --%>
                    <asp:Button ID="btnContinueToPayment" runat="server"
                                Text="Continue to payment"
                                CssClass="btn-ops-primary"
                                data-role="seat-confirm-btn"
                                OnClick="btnContinueToPayment_Click" />
                    <asp:Button ID="btnCancelPurchase" runat="server"
                                Text="Cancel"
                                CssClass="btn-ops-secondary"
                                CausesValidation="false"
                                OnClick="btnCancelPurchase_Click" />
                </div>

                <asp:HiddenField ID="hfSeatAssignmentId" runat="server" />
                <input type="hidden" id="hfSelectedSeats" runat="server" data-role="selected-seat-input" />

            </asp:Panel>

        </div>
    </div>
    </div>

</asp:Content>
