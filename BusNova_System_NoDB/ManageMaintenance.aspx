<%@ Page Title="Manage Maintenance" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="ManageMaintenance.aspx.cs" Inherits="BusNova_System.ManageMaintenance" %>
<asp:Content ID="Content1" ContentPlaceHolderID="head" runat="server">
    <link href="Styles/style.css?v=8" rel="stylesheet" />
    <link href="Styles/Ops.css?v=9" rel="stylesheet" />
</asp:Content>
<asp:Content ID="Content2" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">

    <div class="ops-page">
    <div class="ops-wrap ops-wrap--default">

        <div class="ops-box">

            <h2 class="ops-title">Manage Bus Maintenance</h2>
            <div class="ops-subtitle">
                Set next service due dates, ground a bus for scheduled maintenance, or update maintenance notes.
            </div>

            <asp:Label ID="lblMessage" runat="server" CssClass="ops-message" />

            <div class="mb-3">
                <label for="<%= ddlBus.ClientID %>">Select Bus<span class="required-mark">*</span></label>
                <asp:DropDownList ID="ddlBus" runat="server" CssClass="form-select"
                                   AutoPostBack="true" OnSelectedIndexChanged="ddlBus_SelectedIndexChanged">
                    <asp:ListItem Text="-- Select a bus --" Value="" />
                </asp:DropDownList>
                <asp:RequiredFieldValidator
                    runat="server"
                    ControlToValidate="ddlBus"
                    ErrorMessage="Please select a bus."
                    CssClass="field-error"
                    Display="Dynamic"
                    ValidationGroup="ManageMaint" />
            </div>

            <asp:Panel ID="pnlBusDetails" runat="server" Visible="false" CssClass="bn-callout bn-mb-4">
                <div class="bn-cols-2 bn-mb-3">
                    <div><strong>Current Bus Status:</strong> <asp:Literal ID="litCurrentStatus" runat="server" /></div>
                    <div><strong>Current Next Service Due:</strong> <asp:Literal ID="litCurrentNextDue" runat="server" /></div>
                </div>

                <div class="mb-3">
                    <label for="<%= txtNewNextDue.ClientID %>">New Next Service Due Date<span class="required-mark">*</span></label>
                    <asp:TextBox ID="txtNewNextDue" runat="server" TextMode="Date" CssClass="form-control" />
                    <asp:RequiredFieldValidator
                        runat="server"
                        ControlToValidate="txtNewNextDue"
                        ErrorMessage="Next service due date is required."
                        CssClass="field-error"
                        Display="Dynamic"
                        ValidationGroup="ManageMaint" />
                </div>

                <div class="mb-3">
                    <label for="<%= txtNotes.ClientID %>">Notes / Maintenance Reason</label>
                    <asp:TextBox ID="txtNotes" runat="server" TextMode="MultiLine" Rows="3" CssClass="form-control"
                                 placeholder="e.g. Scheduled 15,000 km oil and filter change" MaxLength="500" />
                </div>

                <div class="mb-3">
                    <asp:CheckBox ID="chkPutUnderMaintenance" runat="server"
                                  Text=" Put this bus under maintenance now (grounds bus and notifies Maintenance Staff)" />
                </div>

                <div class="ops-actions">
                    <asp:Button ID="btnSave" runat="server" Text="Save Maintenance Schedule"
                                CssClass="btn-ops-primary" ValidationGroup="ManageMaint"
                                OnClick="btnSave_Click" />
                </div>
            </asp:Panel>

        </div>

    </div>
    </div>

</asp:Content>
