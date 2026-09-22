<%@ Page Title="Something went wrong" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Error.aspx.cs" Inherits="BusNova_System.ErrorPage" %>
<asp:Content ID="Content1" ContentPlaceHolderID="head" runat="server">
    <link href="Styles/style.css?v=6" rel="stylesheet" />
    <link href="Styles/Ops.css?v=6" rel="stylesheet" />
</asp:Content>
<asp:Content ID="Content2" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">

    <div class="ops-page">
    <div class="ops-wrap">
        <div class="ops-box">

            <h3 class="ops-title"><asp:Literal ID="litTitle" runat="server" /></h3>
            <div class="ops-subtitle"><asp:Literal ID="litMessage" runat="server" /></div>

            <p class="bn-note bn-mt-5">
                If this keeps happening, note what you were doing at the time and
                report it to a system Manager.
            </p>

            <div class="ops-actions">
                <asp:HyperLink ID="lnkHome" runat="server"
                               NavigateUrl="~/Home.aspx"
                               CssClass="btn-ops-secondary"
                               Text="Back to Home" />
                <asp:HyperLink ID="lnkDashboard" runat="server"
                               NavigateUrl="~/Dashboard.aspx"
                               CssClass="btn-ops-secondary"
                               Text="Back to my dashboard"
                               Visible="false" />
            </div>

        </div>
    </div>
    </div>

</asp:Content>
