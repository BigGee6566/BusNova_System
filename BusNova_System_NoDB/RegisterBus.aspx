<%@ Page Title="Register Bus" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="RegisterBus.aspx.cs" Inherits="BusNova_System.RegisterBus" %>
<asp:Content ID="Content1" ContentPlaceHolderID="head" runat="server">
    <link href="Styles/style.css?v=8" rel="stylesheet" />
    <link href="Styles/Ops.css?v=9" rel="stylesheet" />
</asp:Content>
<asp:Content ID="Content2" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">

    <div class="ops-page">
    <div class="ops-wrap">

        <div class="ops-box">

            <h2 class="ops-title">Register Bus</h2>
            <div class="ops-subtitle">Register a new bus into the BusNova fleet management system.</div>

            <div class="bn-cols-2">
                <div class="bn-field">
                    <label for="<%= txtRegistrationNumber.ClientID %>">Registration Number<span class="required-mark">*</span></label>
                    <asp:TextBox
                        ID="txtRegistrationNumber"
                        runat="server"
                        CssClass="form-control"
                        MaxLength="15"
                        placeholder="e.g. CA 123-456">
                    </asp:TextBox>
                    <asp:RequiredFieldValidator
                        runat="server"
                        ControlToValidate="txtRegistrationNumber"
                        ErrorMessage="Registration number is required."
                        CssClass="field-error"
                        Display="Dynamic"
                        ValidationGroup="RegisterBus" />
                </div>

                <div class="bn-field">
                    <label for="<%= txtModel.ClientID %>">Model<span class="required-mark">*</span></label>
                    <asp:TextBox
                        ID="txtModel"
                        runat="server"
                        CssClass="form-control"
                        MaxLength="50"
                        placeholder="e.g. Marcopolo Torino">
                    </asp:TextBox>
                    <asp:RequiredFieldValidator
                        runat="server"
                        ControlToValidate="txtModel"
                        ErrorMessage="Model is required."
                        CssClass="field-error"
                        Display="Dynamic"
                        ValidationGroup="RegisterBus" />
                </div>
            </div>

            <div class="bn-cols-2">
                <div class="bn-field">
                    <label for="<%= txtManufacturer.ClientID %>">Manufacturer<span class="required-mark">*</span></label>
                    <asp:TextBox
                        ID="txtManufacturer"
                        runat="server"
                        CssClass="form-control"
                        MaxLength="50"
                        placeholder="e.g. Mercedes-Benz">
                    </asp:TextBox>
                    <asp:RequiredFieldValidator
                        runat="server"
                        ControlToValidate="txtManufacturer"
                        ErrorMessage="Manufacturer is required."
                        CssClass="field-error"
                        Display="Dynamic"
                        ValidationGroup="RegisterBus" />
                </div>

                <div class="bn-field">
                    <label for="<%= txtSeatingCapacity.ClientID %>">Seating Capacity<span class="required-mark">*</span></label>
                    <asp:TextBox
                        ID="txtSeatingCapacity"
                        runat="server"
                        CssClass="form-control"
                        MaxLength="3"
                        placeholder="e.g. 60">
                    </asp:TextBox>
                    <asp:RequiredFieldValidator
                        runat="server"
                        ControlToValidate="txtSeatingCapacity"
                        ErrorMessage="Seating capacity is required."
                        CssClass="field-error"
                        Display="Dynamic"
                        ValidationGroup="RegisterBus" />
                    <asp:RegularExpressionValidator
                        runat="server"
                        ControlToValidate="txtSeatingCapacity"
                        ErrorMessage="Seating capacity must be a positive whole number."
                        ValidationExpression="^[1-9][0-9]*$"
                        CssClass="field-error"
                        Display="Dynamic"
                        ValidationGroup="RegisterBus" />
                </div>
            </div>

            <div class="bn-cols-2">
                <div class="bn-field">
                    <label for="<%= ddlFuelType.ClientID %>">Fuel Type<span class="required-mark">*</span></label>
                    <asp:DropDownList
                        ID="ddlFuelType"
                        runat="server"
                        CssClass="form-select">
                        <asp:ListItem Text="-- Select Fuel Type --" Value="-- Select Fuel Type --" />
                        <asp:ListItem Text="Diesel" Value="Diesel" />
                        <asp:ListItem Text="Petrol" Value="Petrol" />
                        <asp:ListItem Text="Electric" Value="Electric" />
                        <asp:ListItem Text="Hybrid" Value="Hybrid" />
                    </asp:DropDownList>
                    <asp:RequiredFieldValidator
                        runat="server"
                        ControlToValidate="ddlFuelType"
                        InitialValue="-- Select Fuel Type --"
                        ErrorMessage="Please select a fuel type."
                        CssClass="field-error"
                        Display="Dynamic"
                        ValidationGroup="RegisterBus" />
                </div>

                <div class="bn-field">
                    <label for="<%= txtYearOfManufacture.ClientID %>">Year of Manufacture<span class="required-mark">*</span></label>
                    <asp:TextBox
                        ID="txtYearOfManufacture"
                        runat="server"
                        CssClass="form-control"
                        MaxLength="4"
                        placeholder="e.g. 2024">
                    </asp:TextBox>
                    <asp:RequiredFieldValidator
                        runat="server"
                        ControlToValidate="txtYearOfManufacture"
                        ErrorMessage="Year of manufacture is required."
                        CssClass="field-error"
                        Display="Dynamic"
                        ValidationGroup="RegisterBus" />
                    <asp:RegularExpressionValidator
                        runat="server"
                        ControlToValidate="txtYearOfManufacture"
                        ErrorMessage="Enter a valid 4-digit year."
                        ValidationExpression="^(19|20)\d{2}$"
                        CssClass="field-error"
                        Display="Dynamic"
                        ValidationGroup="RegisterBus" />
                </div>
            </div>

            <div class="bn-cols-2">
                <div class="bn-field">
                    <label for="<%= txtFuelCapacity.ClientID %>">Fuel Capacity (litres)</label>
                    <asp:TextBox
                        ID="txtFuelCapacity"
                        runat="server"
                        CssClass="form-control"
                        MaxLength="7"
                        placeholder="e.g. 300">
                    </asp:TextBox>
                    <span class="bn-note">How far the bus can run on one tank. Leave blank if you do not know it.</span>
                    <asp:RegularExpressionValidator
                        runat="server"
                        ControlToValidate="txtFuelCapacity"
                        ErrorMessage="Enter litres as a number, up to 9999.99."
                        ValidationExpression="^\d{1,4}(\.\d{1,2})?$"
                        CssClass="field-error"
                        Display="Dynamic"
                        ValidationGroup="RegisterBus" />
                </div>

                <div class="bn-field">
                    <label for="<%= txtOdometerReading.ClientID %>">Odometer Reading (km)</label>
                    <asp:TextBox
                        ID="txtOdometerReading"
                        runat="server"
                        CssClass="form-control"
                        MaxLength="7"
                        placeholder="e.g. 125000">
                    </asp:TextBox>
                    <span class="bn-note">Current mileage. Defaults to 0 for a brand-new vehicle.</span>
                    <asp:RegularExpressionValidator
                        runat="server"
                        ControlToValidate="txtOdometerReading"
                        ErrorMessage="Enter the odometer reading as a whole number of kilometres."
                        ValidationExpression="^\d{1,7}$"
                        CssClass="field-error"
                        Display="Dynamic"
                        ValidationGroup="RegisterBus" />
                </div>
            </div>

            <div class="ops-actions">
                <asp:Button
                    ID="btnRegisterBus"
                    runat="server"
                    Text="Register Bus"
                    CssClass="btn-ops-primary"
                    ValidationGroup="RegisterBus"
                    OnClick="btnRegisterBus_Click" />
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
