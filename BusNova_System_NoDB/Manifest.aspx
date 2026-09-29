<%@ Page Title="Passenger Manifest" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Manifest.aspx.cs" Inherits="BusNova_System.Manifest" %>
<%@ Import Namespace="BusNova_System.Helpers" %>
<asp:Content ID="Content1" ContentPlaceHolderID="head" runat="server">
    <link href="Styles/Ops.css?v=9" rel="stylesheet" />
    <link href="Styles/Manifest.css?v=4" rel="stylesheet" />
</asp:Content>
<asp:Content ID="Content2" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">

    <div class="ops-wrap ops-wrap--wide">

        <%-- Everything in this block is hidden when printing. What comes
             out of the printer is the manifest, not the application. --%>
        <div class="mf-screen-only">
            <%-- The page title is rendered once, by Site.Master's top bar. --%>
            <p class="bn-page-intro">
                The list to carry on the bus. Print it, or export it if it has to go into
                another system.
            </p>

            <div class="bn-card">
                <div class="rpt-filters">
                    <div class="bn-field">
                        <label for="<%= ddlTrip.ClientID %>">Trip</label>
                        <asp:DropDownList ID="ddlTrip" runat="server" CssClass="form-control"
                                          AutoPostBack="true" OnSelectedIndexChanged="ddlTrip_SelectedIndexChanged" />
                    </div>
                    <div class="bn-field bn-align-end">
                        <asp:Button ID="btnPrint" runat="server" Text="Print" CssClass="bn-btn bn-btn--primary"
                                    OnClientClick="window.print(); return false;" />
                        <asp:Button ID="btnCsv" runat="server" Text="Export CSV"
                                    CssClass="bn-btn bn-btn--secondary" OnClick="btnCsv_Click" />
                    </div>
                </div>
                <asp:Label ID="lblMessage" runat="server" />
            </div>
        </div>

        <%-- The manifest itself. This is what prints. --%>
        <asp:Panel ID="pnlManifest" runat="server" Visible="false" CssClass="mf-sheet">

            <div class="mf-head">
                <div class="mf-title">BusNova &mdash; Passenger Manifest</div>
                <div class="mf-meta">
                    <div><span>Route</span><strong><asp:Literal ID="litRoute" runat="server" /></strong></div>
                    <div><span>Date</span><strong><asp:Literal ID="litDate" runat="server" /></strong></div>
                    <div><span>Departs</span><strong><asp:Literal ID="litDeparts" runat="server" /></strong></div>
                    <div><span>Bus</span><strong><asp:Literal ID="litBus" runat="server" /></strong></div>
                    <div><span>Driver</span><strong><asp:Literal ID="litDriver" runat="server" /></strong></div>
                    <div><span>Booked</span><strong><asp:Literal ID="litBooked" runat="server" /></strong></div>
                </div>
            </div>

            <div class="ops-grid-wrap">
                <%-- OnRowDataBound is what puts the header into a real <thead>
                     and copies each HeaderText onto its cell as data-label.
                     Without it this grid collapsed at 360px into a column of
                     unlabelled values under a header row that stayed visible
                     and was clipped mid-word - AUD-066. Every other .ops-grid
                     in the application already routed through this. --%>
                <asp:GridView ID="gvManifest" runat="server" AutoGenerateColumns="false"
                              CssClass="ops-grid mf-grid" GridLines="None" UseAccessibleHeader="true"
                              OnRowDataBound="gvManifest_RowDataBound"
                              EmptyDataText="No passengers are booked on this trip.">
                    <Columns>
                        <asp:BoundField DataField="SeatNumber" HeaderText="Seat" ItemStyle-CssClass="num" HeaderStyle-CssClass="num" />
                        <asp:BoundField DataField="PassengerName" HeaderText="Passenger" />
                        <asp:TemplateField HeaderText="Ticket">
                            <ItemTemplate>
                                <span class="code-badge"><%# Server.HtmlEncode(Convert.ToString(Eval("TicketNumber"))) %></span>
                            </ItemTemplate>
                        </asp:TemplateField>
                        <asp:TemplateField HeaderText="Status">
                            <ItemTemplate>
                                <span class='<%# "status-badge " + StatusTone.ForTicket(Eval("TicketStatus")) %>'>
                                    <%# Server.HtmlEncode(BusNova_System.Helpers.StatusTone.Label(Eval("TicketStatus"))) %>
                                </span>
                            </ItemTemplate>
                        </asp:TemplateField>
                        <asp:TemplateField HeaderText="Boarded">
                            <ItemTemplate><span class="mf-tick" aria-hidden="true"></span></ItemTemplate>
                        </asp:TemplateField>
                    </Columns>
                </asp:GridView>
            </div>

            <div class="mf-foot">
                <div class="mf-sign">
                    <span class="mf-sign__line"></span>
                    <span class="mf-sign__label">Driver signature</span>
                </div>
                <div class="mf-sign">
                    <span class="mf-sign__line"></span>
                    <span class="mf-sign__label">Date</span>
                </div>
                <div class="mf-printed">
                    Printed <asp:Literal ID="litPrinted" runat="server" />
                    by <asp:Literal ID="litPrintedBy" runat="server" />
                </div>
            </div>

        </asp:Panel>

    </div>

</asp:Content>
