<%@ Page Title="Payment" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="PaymentSimulation.aspx.cs" Inherits="BusNova_System.PaymentSimulation" %>
<asp:Content ID="Content1" ContentPlaceHolderID="head" runat="server">
    <link href="Styles/style.css?v=7" rel="stylesheet" />
    <link href="Styles/Ops.css?v=8" rel="stylesheet" />
    <script src="Scripts/payment-simulation.js?v=3"></script>
</asp:Content>
<asp:Content ID="Content2" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">

    <div class="ops-page">
    <div class="ops-wrap ops-wrap--narrow">

        <a class="ops-back-link" href="BookTrip.aspx">&larr; Back to trip search</a>

        <div class="ops-box">

            <span class="demo-payment-badge">Demo Payment &mdash; no real charge is made</span>

            <h2 class="ops-title bn-mt-3">Payment</h2>
            <div class="ops-subtitle">
                Review your booking, choose a payment method, and confirm. This is a
                simulation for the purposes of this project - no real payment gateway is involved.
            </div>

            <div class="payment-summary">
                <div class="payment-summary-row">
                    <span>Route</span>
                    <strong><asp:Literal ID="litRoute" runat="server" /></strong>
                </div>
                <div class="payment-summary-row">
                    <span>Departure</span>
                    <strong><asp:Literal ID="litDeparture" runat="server" /></strong>
                </div>
                <div class="payment-summary-row">
                    <span>Traveller(s)</span>
                    <strong><asp:Literal ID="litTravellerCount" runat="server" /></strong>
                </div>

                <div class="ops-grid-wrap bn-mt-4 bn-mb-4">
                    <%-- OnRowDataBound puts the header into a real <thead> and
                         writes the data-label each cell needs to stay readable
                         once the table collapses into cards below 700px.
                         AUD-066: this grid and Manifest's were the only two in
                         the application missing it. --%>
                    <asp:GridView ID="gvTravellers" runat="server" CssClass="ops-grid"
                                  AutoGenerateColumns="false" GridLines="None"
                                  OnRowDataBound="gvTravellers_RowDataBound">
                        <Columns>
                            <asp:BoundField DataField="SeatNumber" HeaderText="Seat" />
                            <asp:BoundField DataField="FullName" HeaderText="Traveller" />
                            <asp:TemplateField HeaderText="Fare">
                                <ItemTemplate><%# FormatFare(Eval("Fare")) %></ItemTemplate>
                            </asp:TemplateField>
                        </Columns>
                    </asp:GridView>
                </div>

                <div class="payment-summary-row payment-summary-total">
                    <span>Total payable</span>
                    <strong><asp:Literal ID="litTotal" runat="server" /></strong>
                </div>
            </div>

            <h4 class="bn-subheading bn-mt-6">Payment method</h4>

            <div class="payment-method-options" data-role="payment-method-options">
                <label class="payment-method-option">
                    <asp:RadioButton ID="rbCard" runat="server" GroupName="PaymentMethod" Checked="true"
                                      data-role="payment-method-radio" data-target="card-fields" />
                    <i class="fas fa-credit-card" aria-hidden="true"></i> Card
                </label>
                <label class="payment-method-option">
                    <asp:RadioButton ID="rbWallet" runat="server" GroupName="PaymentMethod"
                                      data-role="payment-method-radio" />
                    <i class="fas fa-mobile-screen" aria-hidden="true"></i> Mobile wallet
                </label>
                <label class="payment-method-option">
                    <asp:RadioButton ID="rbEft" runat="server" GroupName="PaymentMethod"
                                      data-role="payment-method-radio" />
                    <i class="fas fa-building-columns" aria-hidden="true"></i> Bank transfer / EFT
                </label>
            </div>

            <%-- Only shown/required when Card is selected - see payment-simulation.js.
                 Values are validated for shape only and are never persisted anywhere;
                 TicketService.CreateBooking never receives them. --%>
            <asp:Panel ID="pnlCardFields" runat="server" CssClass="card-fields" data-role="card-fields">
                <div class="row">
                    <div class="col-md-12 mb-3">
                        <label for="<%= txtCardName.ClientID %>">Cardholder name<span class="required-mark">*</span></label>
                        <asp:TextBox ID="txtCardName" runat="server" CssClass="form-control" MaxLength="100" autocomplete="cc-name" />
                        <asp:RequiredFieldValidator ID="rfvCardName"
                            runat="server" ControlToValidate="txtCardName"
                            ErrorMessage="Cardholder name is required." CssClass="field-error"
                            Display="Dynamic" ValidationGroup="Payment" />
                    </div>
                </div>
                <div class="row">
                    <div class="col-md-6 mb-3">
                        <label for="<%= txtCardNumber.ClientID %>">Card number<span class="required-mark">*</span></label>
                        <asp:TextBox ID="txtCardNumber" runat="server" CssClass="form-control"
                                     placeholder="4111 1111 1111 1111" MaxLength="19" autocomplete="cc-number" />
                        <asp:RequiredFieldValidator ID="rfvCardNumber"
                            runat="server" ControlToValidate="txtCardNumber"
                            ErrorMessage="Card number is required." CssClass="field-error"
                            Display="Dynamic" ValidationGroup="Payment" />
                        <asp:RegularExpressionValidator ID="revCardNumber"
                            runat="server" ControlToValidate="txtCardNumber"
                            ErrorMessage="Enter a 16-digit demo card number."
                            ValidationExpression="^\d{4}\s?\d{4}\s?\d{4}\s?\d{4}$"
                            CssClass="field-error" Display="Dynamic" ValidationGroup="Payment" />
                    </div>
                    <div class="col-md-3 mb-3">
                        <label for="<%= txtCardExpiry.ClientID %>">Expiry (MM/YY)<span class="required-mark">*</span></label>
                        <asp:TextBox ID="txtCardExpiry" runat="server" CssClass="form-control"
                                     placeholder="MM/YY" MaxLength="5" autocomplete="cc-exp" />
                        <asp:RequiredFieldValidator ID="rfvCardExpiry"
                            runat="server" ControlToValidate="txtCardExpiry"
                            ErrorMessage="Expiry date is required." CssClass="field-error"
                            Display="Dynamic" ValidationGroup="Payment" />
                        <asp:RegularExpressionValidator ID="revCardExpiry"
                            runat="server" ControlToValidate="txtCardExpiry"
                            ErrorMessage="Use MM/YY."
                            ValidationExpression="^(0[1-9]|1[0-2])\/\d{2}$"
                            CssClass="field-error" Display="Dynamic" ValidationGroup="Payment" />
                    </div>
                    <div class="col-md-3 mb-3">
                        <label for="<%= txtCardCvv.ClientID %>">CVV<span class="required-mark">*</span></label>
                        <asp:TextBox ID="txtCardCvv" runat="server" CssClass="form-control" TextMode="Password"
                                     placeholder="123" MaxLength="4" autocomplete="cc-csc" />
                        <asp:RequiredFieldValidator ID="rfvCardCvv"
                            runat="server" ControlToValidate="txtCardCvv"
                            ErrorMessage="CVV is required." CssClass="field-error"
                            Display="Dynamic" ValidationGroup="Payment" />
                        <asp:RegularExpressionValidator ID="revCardCvv"
                            runat="server" ControlToValidate="txtCardCvv"
                            ErrorMessage="3 or 4 digits."
                            ValidationExpression="^\d{3,4}$"
                            CssClass="field-error" Display="Dynamic" ValidationGroup="Payment" />
                    </div>
                </div>
                <p class="empty-note">
                    This is a demo form. Do not enter a real card number - nothing you type here
                    is stored or sent anywhere; it is only checked for shape before being discarded.
                </p>
            </asp:Panel>

            <div class="ops-actions">
                <asp:Button ID="btnConfirmPayment" runat="server" Text="Confirm payment"
                            CssClass="btn-ops-primary" ValidationGroup="Payment"
                            OnClick="btnConfirmPayment_Click" />
                <asp:Button ID="btnCancel" runat="server" Text="Cancel booking"
                            CssClass="btn-ops-secondary" CausesValidation="false"
                            OnClick="btnCancel_Click" />
            </div>

            <asp:Label ID="lblMessage" runat="server" CssClass="ops-message" />

        </div>
    </div>
    </div>

</asp:Content>
