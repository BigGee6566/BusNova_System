<%@ Page Title="Validate Ticket" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="ValidateTicket.aspx.cs" Inherits="BusNova_System.ValidateTicket" %>
<asp:Content ID="Content1" ContentPlaceHolderID="head" runat="server">
    <link href="Styles/Ops.css?v=9" rel="stylesheet" />
</asp:Content>
<asp:Content ID="Content2" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">

    <div class="ops-wrap">

        <%-- The page title is rendered once, by Site.Master's top bar. --%>
        <p class="bn-page-intro">
            Scan the passenger's QR code, or type the ticket number if the code will not scan.
        </p>

        <%-- The outcome of the last attempt sits above the forms: it is the
             first thing the person at the door needs after a scan. --%>
        <asp:Label ID="lblMessage" runat="server" />

        <%-- The trip applies to both ways of presenting a ticket, so it spans
             the top; the two ways sit side by side beneath it. --%>
        <div class="bn-cols-2 bn-mt-4">

            <div class="bn-card bn-span-full">
                <div class="bn-field bn-measure">
                    <label for="<%= ddlTrip.ClientID %>">Trip being boarded</label>
                    <asp:DropDownList ID="ddlTrip" runat="server" CssClass="form-control" />
                    <p class="bn-note">
                        Leave as &ldquo;Any trip&rdquo; to accept a valid ticket for any departure. Choosing a
                        trip means a ticket for a different departure is refused with its own message,
                        rather than being boarded onto the wrong bus.
                    </p>
                </div>
            </div>

            <%-- Left: the two ways to present a ticket. Right: what came of
                 the last one. On a laptop the result is beside the input,
                 near the top of the screen, not below it. --%>
            <div class="bn-stack">

            <asp:Panel ID="pnlScan" runat="server" DefaultButton="btnValidateToken" CssClass="bn-card">
                <h2 class="bn-subheading">Scan the code</h2>
                <div class="bn-field">
                    <label for="<%= txtToken.ClientID %>">Scanned code</label>
                    <asp:TextBox ID="txtToken" runat="server" CssClass="form-control bn-scan-input"
                                 placeholder="Waiting for scan&hellip;" autocomplete="off"
                                 autocapitalize="off" spellcheck="false" />
                    <p class="bn-note">
                        A hardware scanner types the code into this box and presses Enter.
                    </p>
                    <asp:Button ID="btnValidateToken" runat="server" Text="Validate scanned code"
                                CssClass="bn-btn bn-btn--primary bn-w-full" OnClick="btnValidateToken_Click" />
                </div>
            </asp:Panel>

            <div class="bn-card">
                <h2 class="bn-subheading">Code will not scan?</h2>
                <div class="bn-field">
                    <label for="<%= txtTicketNumber.ClientID %>">Ticket number printed on the ticket</label>
                    <asp:TextBox ID="txtTicketNumber" runat="server" CssClass="form-control"
                                 placeholder="BN-001009" autocomplete="off" MaxLength="20" />
                    <asp:Button ID="btnValidateNumber" runat="server" Text="Validate ticket number"
                                CssClass="bn-btn bn-btn--secondary bn-w-full" OnClick="btnValidateNumber_Click" />
                </div>
            </div>

            </div>

        <asp:Panel ID="pnlAwaiting" runat="server" CssClass="bn-card">
            <div class="bn-state">
                <i class="fas fa-qrcode bn-state__icon" aria-hidden="true"></i>
                <h2 class="bn-state__title">Scan a ticket to see it here</h2>
                <p class="bn-state__body">
                    The result appears in this space: whether the passenger may board, their seat
                    and trip, and any earlier attempts on the same ticket.
                </p>
            </div>
        </asp:Panel>

        <asp:Panel ID="pnlResult" runat="server" Visible="false" CssClass="bn-card">
            <h2 class="bn-subheading">
                <asp:Literal ID="litOutcomeTitle" runat="server" />
            </h2>

            <div class="e-ticket-grid">
                <div><span>Ticket</span><strong><asp:Literal ID="litTicketNumber" runat="server" /></strong></div>
                <div><span>Traveller</span><strong><asp:Literal ID="litTraveller" runat="server" /></strong></div>
                <div><span>Route</span><strong><asp:Literal ID="litRoute" runat="server" /></strong></div>
                <div><span>Travel date</span><strong><asp:Literal ID="litDate" runat="server" /></strong></div>
                <div><span>Seat</span><strong><asp:Literal ID="litSeat" runat="server" /></strong></div>
                <div><span>Status</span><strong><asp:Literal ID="litStatus" runat="server" /></strong></div>
            </div>

            <h3 class="bn-subheading bn-mt-3">Recent attempts on this ticket</h3>
            <div class="ops-grid-wrap ops-grid-wrap--compact">
                <asp:GridView ID="gvHistory" runat="server" AutoGenerateColumns="false"
                              CssClass="ops-grid" GridLines="None" UseAccessibleHeader="true"
                              OnRowDataBound="gvHistory_RowDataBound"
                              EmptyDataText="No earlier attempts recorded for this ticket.">
                    <Columns>
                        <asp:BoundField DataField="OccurredAt" HeaderText="When" DataFormatString="{0:dd MMM yyyy HH:mm}" />
                        <asp:BoundField DataField="Method" HeaderText="Presented as" />
                        <asp:TemplateField HeaderText="Outcome">
                            <ItemTemplate>
                                <span class='<%# "status-badge " + OutcomeCss(Eval("Outcome")) %>'>
                                    <%# OutcomeText(Eval("Outcome")) %>
                                </span>
                            </ItemTemplate>
                        </asp:TemplateField>
                        <asp:BoundField DataField="ValidatedByRole" HeaderText="By" />
                    </Columns>
                </asp:GridView>
            </div>
        </asp:Panel>

        </div>

    </div>

</asp:Content>
