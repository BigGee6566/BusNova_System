<%@ Page Title="My Trip" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="DriverWorkspace.aspx.cs" Inherits="BusNova_System.DriverWorkspace" %>
<asp:Content ID="Content1" ContentPlaceHolderID="head" runat="server">
    <link href="Styles/Ops.css?v=6" rel="stylesheet" />
    <link href="Styles/DriverWorkspace.css?v=1" rel="stylesheet" />
</asp:Content>
<asp:Content ID="Content2" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">

    <%-- 360px first. Everything on this page is a single column that grows
         wider on a desk, rather than a desktop layout squeezed down. A
         driver reads this on a phone, at a bus, often in the dark. --%>
    <div class="ops-wrap ops-wrap--narrow dw">

        <div class="bn-page-header">
            <h1 class="bn-page-header__title">My trip</h1>
            <p class="bn-page-header__subtitle">
                <asp:Literal ID="litToday" runat="server" />
            </p>
        </div>

        <asp:Label ID="lblMessage" runat="server" />

        <%-- ---------------- no trip ---------------- --%>
        <asp:Panel ID="pnlNoTrip" runat="server" Visible="false" CssClass="bn-card dw-empty">
            <h2 class="bn-subheading">Nothing scheduled</h2>
            <p class="bn-note">You have no trips in the next two days. Enjoy the quiet.</p>
        </asp:Panel>

        <%-- ---------------- trip picker ---------------- --%>
        <asp:Panel ID="pnlPicker" runat="server" Visible="false" CssClass="bn-card">
            <label for="<%= ddlTrip.ClientID %>">Trip</label>
            <asp:DropDownList ID="ddlTrip" runat="server" CssClass="form-control dw-select"
                              AutoPostBack="true" OnSelectedIndexChanged="ddlTrip_SelectedIndexChanged" />
        </asp:Panel>

        <%-- ---------------- the trip ---------------- --%>
        <asp:Panel ID="pnlTrip" runat="server" Visible="false">

            <div class="bn-card dw-trip">
                <div class="dw-route"><asp:Literal ID="litRoute" runat="server" /></div>
                <div class="dw-when"><asp:Literal ID="litWhen" runat="server" /></div>
                <div class="dw-bus">
                    <asp:Literal ID="litBus" runat="server" />
                    <asp:Label ID="lblBusStatus" runat="server" CssClass="status-badge" />
                </div>

                <div class="dw-counts">
                    <div class="dw-count">
                        <span class="dw-count__value"><asp:Literal ID="litBooked" runat="server" /></span>
                        <span class="dw-count__label">Booked</span>
                    </div>
                    <div class="dw-count">
                        <span class="dw-count__value"><asp:Literal ID="litBoarded" runat="server" /></span>
                        <span class="dw-count__label">Boarded</span>
                    </div>
                    <div class="dw-count">
                        <span class="dw-count__value"><asp:Literal ID="litCapacity" runat="server" /></span>
                        <span class="dw-count__label">Seats</span>
                    </div>
                </div>

                <div class="dw-actions">
                    <asp:Button ID="btnStart" runat="server" Text="Start trip"
                                CssClass="bn-btn bn-btn--primary dw-btn" OnClick="btnStart_Click" />
                    <asp:Button ID="btnComplete" runat="server" Text="Complete trip"
                                CssClass="bn-btn bn-btn--primary dw-btn" OnClick="btnComplete_Click" Visible="false" />
                </div>
            </div>

            <%-- ---------------- inspection ---------------- --%>
            <div class="bn-card">
                <h2 class="bn-subheading">
                    <asp:Literal ID="litInspectionTitle" runat="server" Text="Pre-trip inspection" />
                </h2>
                <p class="bn-note">
                    Tick what you have checked. Anything left unticked is recorded as not checked,
                    not as a fault.
                </p>

                <asp:CheckBoxList ID="cblInspection" runat="server" CssClass="dw-checks"
                                  RepeatLayout="UnorderedList" RepeatDirection="Vertical" />

                <div class="bn-field">
                    <label for="<%= txtInspectionNotes.ClientID %>">Anything to note?</label>
                    <asp:TextBox ID="txtInspectionNotes" runat="server" CssClass="form-control"
                                 TextMode="MultiLine" Rows="2" MaxLength="400"
                                 placeholder="Optional - e.g. nearside mirror loose" />
                </div>

                <asp:Button ID="btnSaveInspection" runat="server" Text="Save inspection"
                            CssClass="bn-btn bn-btn--secondary dw-btn" OnClick="btnSaveInspection_Click" />
            </div>

            <%-- ---------------- manifest ---------------- --%>
            <div class="bn-card">
                <h2 class="bn-subheading">Passenger manifest</h2>
                <div class="ops-grid-wrap">
                    <asp:GridView ID="gvManifest" runat="server" AutoGenerateColumns="false"
                                  CssClass="ops-grid dw-manifest" GridLines="None" UseAccessibleHeader="true"
                                  OnRowDataBound="gvManifest_RowDataBound"
                                  EmptyDataText="No passengers booked on this trip yet.">
                        <Columns>
                            <asp:BoundField DataField="SeatNumber" HeaderText="Seat" />
                            <asp:BoundField DataField="PassengerName" HeaderText="Passenger" />
                            <asp:BoundField DataField="TicketNumber" HeaderText="Ticket" />
                            <asp:TemplateField HeaderText="Status">
                                <ItemTemplate>
                                    <span class='<%# "status-badge " + TicketCss(Eval("TicketStatus")) %>'>
                                        <%# Server.HtmlEncode(Convert.ToString(Eval("TicketStatus"))) %>
                                    </span>
                                </ItemTemplate>
                            </asp:TemplateField>
                        </Columns>
                    </asp:GridView>
                </div>
                <p class="bn-note">
                    Boarding is recorded on the Validate Ticket screen, which is what moves a
                    ticket to Used.
                </p>
            </div>

            <%-- ---------------- delay ---------------- --%>
            <div class="bn-card">
                <h2 class="bn-subheading">Report a delay</h2>
                <div class="bn-field">
                    <label for="<%= ddlDelay.ClientID %>">How late?</label>
                    <asp:DropDownList ID="ddlDelay" runat="server" CssClass="form-control dw-select">
                        <asp:ListItem Text="15 minutes" Value="15" />
                        <asp:ListItem Text="30 minutes" Value="30" />
                        <asp:ListItem Text="1 hour" Value="60" />
                        <asp:ListItem Text="2 hours or more" Value="120" />
                    </asp:DropDownList>
                </div>
                <div class="bn-field">
                    <label for="<%= txtDelayReason.ClientID %>">Why?</label>
                    <asp:TextBox ID="txtDelayReason" runat="server" CssClass="form-control"
                                 TextMode="MultiLine" Rows="2" MaxLength="300"
                                 placeholder="e.g. roadworks on the N2" />
                </div>
                <asp:Button ID="btnDelay" runat="server" Text="Report delay"
                            CssClass="bn-btn bn-btn--secondary dw-btn" OnClick="btnDelay_Click" />
            </div>

            <%-- ---------------- emergency ---------------- --%>
            <div class="bn-card dw-emergency">
                <h2 class="bn-subheading">Emergency</h2>
                <p class="bn-note">
                    Raises a critical fault against this bus immediately and takes it off the road.
                    Use it when the bus is not safe to continue.
                </p>
                <%-- The attributes below must be data-bn-confirm and
                     data-bn-confirm-ok. Scripts/busnova-ui.js finds its
                     triggers with closest('[data-bn-confirm]'), so the
                     -body / -accept names this used to carry meant the
                     button had no confirmation at all and a single press
                     grounded a bus immediately. --%>
                <asp:Button ID="btnEmergency" runat="server" Text="Raise emergency"
                            CssClass="bn-btn bn-btn--danger dw-btn"
                            data-bn-confirm-title="Raise an emergency?"
                            data-bn-confirm="This takes the bus off the road straight away and alerts the depot. Only do this if the bus is not safe to continue."
                            data-bn-confirm-ok="Raise emergency"
                            OnClick="btnEmergency_Click" />
            </div>

        </asp:Panel>

    </div>

</asp:Content>
