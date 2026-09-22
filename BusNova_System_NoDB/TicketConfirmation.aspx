<%@ Page Title="Your Tickets" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="TicketConfirmation.aspx.cs" Inherits="BusNova_System.TicketConfirmation" %>
<asp:Content ID="Content1" ContentPlaceHolderID="head" runat="server">
    <link href="Styles/style.css?v=6" rel="stylesheet" />
    <link href="Styles/Ops.css?v=6" rel="stylesheet" />
    <link href="Styles/Ticket.css?v=1" rel="stylesheet" />
    <script src="https://cdnjs.cloudflare.com/ajax/libs/qrcodejs/1.0.0/qrcode.min.js"></script>
    <script src="Scripts/ticket-confirmation.js?v=1"></script>
</asp:Content>
<asp:Content ID="Content2" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">

    <div class="ops-page">
    <div class="ops-wrap ops-wrap--narrow">

        <a class="ops-back-link no-print" href="MyTickets.aspx">&larr; Back to My Tickets</a>

        <asp:Panel ID="pnlConfirmed" runat="server" Visible="false">

            <div class="ops-box no-print">
                <span class="bn-badge bn-badge--success">
                    <i class="fas fa-circle-check" aria-hidden="true"></i> Payment successful
                </span>
                <h3 class="ops-title bn-mt-3">Booking <asp:Literal ID="litBookingReference" runat="server" /></h3>
                <div class="ops-subtitle">
                    <asp:Literal ID="litBookingSummary" runat="server" />
                </div>

                <div class="ops-actions">
                    <asp:Button ID="btnPrint" runat="server" Text="Print tickets"
                                CssClass="btn-ops-secondary" CausesValidation="false"
                                OnClientClick="window.print(); return false;" />
                    <asp:Button ID="btnSendEmail" runat="server" Text="Send to my email"
                                CssClass="btn-ops-secondary" CausesValidation="false"
                                OnClick="btnSendEmail_Click" />
                    <a class="bn-btn bn-btn--primary" href="MyTickets.aspx">
                        View all my tickets
                    </a>
                </div>

                <asp:Label ID="lblMessage" runat="server" CssClass="ops-message" />
            </div>

            <asp:Repeater ID="rptTickets" runat="server">
                <ItemTemplate>
                    <div class="e-ticket">
                        <div class="e-ticket-header">
                            <div class="e-ticket-brand">
                                <img src="Images/logo.jpeg" alt="BusNova" class="e-ticket-logo" />
                                <span>BusNova</span>
                            </div>
                            <span class='<%# "e-ticket-status e-ticket-status-" + Convert.ToString(Eval("TicketStatus")).ToLowerInvariant() %>'>
                                <%# Server.HtmlEncode(Convert.ToString(Eval("TicketStatus"))) %>
                            </span>
                        </div>

                        <div class="e-ticket-body">
                            <div class="e-ticket-route">
                                <div class="e-ticket-route-point">
                                    <span class="e-ticket-route-label">Boarding point</span>
                                    <span class="e-ticket-route-value"><%# Server.HtmlEncode(Convert.ToString(Eval("Origin"))) %></span>
                                </div>
                                <i class="fas fa-arrow-right-long" aria-hidden="true"></i>
                                <div class="e-ticket-route-point">
                                    <span class="e-ticket-route-label">Destination</span>
                                    <span class="e-ticket-route-value"><%# Server.HtmlEncode(Convert.ToString(Eval("Destination"))) %></span>
                                </div>
                            </div>

                            <div class="e-ticket-grid">
                                <div><span>Traveller</span><strong><%# Server.HtmlEncode(Convert.ToString(Eval("PassengerName"))) %></strong></div>
                                <div><span>Route</span><strong><%# Server.HtmlEncode(Convert.ToString(Eval("RouteName"))) %></strong></div>
                                <div><span>Travel date</span><strong><%# Convert.ToDateTime(Eval("AssignmentDate")).ToString("dd MMM yyyy") %></strong></div>
                                <div><span>Departure</span><strong><%# FormatTime(Eval("DepartureTime")) %></strong></div>
                                <div><span>Bus</span><strong><%# Server.HtmlEncode(Convert.ToString(Eval("BusRegistration"))) %></strong></div>
                                <div><span>Seat</span><strong><%# Server.HtmlEncode(Convert.ToString(Eval("SeatNumber"))) %></strong></div>
                                <div><span>Fare paid</span><strong><%# FormatFare(Eval("TicketPrice")) %></strong></div>
                                <div><span>Issued</span><strong><%# Convert.ToDateTime(Eval("PurchaseDate")).ToString("dd MMM yyyy HH:mm") %></strong></div>
                            </div>

                            <div class="e-ticket-footer">
                                <div class="e-ticket-numbers">
                                    <div><span>Ticket number</span><strong><%# Server.HtmlEncode(Convert.ToString(Eval("TicketNumber"))) %></strong></div>
                                    <div><span>Booking reference</span><strong><%# Server.HtmlEncode(BookingReference) %></strong></div>
                                    <p class="e-ticket-note">Present this ticket when boarding.</p>
                                </div>
                                <div class="e-ticket-qr" data-role="ticket-qr" data-token='<%# QrToken(Eval("TicketId")) %>'></div>
                            </div>
                        </div>
                    </div>
                </ItemTemplate>
            </asp:Repeater>

        </asp:Panel>

        <asp:Panel ID="pnlNotFound" runat="server" Visible="false" CssClass="ops-box">
            <h3 class="ops-title">Booking not found</h3>
            <div class="ops-subtitle">
                <asp:Literal ID="litNotFoundMessage" runat="server" />
            </div>
            <a class="bn-btn bn-btn--primary" href="MyTickets.aspx">
                Go to My Tickets
            </a>
        </asp:Panel>

    </div>
    </div>

</asp:Content>
