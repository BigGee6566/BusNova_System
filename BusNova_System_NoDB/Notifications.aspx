<%@ Page Title="Notifications" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Notifications.aspx.cs" Inherits="BusNova_System.Notifications" %>
<asp:Content ID="Content1" ContentPlaceHolderID="head" runat="server">
    <link href="Styles/Ops.css?v=8" rel="stylesheet" />
</asp:Content>
<asp:Content ID="Content2" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">

    <div class="ops-wrap ops-wrap--wide">

        <%-- The page title is rendered once, by Site.Master's top bar. --%>
        <p class="bn-page-intro">
            Everything the system has queued to send, and what became of it.
        </p>

        <div class="bn-card">
            <p class="bn-note">
                No email service is currently configured for this deployment. Notifications are
                safely recorded here and will be sent as soon as one is enabled.
            </p>
            <details class="bn-details">
                <summary>About this queue</summary>
                <p class="bn-note">
                    Delivery is the queue itself &mdash; a message here has been durably recorded,
                    not emailed. That is deliberate: a system that silently discarded messages
                    while reporting success would be worse than one that is honest about not
                    having sent them.
                </p>
            </details>

            <div class="ops-actions ops-actions--start">
                <asp:Button ID="btnDispatch" runat="server" Text="Process due notifications"
                            CssClass="bn-btn bn-btn--primary" OnClick="btnDispatch_Click" />
            </div>

            <asp:Label ID="lblMessage" runat="server" />
        </div>

        <div class="bn-card bn-mt-3">
            <div class="ops-grid-wrap">
                <asp:GridView ID="gvNotifications" runat="server" AutoGenerateColumns="false"
                              CssClass="ops-grid" GridLines="None" UseAccessibleHeader="true"
                              OnRowDataBound="gvNotifications_RowDataBound"
                              EmptyDataText="Nothing has been queued yet.">
                    <Columns>
                        <asp:BoundField DataField="CreatedAt" HeaderText="Queued" DataFormatString="{0:dd MMM yyyy HH:mm}" />
                        <asp:BoundField DataField="NotificationType" HeaderText="Type" />
                        <asp:BoundField DataField="Recipient" HeaderText="To" />
                        <asp:BoundField DataField="Subject" HeaderText="Subject" />
                        <asp:TemplateField HeaderText="Status">
                            <ItemTemplate>
                                <span class='<%# "status-badge " + StatusCss(Eval("Status")) %>'>
                                    <%# StatusText(Eval("Status")) %>
                                </span>
                            </ItemTemplate>
                        </asp:TemplateField>
                        <asp:TemplateField HeaderText="Attempts">
                            <ItemTemplate>
                                <%# Eval("AttemptCount") %> / <%# Eval("MaxAttempts") %>
                            </ItemTemplate>
                        </asp:TemplateField>
                        <asp:TemplateField HeaderText="Next attempt">
                            <ItemTemplate>
                                <%# NextAttempt(Eval("Status"), Eval("NextAttemptAt")) %>
                            </ItemTemplate>
                        </asp:TemplateField>
                        <asp:TemplateField HeaderText="Last error">
                            <ItemTemplate>
                                <%# LastError(Eval("LastError")) %>
                            </ItemTemplate>
                        </asp:TemplateField>
                    </Columns>
                </asp:GridView>
            </div>
        </div>

    </div>

</asp:Content>
