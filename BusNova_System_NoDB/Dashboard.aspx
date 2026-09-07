<%@ Page Title="Dashboard" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Dashboard.aspx.cs" Inherits="BusNova_System.Dashboard" %>
<asp:Content ID="Content1" ContentPlaceHolderID="head" runat="server">
    <link href="Styles/style.css?v=4" rel="stylesheet" />
    <style>
        .dash-page {
            min-height: calc(100vh - 200px);
            padding-top: 90px;
            background-color: #F5F7FB;
        }

        .dash-wrap {
            max-width: 780px;
            margin: 0 auto;
            padding: 30px 20px 60px;
        }

        .dash-welcome {
            margin-bottom: 26px;
        }

        .dash-welcome h2 {
            margin: 0 0 6px;
            color: #14213D;
        }

        .dash-welcome p {
            color: #55607A;
            margin: 0;
        }

        .dash-grid {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(210px, 1fr));
            gap: 16px;
        }

        .dash-tile {
            display: block;
            padding: 22px;
            border-radius: 12px;
            border: 1px solid rgba(20,33,61,0.18);
            background: #FFFFFF;
            box-shadow: 0 4px 14px rgba(20,33,61,0.10);
            text-decoration: none !important;
            color: #14213D !important;
            font-weight: 600;
            transition: transform .15s ease, box-shadow .15s ease;
        }

        .dash-tile:hover {
            transform: translateY(-2px);
            box-shadow: 0 8px 20px rgba(20,33,61,0.12);
        }

        .dash-tile .dash-tile-desc {
            display: block;
            font-weight: 400;
            font-size: 0.82rem;
            color: #6B7488;
            margin-top: 4px;
        }

        .dash-tile.logout {
            border-color: rgba(192,57,43,0.3);
            color: #C0392B !important;
        }
    </style>
</asp:Content>
<asp:Content ID="Content2" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">

    <div class="dash-page">
    <div class="dash-wrap">

        <div class="dash-welcome">
            <h2>Fleet Manager Dashboard</h2>
            <p>This build has no database connected yet, so all four screens below are static UI only — nothing is saved.</p>
        </div>

        <div class="dash-grid">
            <a class="dash-tile" href="RegisterBus.aspx">
                Register Bus
                <span class="dash-tile-desc">Add a new bus to the fleet</span>
            </a>
            <a class="dash-tile" href="CreateRoute.aspx">
                Create Route
                <span class="dash-tile-desc">Define a new bus route</span>
            </a>
            <a class="dash-tile" href="AssignRoute.aspx">
                Assign Route
                <span class="dash-tile-desc">Assign a bus and driver to a route</span>
            </a>
            <a class="dash-tile" href="ViewAssignedRoutes.aspx">
                View Assigned Routes
                <span class="dash-tile-desc">Driver's schedule view</span>
            </a>
            <a class="dash-tile logout" href="Login.aspx">Logout</a>
        </div>

    </div>
    </div>

</asp:Content>
