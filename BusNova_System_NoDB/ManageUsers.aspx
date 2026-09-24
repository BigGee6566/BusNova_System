<%@ Page Title="Manage Users" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="ManageUsers.aspx.cs" Inherits="BusNova_System.ManageUsers" %>
<asp:Content ID="Content1" ContentPlaceHolderID="head" runat="server">
    <link href="Styles/style.css?v=7" rel="stylesheet" />
    <link href="Styles/Ops.css?v=8" rel="stylesheet" />
</asp:Content>
<asp:Content ID="Content2" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">

    <div class="ops-page">
    <div class="ops-wrap ops-wrap--wide">

        <a class="ops-back-link" href="Dashboard.aspx">&larr; Back to Dashboard</a>

        <div class="ops-box">

            <h2 class="ops-title">Create Staff Account</h2>
            <div class="ops-subtitle">
                Staff accounts (Manager, Driver, Maintenance Staff) can
                only be created here - never from the public Register page.
            </div>

            <div class="row">
                <div class="col-md-4 mb-3">
                    <label for="<%= txtUsername.ClientID %>">Username<span class="required-mark">*</span></label>
                    <asp:TextBox ID="txtUsername" runat="server" CssClass="form-control" MaxLength="50" />
                    <asp:RequiredFieldValidator
                        runat="server" ControlToValidate="txtUsername"
                        ErrorMessage="Username is required." CssClass="field-error"
                        Display="Dynamic" ValidationGroup="CreateStaff" />
                </div>
                <div class="col-md-4 mb-3">
                    <label for="<%= txtPassword.ClientID %>">Temporary password<span class="required-mark">*</span></label>
                    <asp:TextBox ID="txtPassword" runat="server" CssClass="form-control" TextMode="Password" />
                    <asp:RequiredFieldValidator
                        runat="server" ControlToValidate="txtPassword"
                        ErrorMessage="Password is required." CssClass="field-error"
                        Display="Dynamic" ValidationGroup="CreateStaff" />
                </div>
                <div class="col-md-4 mb-3">
                    <label for="<%= ddlRole.ClientID %>">Role<span class="required-mark">*</span></label>
                    <asp:DropDownList ID="ddlRole" runat="server" CssClass="form-select">
                        <asp:ListItem Text="-- Select Role --" Value="" />
                        <asp:ListItem Text="Manager" Value="Manager" />
                        <asp:ListItem Text="Driver" Value="Driver" />
                        <asp:ListItem Text="Maintenance Staff" Value="MaintenanceStaff" />
                    </asp:DropDownList>
                    <asp:RequiredFieldValidator
                        runat="server" ControlToValidate="ddlRole" InitialValue=""
                        ErrorMessage="Please choose a role." CssClass="field-error"
                        Display="Dynamic" ValidationGroup="CreateStaff" />
                </div>
            </div>

            <div class="row">
                <div class="col-md-3 mb-3">
                    <label for="<%= txtFirstName.ClientID %>">First name<span class="required-mark">*</span></label>
                    <asp:TextBox ID="txtFirstName" runat="server" CssClass="form-control" MaxLength="50" />
                    <asp:RequiredFieldValidator
                        runat="server" ControlToValidate="txtFirstName"
                        ErrorMessage="First name is required." CssClass="field-error"
                        Display="Dynamic" ValidationGroup="CreateStaff" />
                </div>
                <div class="col-md-3 mb-3">
                    <label for="<%= txtLastName.ClientID %>">Last name<span class="required-mark">*</span></label>
                    <asp:TextBox ID="txtLastName" runat="server" CssClass="form-control" MaxLength="50" />
                    <asp:RequiredFieldValidator
                        runat="server" ControlToValidate="txtLastName"
                        ErrorMessage="Last name is required." CssClass="field-error"
                        Display="Dynamic" ValidationGroup="CreateStaff" />
                </div>
                <div class="col-md-3 mb-3">
                    <label for="<%= txtEmail.ClientID %>">Email<span class="required-mark">*</span></label>
                    <asp:TextBox ID="txtEmail" runat="server" CssClass="form-control" MaxLength="100" />
                    <asp:RequiredFieldValidator
                        runat="server" ControlToValidate="txtEmail"
                        ErrorMessage="Email is required." CssClass="field-error"
                        Display="Dynamic" ValidationGroup="CreateStaff" />
                    <asp:RegularExpressionValidator
                        runat="server" ControlToValidate="txtEmail"
                        ErrorMessage="Enter a valid email address."
                        ValidationExpression="^[^@\s]+@[^@\s]+\.[^@\s]+$"
                        CssClass="field-error" Display="Dynamic" ValidationGroup="CreateStaff" />
                </div>
                <div class="col-md-3 mb-3">
                    <label for="<%= txtPhone.ClientID %>">Phone<span class="required-mark">*</span></label>
                    <asp:TextBox ID="txtPhone" runat="server" CssClass="form-control" MaxLength="15" />
                    <asp:RequiredFieldValidator
                        runat="server" ControlToValidate="txtPhone"
                        ErrorMessage="Phone number is required." CssClass="field-error"
                        Display="Dynamic" ValidationGroup="CreateStaff" />
                </div>
            </div>

            <div class="row">
                <div class="col-md-4 mb-3">
                    <label for="<%= txtLicenseNumber.ClientID %>">Driver's licence number <span class="empty-note">(Driver only)</span></label>
                    <asp:TextBox ID="txtLicenseNumber" runat="server" CssClass="form-control" MaxLength="20" />
                </div>
                <div class="col-md-4 mb-3">
                    <label for="<%= txtLicenseExpiry.ClientID %>">Licence expiry <span class="empty-note">(Driver only)</span></label>
                    <asp:TextBox ID="txtLicenseExpiry" runat="server" TextMode="Date" CssClass="form-control" />
                </div>
                <div class="col-md-4 mb-3">
                    <label for="<%= txtSpecialisation.ClientID %>">Specialisation <span class="empty-note">(Maintenance Staff only)</span></label>
                    <asp:TextBox ID="txtSpecialisation" runat="server" CssClass="form-control" MaxLength="50" />
                </div>
            </div>

            <div class="ops-actions">
                <asp:Button ID="btnCreateStaff" runat="server" Text="Create Account"
                            CssClass="btn-ops-primary" ValidationGroup="CreateStaff"
                            OnClick="btnCreateStaff_Click" />
                <asp:Button ID="btnClear" runat="server" Text="Clear"
                            CssClass="btn-ops-secondary" CausesValidation="false"
                            OnClick="btnClear_Click" />
            </div>

            <asp:Label ID="lblMessage" runat="server" CssClass="ops-message" />

        </div>

        <div class="ops-box bn-mt-6">
            <h2 class="ops-title bn-subheading">Staff accounts</h2>
            <div class="ops-grid-wrap">
                <asp:GridView
                    ID="gvEmployees"
                    runat="server"
                    CssClass="ops-grid"
                    AutoGenerateColumns="false"
                    GridLines="None"
                    DataKeyNames="EmployeeId"
                    EmptyDataText="No staff accounts found."
                    OnRowCommand="gvEmployees_RowCommand"
                    OnRowDataBound="gvEmployees_RowDataBound">
                    <Columns>
                        <asp:BoundField DataField="Username" HeaderText="Username" />
                        <asp:BoundField DataField="FullName" HeaderText="Name" />
                        <asp:BoundField DataField="Email" HeaderText="Email" />
                        <asp:TemplateField HeaderText="Role">
                            <%-- Fully qualified deliberately. Every .aspx auto-imports
                                 System.Web.Security, whose Roles class wins an unqualified
                                 reference and has no Display method - which made this page
                                 fail to compile on every request. See AUD-052. --%>
                            <ItemTemplate><%# Server.HtmlEncode(BusNova_System.Models.Roles.Display(Convert.ToString(Eval("Role")))) %></ItemTemplate>
                        </asp:TemplateField>
                        <asp:BoundField DataField="DateHired" HeaderText="Hired" DataFormatString="{0:dd MMM yyyy}" />
                        <asp:TemplateField HeaderText="Status">
                            <ItemTemplate>
                                <span class='<%# "status-badge " + (Convert.ToBoolean(Eval("IsActive")) ? "status-completed" : "status-muted") %>'>
                                    <%# Convert.ToBoolean(Eval("IsActive")) ? "Active" : "Deactivated" %>
                                </span>
                            </ItemTemplate>
                        </asp:TemplateField>
                        <asp:TemplateField HeaderText="Action">
                            <ItemTemplate>
                                <asp:LinkButton
                                    ID="btnToggleActive"
                                    runat="server"
                                    CommandName="ToggleActive"
                                    CommandArgument='<%# Eval("EmployeeId") %>'
                                    CssClass="btn-ops-secondary"
                                    CausesValidation="false"
                                    data-bn-confirm-title='<%# (Convert.ToBoolean(Eval("IsActive")) ? "Deactivate this account?" : "Reactivate this account?") %>'
                                    data-bn-confirm='<%# (Convert.ToBoolean(Eval("IsActive")) ? "They will not be able to sign in until the account is reactivated." : "They will be able to sign in again immediately.") %>'
                                    data-bn-confirm-ok='<%# (Convert.ToBoolean(Eval("IsActive")) ? "Deactivate" : "Reactivate") %>' 
                                    Text='<%# Convert.ToBoolean(Eval("IsActive")) ? "Deactivate" : "Reactivate" %>' />
                            </ItemTemplate>
                        </asp:TemplateField>
                    </Columns>
                </asp:GridView>
            </div>
        </div>

        <div class="ops-box bn-mt-6">
            <h2 class="ops-title bn-subheading">Passenger accounts</h2>
            <div class="ops-grid-wrap">
                <asp:GridView
                    ID="gvPassengers"
                    runat="server"
                    CssClass="ops-grid"
                    AutoGenerateColumns="false"
                    GridLines="None"
                    DataKeyNames="PassengerId"
                    EmptyDataText="No passenger accounts found."
                    OnRowCommand="gvPassengers_RowCommand"
                    OnRowDataBound="gvPassengers_RowDataBound">
                    <Columns>
                        <asp:BoundField DataField="FullName" HeaderText="Name" />
                        <asp:BoundField DataField="Email" HeaderText="Email" />
                        <asp:BoundField DataField="PhoneNumber" HeaderText="Phone" />
                        <asp:BoundField DataField="DateRegistered" HeaderText="Registered" DataFormatString="{0:dd MMM yyyy}" />
                        <asp:TemplateField HeaderText="Status">
                            <ItemTemplate>
                                <span class='<%# "status-badge " + (Convert.ToBoolean(Eval("IsActive")) ? "status-completed" : "status-muted") %>'>
                                    <%# Convert.ToBoolean(Eval("IsActive")) ? "Active" : "Deactivated" %>
                                </span>
                            </ItemTemplate>
                        </asp:TemplateField>
                        <asp:TemplateField HeaderText="Action">
                            <ItemTemplate>
                                <asp:LinkButton
                                    ID="btnTogglePassengerActive"
                                    runat="server"
                                    CommandName="ToggleActive"
                                    CommandArgument='<%# Eval("PassengerId") %>'
                                    CssClass="btn-ops-secondary"
                                    CausesValidation="false"
                                    data-bn-confirm-title='<%# (Convert.ToBoolean(Eval("IsActive")) ? "Deactivate this account?" : "Reactivate this account?") %>'
                                    data-bn-confirm='<%# (Convert.ToBoolean(Eval("IsActive")) ? "They will not be able to sign in until the account is reactivated." : "They will be able to sign in again immediately.") %>'
                                    data-bn-confirm-ok='<%# (Convert.ToBoolean(Eval("IsActive")) ? "Deactivate" : "Reactivate") %>' 
                                    Text='<%# Convert.ToBoolean(Eval("IsActive")) ? "Deactivate" : "Reactivate" %>' />
                            </ItemTemplate>
                        </asp:TemplateField>
                    </Columns>
                </asp:GridView>
            </div>
        </div>

    </div>
    </div>

</asp:Content>
