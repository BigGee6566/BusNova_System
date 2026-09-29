<%@ Page Title="Something went wrong" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Error.aspx.cs" Inherits="BusNova_System.ErrorPage" %>
<asp:Content ID="Content1" ContentPlaceHolderID="head" runat="server">
    <link href="Styles/style.css?v=8" rel="stylesheet" />
    <link href="Styles/Ops.css?v=9" rel="stylesheet" />
</asp:Content>
<asp:Content ID="Content2" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">

    <div class="ops-page">
    <div class="ops-wrap">
        <div class="ops-box">

            <h2 class="ops-title"><asp:Literal ID="litTitle" runat="server" /></h2>
            <div class="ops-subtitle"><asp:Literal ID="litMessage" runat="server" /></div>

            <%-- bn-mt-4, not bn-mt-5: only bn-mt-2/3/4/6 are defined in
                 busnova-components.css, so bn-mt-5 matched nothing and this
                 paragraph had no top margin at all. --%>
            <p class="bn-note bn-mt-4">
                If this keeps happening, note what you were doing at the time and
                report it to a system Manager.
            </p>

            <%-- Both destinations are worked out server-side in Page_Load.
                 Neither is a fixed href, and neither is taken from the
                 request without being validated first - see
                 Security/LocalUrl and the notes in the code-behind. --%>
            <div class="ops-actions">
                <asp:HyperLink ID="lnkBack" runat="server"
                               CssClass="btn-ops-secondary"
                               Text="&larr; Go back" />
                <asp:HyperLink ID="lnkHome" runat="server"
                               CssClass="btn-ops-secondary" />
            </div>

        </div>
    </div>
    </div>

</asp:Content>
