<%@ Page Title="My Schedule" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="ViewAssignedRoutes.aspx.cs" Inherits="BusNova_System.ViewAssignedRoutes" %>
<asp:Content ID="Content1" ContentPlaceHolderID="head" runat="server">
    <link href="Styles/style.css?v=4" rel="stylesheet" />
    <link href="Styles/Ops.css?v=4" rel="stylesheet" />
</asp:Content>
<asp:Content ID="Content2" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">

    <div class="ops-page">
    <div class="ops-wrap" style="max-width: 900px;">

        <a class="ops-back-link" href="Dashboard.aspx">&larr; Back to Dashboard</a>

        <div class="ops-box">

            <h3 class="ops-title">My Schedule</h3>
            <div class="ops-subtitle">View your assigned routes and trip schedule.</div>

            <div class="ops-grid-wrap">
                <asp:GridView
                    ID="gvAssignedRoutes"
                    runat="server"
                    CssClass="ops-grid"
                    AutoGenerateColumns="false"
                    GridLines="None"
                    EmptyDataText="No routes assigned yet.">
                    <Columns>
                        <asp:BoundField DataField="AssignmentId" HeaderText="Assignment" />
                        <asp:BoundField DataField="Route" HeaderText="Route" />
                        <asp:BoundField DataField="Bus" HeaderText="Bus" />
                        <asp:BoundField DataField="Date" HeaderText="Date" />
                        <asp:BoundField DataField="Departure" HeaderText="Departure" />
                        <asp:BoundField DataField="Arrival" HeaderText="Arrival" />
                        <asp:BoundField DataField="Passengers" HeaderText="Passengers" />
                        <asp:TemplateField HeaderText="Status">
                            <ItemTemplate>
                                <span class='<%# "status-badge " + (Eval("Status").ToString() == "Completed" ? "status-completed" : "status-scheduled") %>'>
                                    <%# Eval("Status") %>
                                </span>
                            </ItemTemplate>
                        </asp:TemplateField>
                    </Columns>
                </asp:GridView>
            </div>

            <div class="ops-message" style="margin-top: 22px; color: #6B7488;">
                Sample data shown for layout purposes — this screen will pull live assignments once the database is connected.
            </div>

        </div>
    </div>
    </div>

</asp:Content>
