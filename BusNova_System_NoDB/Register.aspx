<%@ Page Title="Register" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Register.aspx.cs" Inherits="BusNova_System.Register" %>
<asp:Content ID="Content1" ContentPlaceHolderID="head" runat="server">
    <link href="Styles/style.css?v=4" rel="stylesheet" />
</asp:Content>
<asp:Content ID="Content2" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">
    <div class="reg-wrap">
    <div class="reg-box">

        <h3 class="reg-title">Create an account</h3>
        <div class="reg-subtitle">Register as a Passenger.</div>

        <div class="row">
            <div class="col-md-6 mb-3">
                <label>First Name</label>
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

            <div class="col-md-6 mb-3">
                <label>Last Name</label>
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
            <label>Email</label>
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
            <label>Contact Number</label>
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
            <label>Password</label>
            <asp:TextBox
                ID="txtRegPassword"
                runat="server"
                TextMode="Password"
                CssClass="form-control"
                placeholder="Enter your password">
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
                ErrorMessage="Password must be at least 8 characters."
                ValidationExpression=".{8,}"
                CssClass="field-error"
                Display="Dynamic"
                ValidationGroup="Register" />
        </div>

        <div class="mb-3">
            <label>Confirm Password</label>
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
                runat="server"
                ForeColor="#1E8A5F">
            </asp:Label>
        </div>

        <div class="switch-link">
            Already have an account? <a href="Login.aspx">Sign in</a>
        </div>
      </div>
    </div>
</asp:Content>
