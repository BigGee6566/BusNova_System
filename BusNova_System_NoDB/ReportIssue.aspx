<%@ Page Title="Report Issue" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="ReportIssue.aspx.cs" Inherits="BusNova_System.ReportIssue" %>
<asp:Content ID="Content1" ContentPlaceHolderID="head" runat="server">
    <link href="Styles/style.css?v=8" rel="stylesheet" />
    <link href="Styles/Ops.css?v=9" rel="stylesheet" />
</asp:Content>
<asp:Content ID="Content2" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">

    <div class="ops-page">
    <div class="ops-wrap ops-wrap--default">

        <div class="ops-box">

            <h2 class="ops-title">Report Issue</h2>
            <div class="ops-subtitle">
                Tell maintenance about a problem with a bus. A Critical report takes the
                bus out of service immediately, so choose severity carefully.
            </div>

            <div class="row">
                <div class="col-md-6 mb-3">
                    <label for="<%= ddlBus.ClientID %>">Bus<span class="required-mark">*</span></label>
                    <asp:DropDownList
                        ID="ddlBus"
                        runat="server"
                        CssClass="form-select"
                        AppendDataBoundItems="true"
                        DataTextField="DisplayName"
                        DataValueField="BusId">
                        <asp:ListItem Text="-- Select Bus --" Value="" />
                    </asp:DropDownList>
                    <asp:RequiredFieldValidator
                        runat="server"
                        ControlToValidate="ddlBus"
                        InitialValue=""
                        ErrorMessage="Please select a bus."
                        CssClass="field-error"
                        Display="Dynamic"
                        ValidationGroup="ReportIssue" />
                </div>

                <div class="col-md-6 mb-3">
                    <label for="<%= ddlSeverity.ClientID %>">Severity<span class="required-mark">*</span></label>
                    <asp:DropDownList ID="ddlSeverity" runat="server" CssClass="form-select">
                        <asp:ListItem Text="-- Select Severity --" Value="" />
                        <asp:ListItem Text="Low - cosmetic or minor" Value="Low" />
                        <asp:ListItem Text="Medium - needs attention soon" Value="Medium" />
                        <asp:ListItem Text="High - affects safe operation" Value="High" />
                        <asp:ListItem Text="Critical - bus must stop running now" Value="Critical" />
                    </asp:DropDownList>
                    <asp:RequiredFieldValidator
                        runat="server"
                        ControlToValidate="ddlSeverity"
                        InitialValue=""
                        ErrorMessage="Please choose a severity."
                        CssClass="field-error"
                        Display="Dynamic"
                        ValidationGroup="ReportIssue" />
                </div>
            </div>

            <div class="mb-3">
                <label for="<%= txtCondition.ClientID %>">Current condition of the bus<span class="required-mark">*</span></label>
                <asp:TextBox ID="txtCondition" runat="server" CssClass="form-control"
                             placeholder="e.g. Driveable but noisy, or Not driveable" MaxLength="50" />
                <asp:RequiredFieldValidator
                    runat="server"
                    ControlToValidate="txtCondition"
                    ErrorMessage="Please describe the bus's current condition."
                    CssClass="field-error"
                    Display="Dynamic"
                    ValidationGroup="ReportIssue" />
            </div>

            <div class="mb-3">
                <label for="<%= txtDescription.ClientID %>">Describe the problem<span class="required-mark">*</span></label>
                <asp:TextBox ID="txtDescription" runat="server" CssClass="form-control"
                             TextMode="MultiLine" Rows="4" MaxLength="255"
                             placeholder="What's wrong, and when did you notice it?" />
                <asp:RequiredFieldValidator
                    runat="server"
                    ControlToValidate="txtDescription"
                    ErrorMessage="Please describe the problem."
                    CssClass="field-error"
                    Display="Dynamic"
                    ValidationGroup="ReportIssue" />
                <asp:RegularExpressionValidator
                    runat="server"
                    ControlToValidate="txtDescription"
                    ErrorMessage="Please add a little more detail (at least 10 characters)."
                    ValidationExpression="^[\s\S]{10,255}$"
                    CssClass="field-error"
                    Display="Dynamic"
                    ValidationGroup="ReportIssue" />
            </div>

            <div class="ops-actions">
                <asp:Button
                    ID="btnReportIssue"
                    runat="server"
                    Text="Report Issue"
                    CssClass="btn-ops-primary"
                    ValidationGroup="ReportIssue"
                    OnClick="btnReportIssue_Click" />
                <asp:Button
                    ID="btnClear"
                    runat="server"
                    Text="Clear"
                    CssClass="btn-ops-secondary"
                    CausesValidation="false"
                    OnClick="btnClear_Click" />
            </div>

            <asp:Label ID="lblMessage" runat="server" CssClass="ops-message" />

        </div>

        <div class="ops-box bn-mt-6">
            <h2 class="ops-title bn-subheading">Issues you've reported</h2>
            <div class="ops-grid-wrap">
                <asp:GridView ID="gvMyReports" runat="server" CssClass="ops-grid"
                              AutoGenerateColumns="false" GridLines="None"
                              OnRowDataBound="gvMyReports_RowDataBound"
                              EmptyDataText="You haven't reported any issues yet.">
                    <Columns>
                        <asp:BoundField DataField="DateReported" HeaderText="Date" DataFormatString="{0:dd MMM yyyy}" />
                        <asp:BoundField DataField="BusRegistration" HeaderText="Bus" />
                        <asp:BoundField DataField="IssueDescription" HeaderText="Problem" />
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
                    </Columns>
                </asp:GridView>
            </div>
        </div>

    </div>
    </div>

</asp:Content>
