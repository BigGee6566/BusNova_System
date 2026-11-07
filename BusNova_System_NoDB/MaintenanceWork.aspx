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
            <asp:Panel ID="pnlStaffIntro" runat="server" CssClass="ops-subtitle">
                Faults reported by drivers and fleet managers, ordered by severity.
                Start work to mark a fault as being looked at, then log the completed
                service to close it out.
            </asp:Panel>
            <asp:Panel ID="pnlManagerIntro" runat="server" CssClass="ops-subtitle" Visible="false">
                Faults and services logged by managers and maintenance staff, ordered by severity.
                Use Manage Maintenance to schedule a service.
            </asp:Panel>

            <asp:Label ID="lblMessage" runat="server" CssClass="ops-message" />

            <div class="bn-cols-4 bn-mb-4">
                <div class="bn-field">
                    <label for="<%= ddlFilterBus.ClientID %>">Bus</label>
                    <asp:DropDownList ID="ddlFilterBus" runat="server" CssClass="form-select" />
                </div>
                <div class="bn-field">
                    <label for="<%= txtFilterFromDate.ClientID %>">From date</label>
                    <asp:TextBox ID="txtFilterFromDate" runat="server" TextMode="Date" CssClass="form-control" />
                </div>
                <div class="bn-field">
                    <label for="<%= txtFilterToDate.ClientID %>">To date</label>
                    <asp:TextBox ID="txtFilterToDate" runat="server" TextMode="Date" CssClass="form-control" />
                </div>
                <div class="bn-field">
                    <label for="<%= ddlFilterStatus.ClientID %>">Status</label>
                    <asp:DropDownList ID="ddlFilterStatus" runat="server" CssClass="form-select">
                        <asp:ListItem Text="All statuses" Value="All" />
                        <asp:ListItem Text="Reported" Value="Reported" />
                        <asp:ListItem Text="In Progress" Value="InProgress" />
                        <asp:ListItem Text="Completed" Value="Completed" />
                        <asp:ListItem Text="Cancelled" Value="Cancelled" />
                        <asp:ListItem Text="Overdue" Value="Overdue" />
                    </asp:DropDownList>
                </div>
            </div>

            <div class="ops-actions bn-mb-6">
                <asp:Button ID="btnFilter" runat="server" Text="Filter" CssClass="btn-ops-primary" CausesValidation="false" OnClick="btnFilter_Click" />
                <asp:Button ID="btnResetFilter" runat="server" Text="Reset" CssClass="btn-ops-secondary" CausesValidation="false" OnClick="btnResetFilter_Click" />
            </div>

            <div class="ops-grid-wrap">
                <asp:GridView
                    ID="gvOpen"
                    runat="server"
                    CssClass="ops-grid"
                    AutoGenerateColumns="false"
                    GridLines="None"
                    DataKeyNames="MaintenanceLogId"
                    EmptyDataText="No maintenance issues match the selected criteria."
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
                                    ID="btnViewDetails"
                                    runat="server"
                                    CommandName="ViewDetails"
                                    CommandArgument='<%# Eval("MaintenanceLogId") %>'
                                    CssClass="btn-ops-secondary"
                                    CausesValidation="false"
                                    Text="View" />
                                <asp:LinkButton
                                    ID="btnStartWork"
                                    runat="server"
                                    CommandName="StartWork"
                                    CommandArgument='<%# Eval("MaintenanceLogId") %>'
                                    CssClass="btn-ops-secondary"
                                    CausesValidation="false"
                                    Visible='<%# CanEditRecords && Convert.ToString(Eval("Status")) == "Reported" %>'
                                    Text="Start work" />
                                <asp:LinkButton
                                    ID="btnLogService"
                                    runat="server"
                                    CommandName="LogService"
                                    CommandArgument='<%# Eval("MaintenanceLogId") %>'
                                    CssClass="btn-ops-secondary"
                                    CausesValidation="false"
                                    Visible='<%# CanEditRecords && Convert.ToString(Eval("Status")) != "Completed" && Convert.ToString(Eval("Status")) != "Cancelled" %>'
                                    Text="Log service" />
                                <asp:LinkButton
                                    ID="btnEditOverdue"
                                    runat="server"
                                    CommandName="EditOverdue"
                                    CommandArgument='<%# Eval("MaintenanceLogId") %>'
                                    CssClass="btn-ops-secondary"
                                    CausesValidation="false"
                                    Visible='<%# CanEditRecords && IsOverdueAndNotCancelled(Eval("NextServiceDueDate"), Eval("Status")) %>'
                                    Text="Edit" />
                            </ItemTemplate>
                        </asp:TemplateField>
                    </Columns>
                </asp:GridView>
            </div>

            <%-- Details panel shown when ViewDetails is clicked --%>
            <asp:Panel CssClass="bn-callout bn-callout--info bn-mt-6" ID="pnlDetails" runat="server" Visible="false">
                <h3 class="bn-subheading bn-subheading--sm">Maintenance Record Details</h3>
                <div class="bn-cols-3 bn-mb-3">
                    <div><strong>Bus:</strong> <asp:Literal ID="litDetailBus" runat="server" /></div>
                    <div><strong>Reported By:</strong> <asp:Literal ID="litDetailReportedBy" runat="server" /></div>
                    <div><strong>Date Reported:</strong> <asp:Literal ID="litDetailDateReported" runat="server" /></div>
                    <div><strong>Severity:</strong> <asp:Literal ID="litDetailSeverity" runat="server" /></div>
                    <div><strong>Status:</strong> <asp:Literal ID="litDetailStatus" runat="server" /></div>
                    <div><strong>Condition:</strong> <asp:Literal ID="litDetailCondition" runat="server" /></div>
                </div>
                <div class="mb-3">
                    <strong>Issue Description:</strong><br />
                    <asp:Literal ID="litDetailIssue" runat="server" />
                </div>
                <div class="bn-cols-3 bn-mb-3">
                    <div><strong>Serviced By:</strong> <asp:Literal ID="litDetailServicedBy" runat="server" /></div>
                    <div><strong>Service Date:</strong> <asp:Literal ID="litDetailServiceDate" runat="server" /></div>
                    <div><strong>Next Service Due:</strong> <asp:Literal ID="litDetailNextDue" runat="server" /></div>
                    <div><strong>Odometer at Service:</strong> <asp:Literal ID="litDetailOdometer" runat="server" /></div>
                    <div><strong>Total Cost:</strong> <asp:Literal ID="litDetailCost" runat="server" /></div>
                </div>
                <div class="mb-3">
                    <strong>Work Performed:</strong><br />
                    <asp:Literal ID="litDetailWorkPerformed" runat="server" />
                </div>
                <div class="mb-3">
                    <strong>Parts Used (Summary Note):</strong><br />
                    <asp:Literal ID="litDetailPartsUsed" runat="server" />
                </div>

                <h4 class="bn-subheading bn-subheading--sm bn-mt-4">Itemised Parts Used</h4>
                <asp:GridView ID="gvDetailParts" runat="server" CssClass="ops-grid" AutoGenerateColumns="false" GridLines="None"
                              OnRowDataBound="gvDetailParts_RowDataBound"
                              EmptyDataText="No itemised parts recorded for this service.">
                    <Columns>
                        <asp:BoundField DataField="PartName" HeaderText="Part Name" />
                        <asp:BoundField DataField="PartNumber" HeaderText="Part #" />
                        <asp:BoundField DataField="Supplier" HeaderText="Supplier" />
                        <asp:BoundField DataField="Quantity" HeaderText="Qty" DataFormatString="{0:N2}" />
                        <asp:TemplateField HeaderText="Unit Cost">
                            <ItemTemplate><%# FormatMoney(Eval("UnitCost")) %></ItemTemplate>
                        </asp:TemplateField>
                        <asp:TemplateField HeaderText="Line Total">
                            <ItemTemplate><%# FormatMoney(Eval("LineTotal")) %></ItemTemplate>
                        </asp:TemplateField>
                    </Columns>
                </asp:GridView>

                <div class="ops-actions bn-mt-4">
                    <asp:Button ID="btnCloseDetails" runat="server" Text="Close Details" CssClass="btn-ops-secondary" CausesValidation="false" OnClick="btnCloseDetails_Click" />
                </div>
            </asp:Panel>

            <%-- Shown once a fault has been picked, so the mechanic can record what was done. --%>
            <asp:Panel CssClass="bn-callout bn-mt-6" ID="pnlService" runat="server" Visible="false">

                <h3 class="bn-subheading bn-subheading--sm"><asp:Literal ID="litServiceTitle" runat="server" Text="Log completed service" /></h3>
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

                <asp:Panel ID="pnlReturnToService" runat="server" CssClass="mb-3">
                    <asp:CheckBox ID="chkReturnToService" runat="server" Checked="true"
                                  Text=" Return this bus to service now" />
                </asp:Panel>

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
                <asp:HiddenField ID="hfIsEditMode" runat="server" Value="0" />

            </asp:Panel>

        </div>

        <div class="ops-box bn-mt-6">
            <h2 class="ops-title bn-subheading">Upcoming service</h2>
            <div class="ops-grid-wrap">
                <asp:GridView ID="gvUpcoming" runat="server" CssClass="ops-grid"
                              AutoGenerateColumns="false" GridLines="None"
                              OnRowDataBound="gvUpcoming_RowDataBound"
                              EmptyDataText="No bus has a service scheduled.">
                    <Columns>
                        <asp:BoundField DataField="BusRegistration" HeaderText="Bus" />
                        <asp:BoundField DataField="Model" HeaderText="Model" />
                        <asp:BoundField DataField="NextServiceDueDate" HeaderText="Service due" DataFormatString="{0:dd MMM yyyy}" />
                        <asp:BoundField DataField="LastServiceDate" HeaderText="Last serviced" DataFormatString="{0:dd MMM yyyy}" NullDisplayText="Not recorded" />
                        <asp:TemplateField HeaderText="Status">
                            <ItemTemplate>
                                <span class='<%# "status-badge " + DueCss(Eval("IsOverdue"), Eval("IsDueSoon")) %>'>
                                    <%# DueLabel(Eval("IsOverdue"), Eval("IsDueSoon")) %>
                                </span>
                            </ItemTemplate>
                        </asp:TemplateField>
                    </Columns>
                </asp:GridView>
            </div>
        </div>

        <div class="ops-box bn-mt-6">
            <h2 class="ops-title bn-subheading">Recently completed</h2>
            <div class="ops-grid-wrap">
                <asp:GridView ID="gvCompleted" runat="server" CssClass="ops-grid"
                              AutoGenerateColumns="false" GridLines="None"
                              OnRowCommand="gvCompleted_RowCommand"
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
                        <asp:TemplateField HeaderText="Action">
                            <ItemTemplate>
                                <asp:LinkButton
                                    ID="btnViewCompletedDetails"
                                    runat="server"
                                    CommandName="ViewDetails"
                                    CommandArgument='<%# Eval("MaintenanceLogId") %>'
                                    CssClass="btn-ops-secondary"
                                    CausesValidation="false"
                                    Text="View" />
                                <asp:LinkButton
                                    ID="btnEditCompletedOverdue"
                                    runat="server"
                                    CommandName="EditOverdue"
                                    CommandArgument='<%# Eval("MaintenanceLogId") %>'
                                    CssClass="btn-ops-secondary"
                                    CausesValidation="false"
                                    Visible='<%# CanEditRecords && IsOverdueAndNotCancelled(Eval("NextServiceDueDate"), Eval("Status")) %>'
                                    Text="Edit" />
                            </ItemTemplate>
                        </asp:TemplateField>
                    </Columns>
                </asp:GridView>
            </div>
        </div>

    </div>
    </div>

</asp:Content>
