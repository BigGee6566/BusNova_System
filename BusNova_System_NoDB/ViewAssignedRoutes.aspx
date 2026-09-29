<%@ Page Title="My Schedule" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="ViewAssignedRoutes.aspx.cs" Inherits="BusNova_System.ViewAssignedRoutes" %>
<%@ Import Namespace="BusNova_System.Helpers" %>
<asp:Content ID="Content1" ContentPlaceHolderID="head" runat="server">
    <link href="Styles/style.css?v=8" rel="stylesheet" />
    <link href="Styles/Ops.css?v=9" rel="stylesheet" />
</asp:Content>
<asp:Content ID="Content2" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">

    <div class="ops-page">
    <div class="ops-wrap ops-wrap--default">

        <div class="ops-box">

            <h2 class="ops-title">My Schedule</h2>
            <div class="ops-subtitle">
                Your assigned routes and trip schedule. Start a trip when you set off,
                and complete it when you arrive.
            </div>

            <asp:Label ID="lblMessage" runat="server" CssClass="ops-message" />

            <div class="ops-grid-wrap">
                <asp:GridView
                    ID="gvAssignedRoutes"
                    runat="server"
                    CssClass="ops-grid"
                    AutoGenerateColumns="false"
                    GridLines="None"
                    DataKeyNames="AssignmentId"
                    EmptyDataText="No routes assigned yet."
                    OnRowCommand="gvAssignedRoutes_RowCommand"
                    OnRowDataBound="gvAssignedRoutes_RowDataBound">
                    <Columns>
                        <asp:BoundField DataField="AssignmentId" HeaderText="Trip" />
                        <%-- Route name with its endpoints underneath, and one
                             Time column: ten columns pushed the Action
                             column, which holds Start and Complete trip,
                             off the right edge of the card at 1280px. --%>
                        <asp:TemplateField HeaderText="Route" ItemStyle-CssClass="wrap">
                            <ItemTemplate>
                                <span class="cell-stack">
                                    <span><%# Server.HtmlEncode(Convert.ToString(Eval("RouteName"))) %></span>
                                    <span class="cell-meta">
                                        <%# Server.HtmlEncode(Convert.ToString(Eval("Origin"))) %>
                                        &rarr;
                                        <%# Server.HtmlEncode(Convert.ToString(Eval("Destination"))) %>
                                    </span>
                                </span>
                            </ItemTemplate>
                        </asp:TemplateField>
                        <asp:BoundField DataField="BusRegistration" HeaderText="Bus" />
                        <asp:BoundField DataField="AssignmentDate" HeaderText="Date" DataFormatString="{0:dd MMM yyyy}" />
                        <asp:TemplateField HeaderText="Time">
                            <ItemTemplate><%# FormatTime(Eval("DepartureTime")) %>&ndash;<%# FormatTime(Eval("ArrivalTime")) %></ItemTemplate>
                        </asp:TemplateField>
                        <asp:BoundField DataField="PassengerCount" HeaderText="Passengers" ItemStyle-CssClass="num" HeaderStyle-CssClass="num" />
                        <asp:TemplateField HeaderText="Status">
                            <ItemTemplate>
                                <span class='<%# "status-badge " + StatusTone.ForTrip(Eval("TripStatus")) %>'>
                                    <%# Server.HtmlEncode(StatusTone.Label(Eval("TripStatus"))) %>
                                </span>
                            </ItemTemplate>
                        </asp:TemplateField>

                        <%-- UC12 Update trip status. Only the button that makes
                             sense for the current status is rendered. --%>
                        <asp:TemplateField HeaderText="Action">
                            <ItemTemplate>

                                <asp:LinkButton
                                    ID="btnStart"
                                    runat="server"
                                    CommandName="StartTrip"
                                    CommandArgument='<%# Eval("AssignmentId") %>'
                                    CssClass="btn-ops-primary"
                                    CausesValidation="false"
                                    Visible='<%# Convert.ToString(Eval("TripStatus")) == "Scheduled" %>'
                                    Text="Start trip" />

                                <asp:LinkButton
                                    ID="btnComplete"
                                    runat="server"
                                    CommandName="CompleteTrip"
                                    CommandArgument='<%# Eval("AssignmentId") %>'
                                    CssClass="btn-ops-primary"
                                    CausesValidation="false"
                                    Visible='<%# Convert.ToString(Eval("TripStatus")) == "InProgress" %>'
                                    Text="Complete trip" />

                                <asp:Label
                                    ID="lblNoAction"
                                    runat="server"
                                    Text="&mdash;"
                                    Visible='<%# Convert.ToString(Eval("TripStatus")) == "Completed" || Convert.ToString(Eval("TripStatus")) == "Cancelled" %>' />

                            </ItemTemplate>
                        </asp:TemplateField>
                    </Columns>
                </asp:GridView>
            </div>

            <%-- Shown only while a trip is being completed, so that the driver
                 can record how many passengers actually travelled (R018). --%>
            <asp:Panel CssClass="bn-callout bn-mt-6" ID="pnlComplete" runat="server" Visible="false"
                      >

                <h3 class="bn-subheading bn-subheading--sm">Complete trip</h3>
                <div class="ops-subtitle bn-mb-4">
                    <asp:Literal ID="litCompleteTrip" runat="server" />
                </div>

                <label for="<%= txtCompletePassengers.ClientID %>">Passenger count</label>
                <asp:TextBox ID="txtCompletePassengers" runat="server"
                             CssClass="form-control" MaxLength="3"
                             placeholder="How many passengers travelled?" />
                <asp:RegularExpressionValidator
                    runat="server"
                    ControlToValidate="txtCompletePassengers"
                    ErrorMessage="Passenger count must be a whole number."
                    ValidationExpression="^[0-9]+$"
                    CssClass="field-error"
                    Display="Dynamic"
                    ValidationGroup="CompleteTrip" />

                <div class="ops-actions">
                    <asp:Button ID="btnConfirmComplete" runat="server"
                                Text="Confirm completion"
                                CssClass="btn-ops-primary"
                                ValidationGroup="CompleteTrip"
                                OnClick="btnConfirmComplete_Click" />
                    <asp:Button ID="btnCancelComplete" runat="server"
                                Text="Cancel"
                                CssClass="btn-ops-secondary"
                                CausesValidation="false"
                                OnClick="btnCancelComplete_Click" />
                </div>

                <asp:HiddenField ID="hfCompleteAssignmentId" runat="server" />

            </asp:Panel>

        </div>
    </div>
    </div>

</asp:Content>
