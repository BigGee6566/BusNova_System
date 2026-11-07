<%@ Page Title="Driver Issues" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="DriverIssues.aspx.cs" Inherits="BusNova_System.DriverIssues" %>
<asp:Content ID="Content1" ContentPlaceHolderID="head" runat="server">
    <link href="Styles/style.css?v=8" rel="stylesheet" />
    <link href="Styles/Ops.css?v=9" rel="stylesheet" />
</asp:Content>
<asp:Content ID="Content2" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">

    <div class="ops-page">
    <div class="ops-wrap ops-wrap--wide">

        <div class="ops-box">

            <h2 class="ops-title">Driver Reported Issues</h2>
            <div class="ops-subtitle">
                Review bus faults and maintenance issues reported by drivers across the fleet.
            </div>

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
                    <label for="<%= ddlFilterSeverity.ClientID %>">Severity</label>
                    <asp:DropDownList ID="ddlFilterSeverity" runat="server" CssClass="form-select">
                        <asp:ListItem Text="All severities" Value="All" />
                        <asp:ListItem Text="Low" Value="Low" />
                        <asp:ListItem Text="Medium" Value="Medium" />
                        <asp:ListItem Text="High" Value="High" />
                        <asp:ListItem Text="Critical" Value="Critical" />
                    </asp:DropDownList>
                </div>
            </div>

            <div class="bn-cols-2 bn-mb-4">
                <div class="bn-field">
                    <label for="<%= ddlFilterStatus.ClientID %>">Status</label>
                    <asp:DropDownList ID="ddlFilterStatus" runat="server" CssClass="form-select">
                        <asp:ListItem Text="All statuses" Value="All" />
                        <asp:ListItem Text="Reported" Value="Reported" />
                        <asp:ListItem Text="In Progress" Value="InProgress" />
                        <asp:ListItem Text="Completed" Value="Completed" />
                        <asp:ListItem Text="Cancelled" Value="Cancelled" />
                    </asp:DropDownList>
                </div>
            </div>

            <div class="ops-actions bn-mb-6">
                <asp:Button ID="btnFilter" runat="server" Text="Filter" CssClass="btn-ops-primary" CausesValidation="false" OnClick="btnFilter_Click" />
                <asp:Button ID="btnResetFilter" runat="server" Text="Reset" CssClass="btn-ops-secondary" CausesValidation="false" OnClick="btnResetFilter_Click" />
            </div>

            <div class="ops-grid-wrap">
                <asp:GridView
                    ID="gvDriverIssues"
                    runat="server"
                    CssClass="ops-grid"
                    AutoGenerateColumns="false"
                    GridLines="None"
                    DataKeyNames="MaintenanceLogId"
                    EmptyDataText="No driver-reported issues match the selected criteria."
                    OnRowCommand="gvDriverIssues_RowCommand"
                    OnRowDataBound="gvDriverIssues_RowDataBound">
                    <Columns>
                        <asp:BoundField DataField="BusRegistration" HeaderText="Bus" />
                        <asp:BoundField DataField="ReportedByName" HeaderText="Driver" />
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
                            </ItemTemplate>
                        </asp:TemplateField>
                    </Columns>
                </asp:GridView>
            </div>

            <%-- Details panel shown when View is clicked --%>
            <asp:Panel CssClass="bn-callout bn-callout--info bn-mt-6" ID="pnlDetails" runat="server" Visible="false">
                <h3 class="bn-subheading bn-subheading--sm">Driver Issue Details</h3>
                <div class="bn-cols-3 bn-mb-3">
                    <div><strong>Bus:</strong> <asp:Literal ID="litDetailBus" runat="server" /></div>
                    <div><strong>Reported By Driver:</strong> <asp:Literal ID="litDetailReportedBy" runat="server" /></div>
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

        </div>

    </div>
    </div>

</asp:Content>
