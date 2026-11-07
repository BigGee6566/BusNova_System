<%@ Page Title="Manage Maintenance" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="ManageMaintenance.aspx.cs" Inherits="BusNova_System.ManageMaintenance" %>
<asp:Content ID="Content1" ContentPlaceHolderID="head" runat="server">
    <link href="Styles/style.css?v=8" rel="stylesheet" />
    <link href="Styles/Ops.css?v=9" rel="stylesheet" />
</asp:Content>
<asp:Content ID="Content2" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">

    <div class="ops-page">
    <div class="ops-wrap ops-wrap--default">

        <div class="ops-box">

            <h2 class="ops-title">Manage Bus Maintenance</h2>
            <div class="ops-subtitle">
                Set a bus's next service date, ground it for scheduled maintenance, or manage its recurring service schedules.
            </div>

            <asp:Label ID="lblMessage" runat="server" CssClass="ops-message" />

            <div class="mb-3">
                <label for="<%= ddlBus.ClientID %>">Select Bus<span class="required-mark">*</span></label>
                <asp:DropDownList ID="ddlBus" runat="server" CssClass="form-select"
                                   AutoPostBack="true" OnSelectedIndexChanged="ddlBus_SelectedIndexChanged">
                    <asp:ListItem Text="-- Select a bus --" Value="" />
                </asp:DropDownList>
                <asp:RequiredFieldValidator
                    runat="server"
                    ControlToValidate="ddlBus"
                    ErrorMessage="Please select a bus."
                    CssClass="field-error"
                    Display="Dynamic"
                    ValidationGroup="ManageMaint" />
            </div>

            <asp:Panel ID="pnlBusDetails" runat="server" Visible="false" CssClass="bn-callout bn-mb-4">
                <div class="bn-cols-2 bn-mb-3">
                    <div><strong>Current Bus Status:</strong> <asp:Literal ID="litCurrentStatus" runat="server" /></div>
                    <div><strong>Current Next Service Due:</strong> <asp:Literal ID="litCurrentNextDue" runat="server" /></div>
                </div>

                <div class="mb-3">
                    <label for="<%= txtNewNextDue.ClientID %>">New Next Service Due Date<span class="required-mark">*</span></label>
                    <asp:TextBox ID="txtNewNextDue" runat="server" TextMode="Date" CssClass="form-control" />
                    <asp:RequiredFieldValidator
                        runat="server"
                        ControlToValidate="txtNewNextDue"
                        ErrorMessage="Next service due date is required."
                        CssClass="field-error"
                        Display="Dynamic"
                        ValidationGroup="ManageMaint" />
                </div>

                <div class="mb-3">
                    <label for="<%= txtNotes.ClientID %>">Notes / Maintenance Reason</label>
                    <asp:TextBox ID="txtNotes" runat="server" TextMode="MultiLine" Rows="3" CssClass="form-control"
                                 placeholder="e.g. Scheduled 15,000 km oil and filter change" MaxLength="500" />
                </div>

                <div class="mb-3">
                    <asp:CheckBox ID="chkPutUnderMaintenance" runat="server"
                                  Text=" Put this bus under maintenance now (grounds bus and notifies Maintenance Staff)" />
                </div>

                <div class="ops-actions">
                    <asp:Button ID="btnSave" runat="server" Text="Save next service date"
                                CssClass="btn-ops-primary" ValidationGroup="ManageMaint"
                                OnClick="btnSave_Click" />
                </div>
            </asp:Panel>

            <asp:Panel ID="pnlSchedules" runat="server" Visible="false" CssClass="bn-callout bn-mb-4">
                <h3 class="bn-subheading bn-subheading--sm">Recurring service schedules</h3>
                <div class="ops-subtitle bn-mb-4">
                    Repeating rules for this bus, such as every 15,000 km or 6 months, whichever comes first.
                    They appear on the Maintenance Schedule report, and maintenance staff tick them off when logging a service.
                </div>

                <div class="ops-grid-wrap bn-mb-4">
                    <asp:GridView ID="gvSchedules" runat="server" AutoGenerateColumns="false"
                                  CssClass="ops-grid" GridLines="None" UseAccessibleHeader="true"
                                  OnRowCommand="gvSchedules_RowCommand"
                                  OnRowDataBound="gvSchedules_RowDataBound">
                        <EmptyDataTemplate>
                            <div class="bn-empty">
                                <i class="fas fa-calendar-check bn-empty__icon" aria-hidden="true"></i>
                                <div class="bn-empty__title">No recurring schedules for this bus</div>
                            </div>
                        </EmptyDataTemplate>
                        <Columns>
                            <asp:BoundField DataField="ScheduleName" HeaderText="Schedule" />
                            <asp:BoundField DataField="IntervalDescription" HeaderText="Interval" />
                            <asp:BoundField DataField="LastServicedDescription" HeaderText="Last serviced" />
                            <asp:TemplateField HeaderText="Action">
                                <ItemTemplate>
                                    <asp:Button runat="server" Text="Retire" CssClass="btn-ops-secondary"
                                                CommandName="RetireSchedule"
                                                CommandArgument='<%# Eval("BusServiceScheduleId") %>'
                                                CausesValidation="false"
                                                data-bn-confirm="This schedule will stop appearing on the report."
                                                data-bn-confirm-title="Retire this schedule?"
                                                data-bn-confirm-ok="Retire" />
                                </ItemTemplate>
                            </asp:TemplateField>
                        </Columns>
                    </asp:GridView>
                </div>

                <h4 class="bn-subheading bn-subheading--sm">Add a schedule</h4>

                <div class="mb-3">
                    <label for="<%= txtScheduleName.ClientID %>">Schedule name<span class="required-mark">*</span></label>
                    <asp:TextBox ID="txtScheduleName" runat="server" CssClass="form-control"
                                 placeholder="e.g. Minor service" MaxLength="80" />
                    <asp:RequiredFieldValidator
                        runat="server"
                        ControlToValidate="txtScheduleName"
                        ErrorMessage="Give the schedule a name."
                        CssClass="field-error"
                        Display="Dynamic"
                        ValidationGroup="AddSchedule" />
                </div>

                <div class="bn-cols-2">
                    <div class="bn-field">
                        <label for="<%= txtIntervalKm.ClientID %>">Every (km)</label>
                        <asp:TextBox ID="txtIntervalKm" runat="server" CssClass="form-control" placeholder="e.g. 15000" />
                        <asp:RegularExpressionValidator
                            runat="server"
                            ControlToValidate="txtIntervalKm"
                            ErrorMessage="Enter a whole number of kilometres."
                            ValidationExpression="^[0-9]+$"
                            CssClass="field-error"
                            Display="Dynamic"
                            ValidationGroup="AddSchedule" />
                    </div>
                    <div class="bn-field">
                        <label for="<%= txtIntervalMonths.ClientID %>">Every (months)</label>
                        <asp:TextBox ID="txtIntervalMonths" runat="server" CssClass="form-control" placeholder="e.g. 6" />
                        <asp:RegularExpressionValidator
                            runat="server"
                            ControlToValidate="txtIntervalMonths"
                            ErrorMessage="Enter a whole number of months."
                            ValidationExpression="^[0-9]+$"
                            CssClass="field-error"
                            Display="Dynamic"
                            ValidationGroup="AddSchedule" />
                    </div>
                    <div class="bn-field">
                        <label for="<%= txtLastServicedKm.ClientID %>">Odometer at last service</label>
                        <asp:TextBox ID="txtLastServicedKm" runat="server" CssClass="form-control" placeholder="Optional" />
                        <asp:RegularExpressionValidator
                            runat="server"
                            ControlToValidate="txtLastServicedKm"
                            ErrorMessage="Enter a whole number of kilometres."
                            ValidationExpression="^[0-9]+$"
                            CssClass="field-error"
                            Display="Dynamic"
                            ValidationGroup="AddSchedule" />
                    </div>
                    <div class="bn-field">
                        <label for="<%= txtLastServicedOn.ClientID %>">Date of last service</label>
                        <asp:TextBox ID="txtLastServicedOn" runat="server" TextMode="Date" CssClass="form-control" />
                    </div>
                </div>

                <div class="ops-actions">
                    <asp:Button ID="btnAddSchedule" runat="server" Text="Add schedule"
                                CssClass="btn-ops-primary" ValidationGroup="AddSchedule"
                                OnClick="btnAddSchedule_Click" />
                </div>
            </asp:Panel>

        </div>

    </div>
    </div>

</asp:Content>
