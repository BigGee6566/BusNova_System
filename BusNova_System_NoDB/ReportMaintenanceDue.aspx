<%@ Page Title="Maintenance Due" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="ReportMaintenanceDue.aspx.cs" Inherits="BusNova_System.ReportMaintenanceDue" %>
<asp:Content ID="Content1" ContentPlaceHolderID="head" runat="server">
    <link href="Styles/style.css?v=8" rel="stylesheet" />
    <link href="Styles/Ops.css?v=9" rel="stylesheet" />
    <link href="Styles/Reports.css?v=3" rel="stylesheet" />
</asp:Content>
<asp:Content ID="Content2" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">

    <div class="ops-page">
    <div class="ops-wrap ops-wrap--wide">

        <div class="ops-box">

            <h2 class="ops-title">Maintenance due and overdue</h2>
            <div class="ops-subtitle">
                Every bus in the fleet, most urgent first. A vehicle with an open critical
                fault comes above an overdue service, because one is unsafe to run and the
                other is merely late.
            </div>

            <asp:Label ID="lblMessage" runat="server" CssClass="ops-message" />

            <div class="rpt-kpis">
                <div class="rpt-kpi rpt-kpi--danger">
                    <div class="rpt-kpi__value"><asp:Literal ID="litOverdue" runat="server" /></div>
                    <div class="rpt-kpi__label">Service overdue</div>
                </div>
                <div class="rpt-kpi rpt-kpi--warn">
                    <div class="rpt-kpi__value"><asp:Literal ID="litDueSoon" runat="server" /></div>
                    <div class="rpt-kpi__label">Due within 14 days</div>
                </div>
                <div class="rpt-kpi rpt-kpi--danger">
                    <div class="rpt-kpi__value"><asp:Literal ID="litCritical" runat="server" /></div>
                    <div class="rpt-kpi__label">Buses with a critical fault</div>
                </div>
                <div class="rpt-kpi rpt-kpi--good">
                    <div class="rpt-kpi__value"><asp:Literal ID="litAvailable" runat="server" /></div>
                    <div class="rpt-kpi__label">Active and clear</div>
                </div>
            </div>

            <asp:Panel ID="pnlEmpty" runat="server" Visible="false" CssClass="rpt-empty">
                <div class="rpt-empty__icon"><i class="fas fa-bus" aria-hidden="true"></i></div>
                <div class="rpt-empty__title">No buses registered</div>
                <div>Add vehicles on the Register Bus page.</div>
            </asp:Panel>

            <asp:Panel ID="pnlResults" runat="server" Visible="false">
                <div class="ops-actions ops-actions--start">
                <asp:Button ID="btnCsv" runat="server" Text="Export CSV"
                            CssClass="bn-btn bn-btn--secondary" OnClick="btnCsv_Click" />
                <asp:Button ID="btnExcel" runat="server" Text="Export Excel"
                            CssClass="bn-btn bn-btn--secondary" OnClick="btnExcel_Click" />
                </div>

                <div class="ops-grid-wrap">
                    <asp:GridView ID="gvBuses" runat="server" CssClass="ops-grid"
                                  AutoGenerateColumns="false" GridLines="None"
                                  OnRowDataBound="gvBuses_RowDataBound">
                        <Columns>
                            <asp:BoundField DataField="BusRegistration" HeaderText="Bus" />
                            <asp:BoundField DataField="Model" HeaderText="Model" ItemStyle-CssClass="wrap" />
                            <asp:TemplateField HeaderText="Bus status">
                                <ItemTemplate>
                                    <span class='<%# "status-badge " + BusStatusCss(Eval("BusStatus")) %>'><%# Server.HtmlEncode(BusNova_System.Helpers.StatusTone.Label(Eval("BusStatus"))) %></span>
                                </ItemTemplate>
                            </asp:TemplateField>
                            <asp:TemplateField HeaderText="Next service">
                                <ItemTemplate><%# FormatDue(Eval("NextServiceDueDate")) %></ItemTemplate>
                            </asp:TemplateField>
                            <asp:TemplateField HeaderText="Position">
                                <ItemTemplate><%# FormatVerdict(Eval("Verdict"), Eval("IsOverdue"), Eval("IsDueSoon"), Eval("OpenCriticalFaults")) %></ItemTemplate>
                            </asp:TemplateField>
                            <asp:TemplateField HeaderText="Last serviced">
                                <ItemTemplate><%# FormatDate(Eval("LastServiceDate")) %></ItemTemplate>
                            </asp:TemplateField>
                            <asp:BoundField DataField="OdometerReading" HeaderText="Odometer" DataFormatString="{0:N0}"
                                            ItemStyle-CssClass="rpt-num" HeaderStyle-CssClass="rpt-num" />
                            <asp:TemplateField HeaderText="Open faults" ItemStyle-CssClass="rpt-num" HeaderStyle-CssClass="rpt-num">
                                <ItemTemplate><%# FormatFaults(Eval("OpenFaults"), Eval("OpenCriticalFaults")) %></ItemTemplate>
                            </asp:TemplateField>
                        </Columns>
                    </asp:GridView>
                </div>
            </asp:Panel>

            <div class="rpt-method">
                <strong>How these figures are calculated.</strong>
                <em>Next service</em> is the date stored against the bus, set when it was registered
                (from the configured service interval) and updated each time a service is logged.
                <em>Position</em> compares that date to today, except where a critical fault is open,
                which takes precedence because the bus should not be running at all.
                <em>Open faults</em> counts maintenance records still Reported or In Progress.
                <em>Active and clear</em> counts buses whose status is Active with no open fault and
                no overdue service &mdash; the vehicles genuinely available to schedule.
            </div>

        </div>
    </div>
    </div>

</asp:Content>
