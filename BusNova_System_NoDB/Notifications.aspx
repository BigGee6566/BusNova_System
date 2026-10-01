<%@ Page Title="Notifications" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Notifications.aspx.cs" Inherits="BusNova_System.Notifications" %>
<asp:Content ID="Content1" ContentPlaceHolderID="head" runat="server">
    <link href="Styles/Ops.css?v=9" rel="stylesheet" />
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
                        <asp:TemplateField HeaderText="Type">
                            <ItemTemplate><%# TypeText(Eval("NotificationType")) %></ItemTemplate>
                        </asp:TemplateField>
                        <asp:TemplateField HeaderText="Message" ItemStyle-CssClass="wrap wrap--wide">
                            <%-- The subject says what the message is; the address
                                 under it says who it is for. --%>
                            <ItemTemplate>
                                <span class="cell-stack">
                                    <span><%# Server.HtmlEncode(Convert.ToString(Eval("Subject"))) %></span>
                                    <span class="cell-meta"><%# Server.HtmlEncode(Convert.ToString(Eval("Recipient"))) %></span>
                                </span>
                            </ItemTemplate>
                        </asp:TemplateField>
                        <%-- Status, attempts and the next retry are one fact -
                             where delivery stands - so they share a cell. As
                             three columns they left the message itself one
                             word wide. --%>
                        <asp:TemplateField HeaderText="Delivery">
                            <ItemTemplate>
                                <span class="cell-stack">
                                    <span class='<%# "status-badge " + StatusCss(Eval("Status")) %>'>
                                        <%# StatusText(Eval("Status")) %>
                                    </span>
                                    <span class="cell-meta">
                                        Attempt <%# Eval("AttemptCount") %> of <%# Eval("MaxAttempts") %>
                                    </span>
                                    <asp:Label runat="server" CssClass="cell-meta"
                                               Visible='<%# NextAttempt(Eval("Status"), Eval("NextAttemptAt")).Length > 0 %>'
                                               Text='<%# Server.HtmlEncode(NextAttempt(Eval("Status"), Eval("NextAttemptAt"))) %>' />
                                </span>
                            </ItemTemplate>
                        </asp:TemplateField>
                        <asp:TemplateField HeaderText="Last error" ItemStyle-CssClass="wrap">
                            <ItemTemplate>
                                <asp:Label runat="server" CssClass='<%# LastError(Eval("LastError")).Css %>'
                                           Text='<%# Server.HtmlEncode(LastError(Eval("LastError")).Text) %>' />
                            </ItemTemplate>
                        </asp:TemplateField>
                    </Columns>
                </asp:GridView>
            </div>
        </div>

    </div>

</asp:Content>
