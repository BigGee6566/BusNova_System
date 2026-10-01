<%@ Page Title="Register" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Register.aspx.cs" Inherits="BusNova_System.Register" %>
<asp:Content ID="Content1" ContentPlaceHolderID="head" runat="server">
    <link href="Styles/style.css?v=8" rel="stylesheet" />
    <script src="Scripts/password-strength.js?v=1"></script>
</asp:Content>
<asp:Content ID="Content2" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">
    <div class="reg-wrap">
    <div class="reg-box">

        <h1 class="reg-title">Create an account</h1>
        <div class="reg-subtitle">Register as a Passenger.</div>

        <div class="bn-cols-2">
            <div class="bn-field">
                <label for="<%= txtFirstName.ClientID %>">First Name<span class="required-mark">*</span></label>
                <asp:TextBox
                    ID="txtFirstName"
                    runat="server"
                    CssClass="form-control"
                    MaxLength="50"
                    placeholder="e.g. Thando">
                </asp:TextBox>
                <asp:RequiredFieldValidator
                    runat="server"
                    ControlToValidate="txtFirstName"
                    ErrorMessage="First name is required."
                    CssClass="field-error"
                    Display="Dynamic"
                    ValidationGroup="Register" />
            </div>

            <div class="bn-field">
                <label for="<%= txtLastName.ClientID %>">Last Name<span class="required-mark">*</span></label>
                <asp:TextBox
                    ID="txtLastName"
                    runat="server"
                    CssClass="form-control"
                    MaxLength="50"
                    placeholder="e.g. Mahlangu">
                </asp:TextBox>
                <asp:RequiredFieldValidator
                    runat="server"
                    ControlToValidate="txtLastName"
                    ErrorMessage="Last name is required."
                    CssClass="field-error"
                    Display="Dynamic"
                    ValidationGroup="Register" />
            </div>
        </div>

        <div class="mb-3">
            <label for="<%= txtRegEmail.ClientID %>">Email<span class="required-mark">*</span></label>
            <asp:TextBox
                ID="txtRegEmail"
                runat="server"
                CssClass="form-control"
                MaxLength="100"
                placeholder="you@example.com">
            </asp:TextBox>
            <asp:RequiredFieldValidator
                runat="server"
                ControlToValidate="txtRegEmail"
                ErrorMessage="Email is required."
                CssClass="field-error"
                Display="Dynamic"
                ValidationGroup="Register" />
            <asp:RegularExpressionValidator
                runat="server"
                ControlToValidate="txtRegEmail"
                ErrorMessage="Enter a valid email address."
                ValidationExpression="^[^@\s]+@[^@\s]+\.[^@\s]+$"
                CssClass="field-error"
                Display="Dynamic"
                ValidationGroup="Register" />
        </div>

        <div class="mb-3">
            <label for="<%= txtContactNumber.ClientID %>">Contact Number<span class="required-mark">*</span></label>
            <asp:TextBox
                ID="txtContactNumber"
                runat="server"
                CssClass="form-control"
                MaxLength="15"
                placeholder="e.g. 0821234567">
            </asp:TextBox>
            <asp:RequiredFieldValidator
                runat="server"
                ControlToValidate="txtContactNumber"
                ErrorMessage="Contact number is required."
                CssClass="field-error"
                Display="Dynamic"
                ValidationGroup="Register" />
            <asp:RegularExpressionValidator
                runat="server"
                ControlToValidate="txtContactNumber"
                ErrorMessage="Enter a valid contact number (digits only, max 15)."
                ValidationExpression="^\+?[0-9\s]{7,15}$"
                CssClass="field-error"
                Display="Dynamic"
                ValidationGroup="Register" />
        </div>

        

        <div class="mb-3">
            <label for="<%= txtRegPassword.ClientID %>">Password<span class="required-mark">*</span></label>
            <asp:TextBox
                ID="txtRegPassword"
                runat="server"
                TextMode="Password"
                CssClass="form-control"
                placeholder="Enter your password"
                data-role="password-strength-input">
            </asp:TextBox>
            <asp:RequiredFieldValidator
                runat="server"
                ControlToValidate="txtRegPassword"
                ErrorMessage="Password is required."
                CssClass="field-error"
                Display="Dynamic"
                ValidationGroup="Register" />
            <asp:RegularExpressionValidator
                runat="server"
                ControlToValidate="txtRegPassword"
                ErrorMessage="Password must be at least 8 characters and include an uppercase letter, a lowercase letter, a number and a special character."
                ValidationExpression="^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^A-Za-z0-9]).{8,}$"
                CssClass="field-error"
                Display="Dynamic"
                ValidationGroup="Register" />

            <%-- Live strength meter, purely a UX convenience - the
                 RegularExpressionValidator above and PasswordPolicy on the
                 server are what actually enforce the rule either way. --%>
            <div class="pwd-strength" data-role="password-strength-meter" aria-live="polite">
                <div class="pwd-strength-bar">
                    <div class="pwd-strength-bar-fill" data-role="password-strength-fill"></div>
                </div>
                <div class="pwd-strength-label" data-role="password-strength-label">&nbsp;</div>
                <ul class="pwd-strength-checklist">
                    <li data-rule="length">At least 8 characters</li>
                    <li data-rule="uppercase">An uppercase letter</li>
                    <li data-rule="lowercase">A lowercase letter</li>
                    <li data-rule="digit">A number</li>
                    <li data-rule="special">A special character (e.g. ! @ # $ %)</li>
                </ul>
            </div>
        </div>

        <div class="mb-3">
            <label for="<%= txtConfirmPassword.ClientID %>">Confirm Password<span class="required-mark">*</span></label>
            <asp:TextBox
                ID="txtConfirmPassword"
                runat="server"
                TextMode="Password"
                CssClass="form-control"
                placeholder="Enter your password">
            </asp:TextBox>
            <asp:RequiredFieldValidator
                runat="server"
                ControlToValidate="txtConfirmPassword"
                ErrorMessage="Please confirm your password."
                CssClass="field-error"
                Display="Dynamic"
                ValidationGroup="Register" />
            <asp:CompareValidator
                runat="server"
                ControlToValidate="txtConfirmPassword"
                ControlToCompare="txtRegPassword"
                ErrorMessage="Passwords do not match."
                CssClass="field-error"
                Display="Dynamic"
                ValidationGroup="Register" />
        </div>

        <asp:Button
            ID="btnRegister"
            runat="server"
            Text="Register"
            CssClass="btn btn-register"
            ValidationGroup="Register"
            OnClick="btnRegister_Click" />

        <div class="mt-3 text-center">
            <asp:Label
                ID="lblRegMessage"
                runat="server">
            </asp:Label>
        </div>

        <div class="switch-link">
            Already have an account? <a href="Login.aspx">Sign in</a>
        </div>
      </div>
    </div>
</asp:Content>
