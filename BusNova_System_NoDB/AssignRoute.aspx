<%@ Page Title="Assign Route" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="AssignRoute.aspx.cs" Inherits="BusNova_System.AssignRoute" %>
<asp:Content ID="Content1" ContentPlaceHolderID="head" runat="server">
    <link href="Styles/style.css?v=6" rel="stylesheet" />
    <link href="Styles/Ops.css?v=6" rel="stylesheet" />
</asp:Content>
<asp:Content ID="Content2" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">

    <div class="ops-page">
    <div class="ops-wrap">

        <a class="ops-back-link" href="Dashboard.aspx">&larr; Back to Dashboard</a>

        <div class="ops-box">

            <h3 class="ops-title">Assign Route</h3>
            <div class="ops-subtitle">Assign an available bus and driver to a route and schedule.</div>

            <div class="row">
                <div class="col-md-4 mb-3">
                    <label for="<%= ddlRoute.ClientID %>">Route<span class="required-mark">*</span></label>
                    <%-- Populated from dbo.Route in Page_Load. --%>
                    <asp:DropDownList
                        ID="ddlRoute"
                        runat="server"
                        CssClass="form-select"
                        AppendDataBoundItems="true"
                        DataTextField="DisplayName"
                        DataValueField="RouteCode">
                        <asp:ListItem Text="-- Select Route --" Value="" />
                    </asp:DropDownList>
                    <asp:RequiredFieldValidator
                        runat="server"
                        ControlToValidate="ddlRoute"
                        InitialValue=""
                        ErrorMessage="Please select a route."
                        CssClass="field-error"
                        Display="Dynamic"
                        ValidationGroup="AssignRoute" />
                </div>

                <div class="col-md-4 mb-3">
                    <label for="<%= ddlBus.ClientID %>">Bus<span class="required-mark">*</span></label>
                    <%-- Active buses only - see FleetService.GetAssignableBuses. --%>
                    <asp:DropDownList
                        ID="ddlBus"
                        runat="server"
                        CssClass="form-select"
                        AppendDataBoundItems="true"
                        DataTextField="DisplayName"
                        DataValueField="BusId">
                        <asp:ListItem Text="-- Select Bus --" Value="" />
                    </asp:DropDownList>
                    <asp:RequiredFieldValidator
                        runat="server"
                        ControlToValidate="ddlBus"
                        InitialValue=""
                        ErrorMessage="Please select a bus."
                        CssClass="field-error"
                        Display="Dynamic"
                        ValidationGroup="AssignRoute" />
                </div>

                <div class="col-md-4 mb-3">
                    <label for="<%= ddlDriver.ClientID %>">Driver<span class="required-mark">*</span></label>
                    <%-- Active drivers only - see FleetService.GetActiveDrivers. --%>
                    <asp:DropDownList
                        ID="ddlDriver"
                        runat="server"
                        CssClass="form-select"
                        AppendDataBoundItems="true"
                        DataTextField="FullName"
                        DataValueField="EmployeeId">
                        <asp:ListItem Text="-- Select Driver --" Value="" />
                    </asp:DropDownList>
                    <asp:RequiredFieldValidator
                        runat="server"
                        ControlToValidate="ddlDriver"
                        InitialValue=""
                        ErrorMessage="Please select a driver."
                        CssClass="field-error"
                        Display="Dynamic"
                        ValidationGroup="AssignRoute" />
                </div>
            </div>

            <div class="row">
                <div class="col-md-4 mb-3">
                    <label for="<%= txtAssignmentDate.ClientID %>">Assignment Date<span class="required-mark">*</span></label>
                    <asp:TextBox
                        ID="txtAssignmentDate"
                        runat="server"
                        TextMode="Date"
                        CssClass="form-control">
                    </asp:TextBox>
                    <asp:RequiredFieldValidator
                        runat="server"
                        ControlToValidate="txtAssignmentDate"
                        ErrorMessage="Assignment date is required."
                        CssClass="field-error"
                        Display="Dynamic"
                        ValidationGroup="AssignRoute" />
                </div>

                <div class="col-md-4 mb-3">
                    <label for="<%= txtDepartureTime.ClientID %>">Departure Time<span class="required-mark">*</span></label>
                    <asp:TextBox
                        ID="txtDepartureTime"
                        runat="server"
                        TextMode="Time"
                        CssClass="form-control">
                    </asp:TextBox>
                    <asp:RequiredFieldValidator
                        runat="server"
                        ControlToValidate="txtDepartureTime"
                        ErrorMessage="Departure time is required."
                        CssClass="field-error"
                        Display="Dynamic"
                        ValidationGroup="AssignRoute" />
                </div>

                <div class="col-md-4 mb-3">
                    <label for="<%= txtArrivalTime.ClientID %>">Arrival Time</label>
                    <asp:TextBox
                        ID="txtArrivalTime"
                        runat="server"
                        TextMode="Time"
                        CssClass="form-control">
                    </asp:TextBox>
                    <!-- Arrival time is not required at assignment stage — it is
                         often only confirmed once the trip is complete. -->
                </div>
            </div>

            <div class="mb-3">
                <label for="<%= txtPassengerCount.ClientID %>">Passenger Count</label>
                <asp:TextBox
                    ID="txtPassengerCount"
                    runat="server"
                    CssClass="form-control"
                    MaxLength="3"
                    placeholder="Optional — leave blank if unknown">
                </asp:TextBox>
                <asp:RegularExpressionValidator
                    runat="server"
                    ControlToValidate="txtPassengerCount"
                    ErrorMessage="Passenger count must be a whole number."
                    ValidationExpression="^[0-9]+$"
                    CssClass="field-error"
                    Display="Dynamic"
                    ValidationGroup="AssignRoute" />
            </div>

            <div class="ops-actions">
                <asp:Button
                    ID="btnAssignRoute"
                    runat="server"
                    Text="Assign Route"
                    CssClass="btn-ops-primary"
                    ValidationGroup="AssignRoute"
                    OnClick="btnAssignRoute_Click" />
                <asp:Button
                    ID="btnClear"
                    runat="server"
                    Text="Clear"
                    CssClass="btn-ops-secondary"
                    CausesValidation="false"
                    OnClick="btnClear_Click" />
            </div>

            <asp:Label
                ID="lblMessage"
                runat="server"
                CssClass="ops-message">
            </asp:Label>

        </div>
    </div>
    </div>

</asp:Content>
