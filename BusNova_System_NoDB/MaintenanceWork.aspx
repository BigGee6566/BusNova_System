<%@ Page Title="Maintenance" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="MaintenanceWork.aspx.cs" Inherits="BusNova_System.MaintenanceWork" %>
<asp:Content ID="Content1" ContentPlaceHolderID="head" runat="server">
    <link href="Styles/style.css?v=8" rel="stylesheet" />
    <link href="Styles/Ops.css?v=9" rel="stylesheet" />
</asp:Content>
<asp:Content ID="Content2" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">

    <div class="ops-page">
    <div class="ops-wrap ops-wrap--wide">

        <div class="ops-box">

            <h2 class="ops-title">Maintenance</h2>
            <div class="ops-subtitle">
                Faults reported by drivers and fleet managers, ordered by severity.
                Start work to mark a fault as being looked at, then log the completed
                service to close it out.
            </div>

            <asp:Label ID="lblMessage" runat="server" CssClass="ops-message" />

            <div class="ops-grid-wrap">
                <asp:GridView
                    ID="gvOpen"
                    runat="server"
                    CssClass="ops-grid"
                    AutoGenerateColumns="false"
                    GridLines="None"
                    DataKeyNames="MaintenanceLogId"
                    EmptyDataText="No open maintenance issues. Everything is up to date."
                    OnRowCommand="gvOpen_RowCommand"
                    OnRowDataBound="gvOpen_RowDataBound">
                    <Columns>
                        <asp:BoundField DataField="BusRegistration" HeaderText="Bus" />
                        <asp:BoundField DataField="ReportedByName" HeaderText="Reported by" />
                        <asp:BoundField DataField="DateReported" HeaderText="Date" DataFormatString="{0:dd MMM yyyy}" />
                        <asp:BoundField DataField="IssueDescription" HeaderText="Problem" />
                        <asp:BoundField DataField="VehicleCondition" HeaderText="Condition" />
                        <asp:TemplateField HeaderText="Severity">
                            <ItemTemplate>
                                <span class='<%# "status-badge " + SeverityCss(Eval("Severity")) %>'>
                                    <%# Server.HtmlEncode(Convert.ToString(Eval("Severity"))) %>
                                </span>
                            </ItemTemplate>
                        </asp:TemplateField>
                        <asp:TemplateField HeaderText="Status">
                            <ItemTemplate>
                                <span class='<%# "status-badge " + StatusCss(Eval("Status")) %>'>
                                    <%# Server.HtmlEncode(BusNova_System.Helpers.StatusTone.Label(Eval("Status"))) %>
                                </span>
                            </ItemTemplate>
                        </asp:TemplateField>
                        <asp:TemplateField HeaderText="Action">
                            <ItemTemplate>
                                <asp:LinkButton
                                    ID="btnStartWork"
                                    runat="server"
                                    CommandName="StartWork"
                                    CommandArgument='<%# Eval("MaintenanceLogId") %>'
                                    CssClass="btn-ops-secondary"
                                    CausesValidation="false"
                                    Visible='<%# Convert.ToString(Eval("Status")) == "Reported" %>'
                                    Text="Start work" />
                                <asp:LinkButton
                                    ID="btnLogService"
                                    runat="server"
                                    CommandName="LogService"
                                    CommandArgument='<%# Eval("MaintenanceLogId") %>'
                                    CssClass="btn-ops-secondary"
                                    CausesValidation="false"
                                    Text="Log service" />
                            </ItemTemplate>
                        </asp:TemplateField>
                    </Columns>
                </asp:GridView>
            </div>

            <%-- Shown once a fault has been picked, so the mechanic can record what was done. --%>
            <asp:Panel CssClass="bn-callout bn-mt-6" ID="pnlService" runat="server" Visible="false"
                      >

                <h3 class="bn-subheading bn-subheading--sm">Log completed service</h3>
                <div class="ops-subtitle bn-mb-4">
                    <asp:Literal ID="litServiceFault" runat="server" />
                </div>

                <div class="mb-3">
                    <label for="<%= txtWorkPerformed.ClientID %>">Work performed<span class="required-mark">*</span></label>
                    <asp:TextBox ID="txtWorkPerformed" runat="server" CssClass="form-control"
                                 TextMode="MultiLine" Rows="3" MaxLength="500" />
                    <asp:RequiredFieldValidator
                        runat="server"
                        ControlToValidate="txtWorkPerformed"
                        ErrorMessage="Please describe the work that was carried out."
                        CssClass="field-error"
                        Display="Dynamic"
                        ValidationGroup="LogService" />
                </div>

                <div class="mb-3">
                    <label for="<%= txtPartsUsed.ClientID %>">Parts used</label>
                    <asp:TextBox ID="txtPartsUsed" runat="server" CssClass="form-control"
                                 placeholder="Optional" MaxLength="255" />
                </div>

                <div class="bn-cols-4">
                    <div class="bn-field">
                        <label for="<%= txtServiceCost.ClientID %>">Cost</label>
                        <asp:TextBox ID="txtServiceCost" runat="server" CssClass="form-control" placeholder="Optional" />
                        <asp:RegularExpressionValidator
                            runat="server"
                            ControlToValidate="txtServiceCost"
                            ErrorMessage="Enter a number, e.g. 850.00."
                            ValidationExpression="^[0-9]+(\.[0-9]{1,2})?$"
                            CssClass="field-error"
                            Display="Dynamic"
                            ValidationGroup="LogService" />
                    </div>
                    <div class="bn-field">
                        <label for="<%= txtServiceOdometer.ClientID %>">Odometer at service</label>
                        <asp:TextBox ID="txtServiceOdometer" runat="server" CssClass="form-control" placeholder="Optional" />
                        <asp:RegularExpressionValidator
                            runat="server"
                            ControlToValidate="txtServiceOdometer"
                            ErrorMessage="Enter a whole number of kilometres."
                            ValidationExpression="^[0-9]+$"
                            CssClass="field-error"
                            Display="Dynamic"
                            ValidationGroup="LogService" />
                    </div>
                    <div class="bn-field">
                        <label for="<%= txtServiceDate.ClientID %>">Service date<span class="required-mark">*</span></label>
                        <asp:TextBox ID="txtServiceDate" runat="server" TextMode="Date" CssClass="form-control" />
                        <asp:RequiredFieldValidator
                            runat="server"
                            ControlToValidate="txtServiceDate"
                            ErrorMessage="Service date is required."
                            CssClass="field-error"
                            Display="Dynamic"
                            ValidationGroup="LogService" />
                    </div>
                    <div class="bn-field">
                        <label for="<%= txtNextServiceDue.ClientID %>">Next service due<span class="required-mark">*</span></label>
                        <asp:TextBox ID="txtNextServiceDue" runat="server" TextMode="Date" CssClass="form-control" />
                        <asp:RequiredFieldValidator
                            runat="server"
                            ControlToValidate="txtNextServiceDue"
                            ErrorMessage="Next service due date is required."
                            CssClass="field-error"
                            Display="Dynamic"
                            ValidationGroup="LogService" />
                    </div>
                </div>

                <div class="mb-3">
                    <label for="<%= txtServiceCondition.ClientID %>">Condition of the bus after the work<span class="required-mark">*</span></label>
                    <asp:TextBox ID="txtServiceCondition" runat="server" CssClass="form-control" MaxLength="50" />
                    <asp:RequiredFieldValidator
                        runat="server"
                        ControlToValidate="txtServiceCondition"
                        ErrorMessage="Please record the condition after the work."
                        CssClass="field-error"
                        Display="Dynamic"
                        ValidationGroup="LogService" />
                </div>

                <div class="mb-3">
                    <asp:CheckBox ID="chkReturnToService" runat="server" Checked="true"
                                  Text=" Return this bus to service now" />
                </div>

                <div class="ops-actions">
                    <asp:Button ID="btnConfirmService" runat="server"
                                Text="Save and close fault"
                                CssClass="btn-ops-primary"
                                ValidationGroup="LogService"
                                OnClick="btnConfirmService_Click" />
                    <asp:Button ID="btnCancelService" runat="server"
                                Text="Cancel"
                                CssClass="btn-ops-secondary"
                                CausesValidation="false"
                                OnClick="btnCancelService_Click" />
                </div>

                <asp:HiddenField ID="hfServiceMaintenanceId" runat="server" />

            </asp:Panel>

        </div>

        <div class="ops-box bn-mt-6">
            <h2 class="ops-title bn-subheading">Recently completed</h2>
            <div class="ops-grid-wrap">
                <asp:GridView ID="gvCompleted" runat="server" CssClass="ops-grid"
                              AutoGenerateColumns="false" GridLines="None"
                              OnRowDataBound="gvCompleted_RowDataBound"
                              EmptyDataText="No maintenance has been completed yet.">
                    <Columns>
                        <asp:BoundField DataField="BusRegistration" HeaderText="Bus" />
                        <asp:BoundField DataField="ServiceDate" HeaderText="Serviced" DataFormatString="{0:dd MMM yyyy}" />
                        <asp:BoundField DataField="ServicedByName" HeaderText="Serviced by" />
                        <asp:BoundField DataField="WorkPerformed" HeaderText="Work performed" />
                        <asp:TemplateField HeaderText="Cost">
                            <ItemTemplate><%# FormatMoney(Eval("Cost")) %></ItemTemplate>
                        </asp:TemplateField>
                        <asp:BoundField DataField="NextServiceDueDate" HeaderText="Next due" DataFormatString="{0:dd MMM yyyy}" />
                    </Columns>
                </asp:GridView>
            </div>
        </div>

    </div>
    </div>

</asp:Content>
