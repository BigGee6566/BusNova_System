<%@ Page Title="My Tickets" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="MyTickets.aspx.cs" Inherits="BusNova_System.MyTickets" %>
<asp:Content ID="Content1" ContentPlaceHolderID="head" runat="server">
    <link href="Styles/style.css?v=6" rel="stylesheet" />
    <link href="Styles/Ops.css?v=6" rel="stylesheet" />
</asp:Content>
<asp:Content ID="Content2" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">

    <div class="ops-page">
    <div class="ops-wrap" style="max-width: 1000px;">

        <a class="ops-back-link" href="Dashboard.aspx">&larr; Back to Dashboard</a>

        <div class="ops-box">

            <h3 class="ops-title">My Tickets</h3>
            <div class="ops-subtitle">
                Every ticket you've booked. Cancel a valid ticket any time before the trip departs
                — paid tickets are automatically marked refunded.
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
                                    Visible='<%# CanCancel(Eval("TicketStatus"), Eval("AssignmentDate")) %>'
                                    Text="Cancel" />
                                <asp:Label
                                    ID="lblNoAction"
                                    runat="server"
                                    Text="&mdash;"
                                    Visible='<%# !CanCancel(Eval("TicketStatus"), Eval("AssignmentDate")) %>' />
                            </ItemTemplate>
                        </asp:TemplateField>
                    </Columns>
                </asp:GridView>
            </div>

        </div>
    </div>
    </div>

</asp:Content>
