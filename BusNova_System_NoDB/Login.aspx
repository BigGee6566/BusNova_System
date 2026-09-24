<%@ Page Title="Login" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Login.aspx.cs" Inherits="BusNova_System.Login" %>
<asp:Content ID="Content1" ContentPlaceHolderID="head" runat="server">
    <link href="Styles/style.css?v=7" rel="stylesheet" />
</asp:Content>
<asp:Content ID="Content2" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">
     <div class="login-wrap">
     <div class="login-box">

         <div class="status-pill">
             <span class="status-dot"></span>
             Route Network Online
         </div>

         <h3 class="login-title">Sign in</h3>

         <div class="mb-3">
             <label for="<%= txtEmail.ClientID %>">Email<span class="required-mark">*</span></label>
             <asp:TextBox
                 ID="txtEmail"
                 runat="server"
                 CssClass="form-control"
                 placeholder="you@example.com">
             </asp:TextBox>
             <asp:RequiredFieldValidator
                 runat="server"
                 ControlToValidate="txtEmail"
                 ErrorMessage="Email is required."
                 CssClass="field-error"
                 Display="Dynamic"
                 ValidationGroup="Login" />
         </div>

         <div class="mb-3">
             <label for="<%= txtPassword.ClientID %>">Password<span class="required-mark">*</span></label>
             <asp:TextBox
                 ID="txtPassword"
                 runat="server"
                 TextMode="Password"
                 CssClass="form-control"
                 placeholder="Enter your password">
             </asp:TextBox>
             <asp:RequiredFieldValidator
                 runat="server"
                 ControlToValidate="txtPassword"
                 ErrorMessage="Password is required."
                 CssClass="field-error"
                 Display="Dynamic"
                 ValidationGroup="Login" />
         </div>

         <asp:Button
             ID="btnLogin"
             runat="server"
             Text="Login"
             CssClass="btn btn-login"
             ValidationGroup="Login"
             OnClick="btnLogin_Click" />

         <div class="mt-3 text-center">
             <asp:Label
                 ID="lblMessage"
                 runat="server">
             </asp:Label>
         </div>

         <div class="switch-link">
             Don't have an account? <a href="Register.aspx">Register</a>
         </div>

     </div>
 </div>
</asp:Content>
