<%@ Page Title="Get Set-Password Link" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="RequestPasswordLink.aspx.cs" Inherits="BusNova_System.RequestPasswordLink" %>
<asp:Content ID="Content1" ContentPlaceHolderID="head" runat="server">
    <link href="Styles/style.css?v=7" rel="stylesheet" />
</asp:Content>
<asp:Content ID="Content2" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">
    <div class="reg-wrap">
    <div class="reg-box">

        <h3 class="reg-title">Get your set-password link</h3>
        <div class="reg-subtitle">
            If a conductor registered you and you've lost the link, enter the email or phone number
            they used and a new one will be shown below.
        </div>

        <div class="mb-3">
            <label for="<%= txtContact.ClientID %>">Email or phone number<span class="required-mark">*</span></label>
            <asp:TextBox
                ID="txtContact"
                runat="server"
                CssClass="form-control"
                placeholder="you@example.com or 0821234567">
            </asp:TextBox>
            <asp:RequiredFieldValidator
                runat="server"
                ControlToValidate="txtContact"
                ErrorMessage="Enter your email or phone number."
                CssClass="field-error"
                Display="Dynamic"
                ValidationGroup="RequestLink" />
        </div>

        <asp:Button
            ID="btnRequestLink"
            runat="server"
            Text="Get my link"
            CssClass="btn btn-register"
            ValidationGroup="RequestLink"
            OnClick="btnRequestLink_Click" />

        <div class="mt-3 text-center">
            <asp:Label
                ID="lblMessage"
                runat="server">
            </asp:Label>
        </div>

        <asp:Panel CssClass="bn-callout bn-callout--info bn-mt-4" ID="pnlLink" runat="server" Visible="false">
            <div class="bn-mb-2">Your link (works once, expires in 48 hours):</div>
            <asp:TextBox ID="txtLink" runat="server" CssClass="form-control" ReadOnly="true" />
            <div class="bn-mt-2">
                <asp:HyperLink ID="hlLink" runat="server" Target="_blank">Open this link</asp:HyperLink>
            </div>
        </asp:Panel>

        <div class="switch-link">
            Already have a password? <a href="Login.aspx">Sign in</a>
        </div>
      </div>
    </div>
</asp:Content>
