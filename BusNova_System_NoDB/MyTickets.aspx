<%@ Page Title="My Tickets" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="MyTickets.aspx.cs" Inherits="BusNova_System.MyTickets" %>
<asp:Content ID="Content1" ContentPlaceHolderID="head" runat="server">
    <link href="Styles/style.css?v=6" rel="stylesheet" />
    <link href="Styles/Ops.css?v=6" rel="stylesheet" />
</asp:Content>
<asp:Content ID="Content2" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">

    <div class="ops-page">
    <div class="ops-wrap ops-wrap--default">

        <a class="ops-back-link" href="Dashboard.aspx">&larr; Back to Dashboard</a>

        <div class="ops-box">

            <h3 class="ops-title">My Tickets</h3>
            <div class="ops-subtitle">
                Every ticket you&rsquo;ve booked. You can cancel a valid ticket up until the trip
                departs; what you get back depends on how close to departure you cancel, and the
                exact amount is shown to you when you do.
            </div>

            <asp:Label ID="lblMessage" runat="server" CssClass="ops-message" />

            <div class="ops-grid-wrap">
                <asp:GridView
                    ID="gvTickets"
                    runat="server"
                    CssClass="ops-grid"
                    AutoGenerateColumns="false"
                    GridLines="None"
                    DataKeyNames="TicketId"
                    EmptyDataText="You haven't booked any tickets yet. Use Book Trip to book your first trip."
                    OnRowCommand="gvTickets_RowCommand"
                    OnRowDataBound="gvTickets_RowDataBound">
                    <Columns>
                        <asp:BoundField DataField="TicketNumber" HeaderText="Ticket #" />
                        <asp:BoundField DataField="RouteName" HeaderText="Route" />
                        <asp:BoundField DataField="AssignmentDate" HeaderText="Date" DataFormatString="{0:dd MMM yyyy}" />
                        <asp:TemplateField HeaderText="Departure">
                            <ItemTemplate><%# FormatTime(Eval("DepartureTime")) %></ItemTemplate>
                        </asp:TemplateField>
                        <asp:BoundField DataField="SeatNumber" HeaderText="Seat" />
                        <asp:TemplateField HeaderText="Price">
                            <ItemTemplate><%# FormatPrice(Eval("TicketPrice")) %></ItemTemplate>
                        </asp:TemplateField>
                        <asp:TemplateField HeaderText="Payment">
                            <ItemTemplate>
                                <%# Server.HtmlEncode(Convert.ToString(Eval("PaymentStatus"))) %>
                            </ItemTemplate>
                        </asp:TemplateField>
                        <asp:TemplateField HeaderText="Status">
                            <ItemTemplate>
                                <span class='<%# "status-badge " + StatusCss(Eval("TicketStatus")) %>'>
                                    <%# Server.HtmlEncode(Convert.ToString(Eval("TicketStatus"))) %>
                                </span>
                            </ItemTemplate>
                        </asp:TemplateField>
                        <asp:TemplateField HeaderText="Action">
                            <ItemTemplate>
                                <asp:HyperLink
                                    ID="hlViewTicket"
                                    runat="server"
                                    NavigateUrl='<%# "TicketConfirmation.aspx?bookingId=" + Eval("BookingId") %>'
                                    CssClass="btn-ops-secondary"
                                    Style="text-decoration:none; display:inline-block; margin-bottom:4px;"
                                    Visible='<%# Eval("BookingId") != null %>'
                                    Text="View ticket" />
                                <asp:LinkButton
                                    ID="btnCancelTicket"
                                    runat="server"
                                    CommandName="CancelTicket"
                                    CommandArgument='<%# Eval("TicketId") %>'
                                    CssClass="btn-ops-secondary"
                                    CausesValidation="false"
                                    data-bn-confirm-title="Cancel this ticket?"
                                    data-bn-confirm='<%# "Ticket " + Server.HtmlEncode(Convert.ToString(Eval("TicketNumber"))) + " will be cancelled and the seat released. This cannot be undone." %>'
                                    data-bn-confirm-ok="Cancel ticket" 
                                    Visible='<%# CanCancel(Eval("TicketStatus"), Eval("AssignmentDate"), Eval("DepartureTime")) %>'
                                    Text="Cancel" />
                                <asp:Label
                                    ID="lblNoAction"
                                    runat="server"
                                    Text="&mdash;"
                                    Visible='<%# !CanCancel(Eval("TicketStatus"), Eval("AssignmentDate"), Eval("DepartureTime")) %>' />
                            </ItemTemplate>
                        </asp:TemplateField>
                    </Columns>
                </asp:GridView>
            </div>

        </div>

        <div class="ops-box bn-mt-3">

            <h3 class="ops-title">My bookings</h3>
            <div class="ops-subtitle">
                A booking is one payment covering one or more travellers on the same trip.
                Tickets bought one at a time at a counter do not have one.
            </div>

            <div class="bn-field">
                <label for="<%= txtReference.ClientID %>">Find a booking by reference</label>
                <asp:TextBox ID="txtReference" runat="server" CssClass="form-control"
                             placeholder="BNB-000001" MaxLength="20" />
                <asp:Button ID="btnFindBooking" runat="server" Text="Find booking"
                            CssClass="bn-btn bn-btn--secondary" OnClick="btnFindBooking_Click" />
            </div>

            <asp:Label ID="lblBookingMessage" runat="server" />

            <div class="ops-grid-wrap">
                <asp:GridView ID="gvBookings" runat="server" AutoGenerateColumns="false"
                              CssClass="ops-grid" GridLines="None" UseAccessibleHeader="true"
                              OnRowDataBound="gvBookings_RowDataBound"
                              EmptyDataText="You have no bookings yet. Tickets bought at a counter appear above instead.">
                    <Columns>
                        <asp:BoundField DataField="BookingReference" HeaderText="Reference" />
                        <asp:BoundField DataField="RouteName" HeaderText="Route" />
                        <asp:BoundField DataField="AssignmentDate" HeaderText="Travel date" DataFormatString="{0:dd MMM yyyy}" />
                        <asp:TemplateField HeaderText="Departs">
                            <ItemTemplate><%# FormatTime(Eval("DepartureTime")) %></ItemTemplate>
                        </asp:TemplateField>
                        <asp:TemplateField HeaderText="Total">
                            <ItemTemplate><%# FormatPrice(Eval("TotalAmount")) %></ItemTemplate>
                        </asp:TemplateField>
                        <asp:TemplateField HeaderText="Payment">
                            <ItemTemplate>
                                <span class='<%# "status-badge " + PaymentCss(Eval("PaymentStatus")) %>'>
                                    <%# Server.HtmlEncode(Convert.ToString(Eval("PaymentStatus"))) %>
                                </span>
                            </ItemTemplate>
                        </asp:TemplateField>
                        <asp:TemplateField HeaderText="">
                            <ItemTemplate>
                                <a class="bn-btn bn-btn--secondary"
                                   href='<%# "TicketConfirmation.aspx?bookingId=" + Eval("BookingId") %>'>
                                    View tickets
                                </a>
                            </ItemTemplate>
                        </asp:TemplateField>
                    </Columns>
                </asp:GridView>
            </div>

        </div>
    </div>
    </div>

</asp:Content>
