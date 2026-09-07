<%@ Page Title="Create Route" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="CreateRoute.aspx.cs" Inherits="BusNova_System.CreateRoute" %>
<asp:Content ID="Content1" ContentPlaceHolderID="head" runat="server">
    <link href="Styles/style.css?v=4" rel="stylesheet" />
    <link href="Styles/Ops.css?v=4" rel="stylesheet" />
</asp:Content>
<asp:Content ID="Content2" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">

    <div class="ops-page">
    <div class="ops-wrap">

        <a class="ops-back-link" href="Dashboard.aspx">&larr; Back to Dashboard</a>

        <div class="ops-box">

            <h3 class="ops-title">Create Route</h3>
            <div class="ops-subtitle">Create a new bus route for the BusNova fleet management system.</div>

            <div class="row">
                <div class="col-md-6 mb-3">
                    <label>Route Name</label>
                    <asp:TextBox
                        ID="txtRouteName"
                        runat="server"
                        CssClass="form-control"
                        MaxLength="50"
                        placeholder="e.g. Alice to East London">
                    </asp:TextBox>
                    <asp:RequiredFieldValidator
                        runat="server"
                        ControlToValidate="txtRouteName"
                        ErrorMessage="Route name is required."
                        CssClass="field-error"
                        Display="Dynamic"
                        ValidationGroup="CreateRoute" />
                </div>

                <div class="col-md-6 mb-3">
                    <label>Origin</label>
                    <asp:TextBox
                        ID="txtOrigin"
                        runat="server"
                        CssClass="form-control"
                        MaxLength="50"
                        placeholder="e.g. Alice">
                    </asp:TextBox>
                    <asp:RequiredFieldValidator
                        runat="server"
                        ControlToValidate="txtOrigin"
                        ErrorMessage="Origin is required."
                        CssClass="field-error"
                        Display="Dynamic"
                        ValidationGroup="CreateRoute" />
                </div>
            </div>

            <div class="row">
                <div class="col-md-6 mb-3">
                    <label>Destination</label>
                    <asp:TextBox
                        ID="txtDestination"
                        runat="server"
                        CssClass="form-control"
                        MaxLength="50"
                        placeholder="e.g. East London">
                    </asp:TextBox>
                    <asp:RequiredFieldValidator
                        runat="server"
                        ControlToValidate="txtDestination"
                        ErrorMessage="Destination is required."
                        CssClass="field-error"
                        Display="Dynamic"
                        ValidationGroup="CreateRoute" />
                </div>

                <div class="col-md-6 mb-3">
                    <label>Distance (KM)</label>
                    <asp:TextBox
                        ID="txtDistanceKM"
                        runat="server"
                        CssClass="form-control"
                        MaxLength="6"
                        placeholder="e.g. 120">
                    </asp:TextBox>
                    <asp:RequiredFieldValidator
                        runat="server"
                        ControlToValidate="txtDistanceKM"
                        ErrorMessage="Distance is required."
                        CssClass="field-error"
                        Display="Dynamic"
                        ValidationGroup="CreateRoute" />
                    <asp:RegularExpressionValidator
                        runat="server"
                        ControlToValidate="txtDistanceKM"
                        ErrorMessage="Enter a valid distance in KM."
                        ValidationExpression="^[0-9]+(\.[0-9]{1,2})?$"
                        CssClass="field-error"
                        Display="Dynamic"
                        ValidationGroup="CreateRoute" />
                </div>
            </div>

            <div class="mb-3">
                <label>Estimated Duration (Minutes)</label>
                <asp:TextBox
                    ID="txtEstimatedDuration"
                    runat="server"
                    CssClass="form-control"
                    MaxLength="4"
                    placeholder="e.g. 150">
                </asp:TextBox>
                <asp:RequiredFieldValidator
                    runat="server"
                    ControlToValidate="txtEstimatedDuration"
                    ErrorMessage="Estimated duration is required."
                    CssClass="field-error"
                    Display="Dynamic"
                    ValidationGroup="CreateRoute" />
                <asp:RegularExpressionValidator
                    runat="server"
                    ControlToValidate="txtEstimatedDuration"
                    ErrorMessage="Enter a valid number of minutes."
                    ValidationExpression="^[1-9][0-9]*$"
                    CssClass="field-error"
                    Display="Dynamic"
                    ValidationGroup="CreateRoute" />
            </div>

            <div class="ops-actions">
                <asp:Button
                    ID="btnCreateRoute"
                    runat="server"
                    Text="Create Route"
                    CssClass="btn-ops-primary"
                    ValidationGroup="CreateRoute"
                    OnClick="btnCreateRoute_Click" />
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
