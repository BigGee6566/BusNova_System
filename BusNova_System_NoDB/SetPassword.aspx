<%@ Page Title="Set Password" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="SetPassword.aspx.cs" Inherits="BusNova_System.SetPassword" %>
<asp:Content ID="Content1" ContentPlaceHolderID="head" runat="server">
    <link href="Styles/style.css?v=6" rel="stylesheet" />
</asp:Content>
<asp:Content ID="Content2" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">
    <div class="reg-wrap">
    <div class="reg-box">

        <h3 class="reg-title">Set your password</h3>

        <asp:Panel ID="pnlForm" runat="server">
            <div class="reg-subtitle">
                <asp:Literal ID="litIntro" runat="server" />
            </div>

            <div class="mb-3">
                <label for="<%= txtNewPassword.ClientID %>">New password<span class="required-mark">*</span></label>
                <asp:TextBox
                    ID="txtNewPassword"
                    runat="server"
                    TextMode="Password"
                    CssClass="form-control"
                    placeholder="Enter a password">
                </asp:TextBox>
                <asp:RequiredFieldValidator
                    runat="server"
                    ControlToValidate="txtNewPassword"
                    ErrorMessage="Password is required."
                    CssClass="field-error"
                    Display="Dynamic"
                    ValidationGroup="SetPassword" />
                <%-- Mirrors PasswordPolicy so the user is told the rule before
                     submitting. The authoritative check is the server-side
                     PasswordPolicy.IsValid call in the code-behind - this
                     expression is a convenience, not the rule. --%>
                <asp:RegularExpressionValidator
                    runat="server"
                    ControlToValidate="txtNewPassword"
                    ErrorMessage="Password must be at least 8 characters and include an uppercase letter, a lowercase letter, a number and a special character."
                    ValidationExpression="(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^a-zA-Z0-9]).{8,}"
                    CssClass="field-error"
                    Display="Dynamic"
                    ValidationGroup="SetPassword" />
            </div>

            <div class="mb-3">
                <label for="<%= txtConfirmPassword.ClientID %>">Confirm password<span class="required-mark">*</span></label>
                <asp:TextBox
                    ID="txtConfirmPassword"
                    runat="server"
                    TextMode="Password"
                    CssClass="form-control"
                    placeholder="Enter the same password again">
                </asp:TextBox>
                <asp:RequiredFieldValidator
                    runat="server"
                    ControlToValidate="txtConfirmPassword"
                    ErrorMessage="Please confirm your password."
                    CssClass="field-error"
                    Display="Dynamic"
                    ValidationGroup="SetPassword" />
                <asp:CompareValidator
                    runat="server"
                    ControlToValidate="txtConfirmPassword"
                    ControlToCompare="txtNewPassword"
                    ErrorMessage="Passwords do not match."
                    CssClass="field-error"
                    Display="Dynamic"
                    ValidationGroup="SetPassword" />
            </div>

            <asp:Button
                ID="btnSetPassword"
                runat="server"
                Text="Set password"
                CssClass="btn btn-register"
                ValidationGroup="SetPassword"
                OnClick="btnSetPassword_Click" />
        </asp:Panel>

        <div class="mt-3 text-center">
            <asp:Label
                ID="lblMessage"
                runat="server"
                ForeColor="#C0392B">
            </asp:Label>
        </div>

        <asp:Panel ID="pnlNeedNewLink" runat="server" Visible="false" CssClass="switch-link">
            Need a new link? <a href="RequestPasswordLink.aspx">Request one here</a>
        </asp:Panel>

        <div class="switch-link">
            Already set your password? <a href="Login.aspx">Sign in</a>
        </div>
      </div>
    </div>
</asp:Content>
