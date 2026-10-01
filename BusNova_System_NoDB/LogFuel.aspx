<%@ Page Title="Log Fuel" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="LogFuel.aspx.cs" Inherits="BusNova_System.LogFuel" %>
<asp:Content ID="Content1" ContentPlaceHolderID="head" runat="server">
    <link href="Styles/style.css?v=8" rel="stylesheet" />
    <link href="Styles/Ops.css?v=9" rel="stylesheet" />
</asp:Content>
<asp:Content ID="Content2" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">

    <div class="ops-page">
    <div class="ops-wrap ops-wrap--default">

        <div class="ops-box">

            <h2 class="ops-title">Log Fuel</h2>
            <div class="ops-subtitle">
                Record a refuelling against a trip you're driving, or against a bus directly
                if you're not on a scheduled trip right now.
            </div>

            <div class="bn-cols-2">
                <div class="bn-field">
                    <label for="<%= ddlSource.ClientID %>">Trip or bus<span class="required-mark">*</span></label>
                    <asp:DropDownList
                        ID="ddlSource"
                        runat="server"
                        CssClass="form-select"
                        AutoPostBack="true"
                        OnSelectedIndexChanged="ddlSource_SelectedIndexChanged">
                        <asp:ListItem Text="-- Select a trip or a bus --" Value="" />
                    </asp:DropDownList>
                    <asp:RequiredFieldValidator
                        runat="server"
                        ControlToValidate="ddlSource"
                        ErrorMessage="Please select a trip or a bus."
                        CssClass="field-error"
                        Display="Dynamic"
                        ValidationGroup="LogFuel" />
                    <div class="empty-note bn-mt-2">
                        <asp:Literal ID="litLastOdometer" runat="server" />
                    </div>
                </div>

                <div class="bn-field">
                    <label for="<%= txtDate.ClientID %>">Date<span class="required-mark">*</span></label>
                    <asp:TextBox ID="txtDate" runat="server" TextMode="Date" CssClass="form-control" />
                    <asp:RequiredFieldValidator
                        runat="server"
                        ControlToValidate="txtDate"
                        ErrorMessage="Date is required."
                        CssClass="field-error"
                        Display="Dynamic"
                        ValidationGroup="LogFuel" />
                </div>
            </div>

            <div class="bn-cols-3">
                <div class="bn-field">
                    <label for="<%= txtLitres.ClientID %>">Fuel amount (litres)<span class="required-mark">*</span></label>
                    <asp:TextBox ID="txtLitres" runat="server" CssClass="form-control" placeholder="e.g. 120.5" />
                    <asp:RequiredFieldValidator
                        runat="server"
                        ControlToValidate="txtLitres"
                        ErrorMessage="Fuel amount is required."
                        CssClass="field-error"
                        Display="Dynamic"
                        ValidationGroup="LogFuel" />
                    <asp:RegularExpressionValidator
                        runat="server"
                        ControlToValidate="txtLitres"
                        ErrorMessage="Enter a number, e.g. 120.5."
                        ValidationExpression="^[0-9]+(\.[0-9]{1,2})?$"
                        CssClass="field-error"
                        Display="Dynamic"
                        ValidationGroup="LogFuel" />
                </div>

                <div class="bn-field">
                    <label for="<%= txtCost.ClientID %>">Cost<span class="required-mark">*</span></label>
                    <asp:TextBox ID="txtCost" runat="server" CssClass="form-control" placeholder="e.g. 2150.00" />
                    <asp:RequiredFieldValidator
                        runat="server"
                        ControlToValidate="txtCost"
                        ErrorMessage="Cost is required."
                        CssClass="field-error"
                        Display="Dynamic"
                        ValidationGroup="LogFuel" />
                    <asp:RegularExpressionValidator
                        runat="server"
                        ControlToValidate="txtCost"
                        ErrorMessage="Enter a number, e.g. 2150.00."
                        ValidationExpression="^[0-9]+(\.[0-9]{1,2})?$"
                        CssClass="field-error"
                        Display="Dynamic"
                        ValidationGroup="LogFuel" />
                </div>

                <div class="bn-field">
                    <label for="<%= txtOdometer.ClientID %>">Odometer reading (km)<span class="required-mark">*</span></label>
                    <asp:TextBox ID="txtOdometer" runat="server" CssClass="form-control" placeholder="e.g. 154200" />
                    <asp:RequiredFieldValidator
                        runat="server"
                        ControlToValidate="txtOdometer"
                        ErrorMessage="Odometer reading is required."
                        CssClass="field-error"
                        Display="Dynamic"
                        ValidationGroup="LogFuel" />
                    <asp:RegularExpressionValidator
                        runat="server"
                        ControlToValidate="txtOdometer"
                        ErrorMessage="Enter a whole number of kilometres."
                        ValidationExpression="^[0-9]+$"
                        CssClass="field-error"
                        Display="Dynamic"
                        ValidationGroup="LogFuel" />
                </div>
            </div>

            <div class="mb-3">
                <label for="<%= txtStation.ClientID %>">Filling station</label>
                <asp:TextBox ID="txtStation" runat="server" CssClass="form-control"
                             placeholder="Optional" MaxLength="50" />
            </div>

            <div class="ops-actions">
                <asp:Button
                    ID="btnLogFuel"
                    runat="server"
                    Text="Log Fuel"
                    CssClass="btn-ops-primary"
                    ValidationGroup="LogFuel"
                    OnClick="btnLogFuel_Click" />
                <asp:Button
                    ID="btnClear"
                    runat="server"
                    Text="Clear"
                    CssClass="btn-ops-secondary"
                    CausesValidation="false"
                    OnClick="btnClear_Click" />
            </div>

            <asp:Label ID="lblMessage" runat="server" CssClass="ops-message" />

        </div>

        <div class="ops-box bn-mt-6">
            <h2 class="ops-title bn-subheading">Your recent fuel logs</h2>
            <div class="ops-grid-wrap">
                <asp:GridView ID="gvFuelLogs" runat="server" CssClass="ops-grid"
                              AutoGenerateColumns="false" GridLines="None"
                              OnRowDataBound="gvFuelLogs_RowDataBound"
                              EmptyDataText="You haven't logged any fuel yet.">
                    <Columns>
                        <asp:BoundField DataField="DateLogged" HeaderText="Date" DataFormatString="{0:dd MMM yyyy}" />
                        <asp:BoundField DataField="BusRegistration" HeaderText="Bus" />
                        <asp:BoundField DataField="RouteName" HeaderText="Trip" />
                        <asp:BoundField DataField="FuelAmountLiters" HeaderText="Litres" DataFormatString="{0:N2}" />
                        <asp:TemplateField HeaderText="Cost">
                            <ItemTemplate><%# FormatMoney(Eval("Cost")) %></ItemTemplate>
                        </asp:TemplateField>
                        <asp:BoundField DataField="OdometerReading" HeaderText="Odometer" DataFormatString="{0:N0}" />
                        <asp:BoundField DataField="Station" HeaderText="Station" />
                    </Columns>
                </asp:GridView>
            </div>
        </div>

    </div>
    </div>

</asp:Content>
