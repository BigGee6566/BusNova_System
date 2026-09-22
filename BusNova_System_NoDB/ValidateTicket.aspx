<%@ Page Title="Validate Ticket" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="ValidateTicket.aspx.cs" Inherits="BusNova_System.ValidateTicket" %>
<asp:Content ID="Content1" ContentPlaceHolderID="head" runat="server">
    <link href="Styles/Ops.css?v=6" rel="stylesheet" />
</asp:Content>
<asp:Content ID="Content2" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">

    <div class="ops-wrap ops-wrap--narrow">

        <div class="bn-page-header">
            <h1 class="bn-page-header__title">Validate a ticket</h1>
            <p class="bn-page-header__subtitle">
                Scan the passenger's QR code, or type the ticket number if the code will not scan.
            </p>
        </div>

        <div class="bn-card">

            <div class="bn-field">
                <label for="<%= ddlTrip.ClientID %>">Trip being boarded</label>
                <asp:DropDownList ID="ddlTrip" runat="server" CssClass="form-control" />
                <p class="bn-note">
                    Leave as &ldquo;Any trip&rdquo; to accept a valid ticket for any departure. Choosing a
                    trip means a ticket for a different departure is refused with its own message,
                    rather than being boarded onto the wrong bus.
                </p>
            </div>

            <div class="bn-field">
                <label for="<%= txtToken.ClientID %>">Scanned code</label>
                <asp:TextBox ID="txtToken" runat="server" CssClass="form-control" TextMode="MultiLine"
                             Rows="3" placeholder="Put the cursor here and scan" autocomplete="off" />
                <p class="bn-note">
                    A hardware scanner types the code into this box and presses Enter.
                </p>
                <asp:Button ID="btnValidateToken" runat="server" Text="Validate scanned code"
                            CssClass="bn-btn bn-btn--primary bn-w-full" OnClick="btnValidateToken_Click" />
            </div>

            <div class="bn-callout">
                <strong>Code will not scan?</strong>
                Use the ticket number printed on the ticket.
            </div>

            <div class="bn-field">
                <label for="<%= txtTicketNumber.ClientID %>">Ticket number</label>
                <asp:TextBox ID="txtTicketNumber" runat="server" CssClass="form-control"
                             placeholder="BN-001009" autocomplete="off" MaxLength="20" />
                <asp:Button ID="btnValidateNumber" runat="server" Text="Validate ticket number"
                            CssClass="bn-btn bn-btn--secondary bn-w-full" OnClick="btnValidateNumber_Click" />
            </div>

            <asp:Label ID="lblMessage" runat="server" />

        </div>

        <asp:Panel ID="pnlResult" runat="server" Visible="false" CssClass="bn-card bn-mt-3">
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
        </asp:Panel>

    </div>

</asp:Content>
