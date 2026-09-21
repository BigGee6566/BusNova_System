<%@ Page Title="Issue Ticket" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="IssueTicket.aspx.cs" Inherits="BusNova_System.IssueTicket" %>
<asp:Content ID="Content1" ContentPlaceHolderID="head" runat="server">
    <link href="Styles/style.css?v=6" rel="stylesheet" />
    <link href="Styles/Ops.css?v=6" rel="stylesheet" />
    <link href="Styles/SeatMap.css?v=2" rel="stylesheet" />
    <script src="Scripts/seat-map.js?v=1"></script>
</asp:Content>
<asp:Content ID="Content2" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">

    <div class="ops-page">
    <div class="ops-wrap" style="max-width: 1000px;">

        <a class="ops-back-link" href="Dashboard.aspx">&larr; Back to Dashboard</a>

        <div class="ops-box">

            <h3 class="ops-title">Issue Ticket</h3>
            <div class="ops-subtitle">
                Pick the trip you're working, find (or register) the passenger, then issue their ticket.
            </div>

            <div class="row">
                <div class="col-md-4 mb-3">
                    <label for="<%= txtDate.ClientID %>">Date<span class="required-mark">*</span></label>
                    <asp:TextBox ID="txtDate" runat="server" TextMode="Date" CssClass="form-control"
                                 AutoPostBack="true" OnTextChanged="txtDate_TextChanged" />
                </div>
                <div class="col-md-8 mb-3">
                    <label for="<%= ddlTrip.ClientID %>">Trip<span class="required-mark">*</span></label>
                    <asp:DropDownList ID="ddlTrip" runat="server" CssClass="form-select"
                                       AutoPostBack="true" OnSelectedIndexChanged="ddlTrip_SelectedIndexChanged">
                        <asp:ListItem Text="-- Select a trip --" Value="" />
                    </asp:DropDownList>
                </div>
            </div>

            <asp:Panel ID="pnlTripInfo" runat="server" Visible="false" CssClass="empty-note" style="margin-bottom:18px;">
                <asp:Literal ID="litTripInfo" runat="server" />
            </asp:Panel>

            <div class="ops-grid-wrap" style="margin-bottom:22px;">
                <asp:GridView ID="gvTripTickets" runat="server" CssClass="ops-grid"
                              AutoGenerateColumns="false" GridLines="None"
                              OnRowDataBound="gvTripTickets_RowDataBound"
                              EmptyDataText="No tickets issued for this trip yet.">
                    <Columns>
                        <asp:BoundField DataField="SeatNumber" HeaderText="Seat" />
                        <asp:BoundField DataField="PassengerName" HeaderText="Passenger" />
                        <asp:BoundField DataField="TicketNumber" HeaderText="Ticket #" />
                        <asp:TemplateField HeaderText="Status">
                            <ItemTemplate>
                                <span class='<%# "status-badge " + (Convert.ToString(Eval("TicketStatus")) == "Cancelled" ? "status-muted" : "status-completed") %>'>
                                    <%# Server.HtmlEncode(Convert.ToString(Eval("TicketStatus"))) %>
                                </span>
                            </ItemTemplate>
                        </asp:TemplateField>
                    </Columns>
                </asp:GridView>
            </div>

            <hr />

            <h4 style="font-size:1rem; color:var(--bn-text);">Find or register the passenger</h4>
            <div class="row">
                <div class="col-md-8 mb-3">
                    <label for="<%= txtContact.ClientID %>">Passenger email or phone</label>
                    <asp:TextBox ID="txtContact" runat="server" CssClass="form-control"
                                 placeholder="e.g. name@example.com or 0821234567" />
                </div>
                <div class="col-md-4 mb-3" style="display:flex; align-items:flex-end;">
                    <asp:Button ID="btnFindPassenger" runat="server" Text="Find passenger"
                                CssClass="btn-ops-secondary" CausesValidation="false"
                                OnClick="btnFindPassenger_Click" style="width:100%;" />
                </div>
            </div>

            <asp:Panel ID="pnlFound" runat="server" Visible="false" CssClass="empty-note" style="margin-bottom:14px;">
                Passenger found: <asp:Literal ID="litFoundName" runat="server" />
            </asp:Panel>

            <asp:Panel ID="pnlRegister" runat="server" Visible="false"
                       style="margin-bottom:18px; padding:16px; border:1px solid rgba(var(--bn-navy-rgb),0.16); border-radius:12px; background:var(--bn-blue-pale);">
                <div class="empty-note" style="margin-bottom:10px;">
                    No account found for that email or phone. Register the passenger now so their ticket can be issued.
                </div>
                <div class="row">
                    <div class="col-md-3 mb-3">
                        <label for="<%= txtRegFirstName.ClientID %>">First name<span class="required-mark">*</span></label>
                        <asp:TextBox ID="txtRegFirstName" runat="server" CssClass="form-control" MaxLength="50" />
                    </div>
                    <div class="col-md-3 mb-3">
                        <label for="<%= txtRegLastName.ClientID %>">Last name<span class="required-mark">*</span></label>
                        <asp:TextBox ID="txtRegLastName" runat="server" CssClass="form-control" MaxLength="50" />
                    </div>
                    <div class="col-md-3 mb-3">
                        <label for="<%= txtRegEmail.ClientID %>">Email<span class="required-mark">*</span></label>
                        <asp:TextBox ID="txtRegEmail" runat="server" CssClass="form-control" MaxLength="100" />
                    </div>
                    <div class="col-md-3 mb-3">
                        <label for="<%= txtRegPhone.ClientID %>">Phone<span class="required-mark">*</span></label>
                        <asp:TextBox ID="txtRegPhone" runat="server" CssClass="form-control" MaxLength="15" />
                    </div>
                </div>
                <asp:Button ID="btnRegisterPassenger" runat="server" Text="Register passenger"
                            CssClass="btn-ops-secondary" CausesValidation="false"
                            OnClick="btnRegisterPassenger_Click" />
            </asp:Panel>

            <asp:Panel ID="pnlPasswordSetup" runat="server" Visible="false"
                       style="margin-bottom:18px; padding:16px; border:1px solid rgba(30,138,95,0.35); border-radius:12px; background:rgba(30,138,95,0.08);">
                <div style="margin-bottom:8px;">
                    Read this link to the passenger, or let them copy it, so they can set their own
                    password before they try to sign in. It works once and expires in 48 hours.
                </div>
                <asp:TextBox ID="txtSetupLink" runat="server" CssClass="form-control" ReadOnly="true" />
                <div style="margin-top:8px;">
                    <asp:HyperLink ID="hlSetupLink" runat="server" Target="_blank">Open this link</asp:HyperLink>
                </div>
            </asp:Panel>

            <asp:Panel ID="pnlIssue" runat="server" Visible="false" data-role="seat-form">
                <hr />
                <h4 style="font-size:1rem; color:var(--bn-text);">Issue the ticket</h4>

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

                <div class="ops-actions">
                    <%-- Left enabled server-side on purpose - see BookTrip.aspx for why. --%>
                    <asp:Button ID="btnIssue" runat="server" Text="Issue ticket (paid)"
                                CssClass="btn-ops-primary" data-role="seat-confirm-btn"
                                CausesValidation="false"
                                OnClick="btnIssue_Click" />
                </div>

                <input type="hidden" id="hfSelectedSeat" runat="server" data-role="selected-seat-input" />
            </asp:Panel>

            <asp:Label ID="lblMessage" runat="server" CssClass="ops-message" />

            <asp:HiddenField ID="hfPassengerId" runat="server" />
            <asp:HiddenField ID="hfPassengerName" runat="server" />

        </div>

        <div class="ops-box" style="margin-top:24px;">
            <h3 class="ops-title" style="font-size:1.1rem;">Validate a ticket</h3>
            <div class="row">
                <div class="col-md-8 mb-3">
                    <label for="<%= txtValidateNumber.ClientID %>">Ticket number</label>
                    <asp:TextBox ID="txtValidateNumber" runat="server" CssClass="form-control" placeholder="e.g. BN-000012" />
                </div>
                <div class="col-md-4 mb-3" style="display:flex; align-items:flex-end;">
                    <asp:Button ID="btnValidate" runat="server" Text="Look up"
                                CssClass="btn-ops-secondary" CausesValidation="false"
                                OnClick="btnValidate_Click" style="width:100%;" />
                </div>
            </div>
            <asp:Label ID="lblValidateResult" runat="server" CssClass="ops-message" />

            <%-- Boarding is a separate, explicit action from looking a ticket
                 up. A conductor checking whether a ticket is valid must not
                 consume it by doing so, and the two have different
                 consequences: one reads, one cannot be undone. --%>
            <input type="hidden" id="hfBoardingTicketId" runat="server" />

            <asp:Panel ID="pnlBoarding" runat="server" Visible="false" CssClass="bn-boarding">
                <div class="bn-boarding__summary">
                    <asp:Literal ID="litBoardingTicket" runat="server" />
                </div>
                <asp:Button ID="btnMarkBoarded" runat="server" Text="Mark as boarded"
                            CssClass="btn-ops-primary" CausesValidation="false"
                            OnClick="btnMarkBoarded_Click" />
                <div class="bn-boarding__note">
                    This records the passenger as having boarded. A ticket can only be
                    boarded once.
                </div>
            </asp:Panel>
        </div>

    </div>
    </div>

</asp:Content>
