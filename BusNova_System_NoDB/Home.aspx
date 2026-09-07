<%@ Page Title="Home" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Home.aspx.cs" Inherits="BusNova_System.Home" %>
<asp:Content ID="Content1" ContentPlaceHolderID="head" runat="server">
    <link href="Styles/Home.css?v=4" rel="stylesheet" />
</asp:Content>
<asp:Content ID="Content2" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">
    <!-- ===================================== -->
            <!-- HERO SECTION -->
            <!-- ===================================== -->

            <section class="hero-section">

                <!-- Dark Overlay -->
                <div class="hero-overlay"></div>

                <div class="container">

                    <div class="row align-items-center min-vh-100">

                        <!-- Hero Text (single column — full-bleed background photo is behind it, no separate foreground image) -->
                        <div class="col-lg-8">

                            <div class="hero-content">

                                <span class="hero-badge">
                                    Smart Transportation Solution
                                </span>

                                <h1 class="hero-title">
                                    BusNova
                                    <span>System Management</span>
                                </h1>

                                <p class="hero-text">

                                    Making public transport management efficient,
                                    reliable, and accessible.

                                    Manage buses, routes, drivers,
                                    schedules, maintenance and passenger
                                    services through one integrated platform.

                                </p>

                                <div class="hero-buttons">

                                    <a href="Register.aspx" class="btn btn-primary hero-btn">

                                        <i class="bi bi-arrow-right-circle"></i>

                                        Get Started

                                    </a>

                                </div>

                            </div>

                        </div>

                    </div>

                </div>

            </section>

            <!-- ======================================
                 ABOUT THE SYSTEM
            =======================================-->

            <section class="about-section">

                <div class="container">

                    <div class="row align-items-center">

                        <!-- Left Side -->
                        <div class="col-lg-6">

                            <span class="section-tag">
                                ABOUT THE SYSTEM
                            </span>

                            <h2>
                                Welcome to the BusNova System Management
                            </h2>

                            <p>
                                The BusNova System Management is a web-based platform
                                designed to improve the management of public transport operations
                                in Forte Town.
                            </p>

                            <p>
                                It enables fleet managers, bus drivers, passengers,
                                maintenance staff and ticket conductors to perform their daily
                                tasks efficiently through one centralized system.
                            </p>

                            <p>
                                The system improves transport efficiency, enhances passenger
                                satisfaction, reduces operational costs and provides better
                                maintenance planning for the entire fleet.
                            </p>

                        </div>

                        <!-- Right Side -->
                        <div class="col-lg-6">

                            <div class="stat-panel">
                                <div class="stat-item">
                                    <span class="stat-number">4</span>
                                    <span class="stat-label">User Roles</span>
                                </div>
                                <div class="stat-item">
                                    <span class="stat-number">24/7</span>
                                    <span class="stat-label">Fleet Visibility</span>
                                </div>
                                <div class="stat-item">
                                    <span class="stat-number">1</span>
                                    <span class="stat-label">Centralised Platform</span>
                                </div>
                            </div>

                        </div>

                    </div>

                </div>

            </section>
            <!-- ============================= -->
            <!-- OUR SERVICES SECTION -->
            <!-- ============================= -->
            <section class="services-section">
                <div class="container">

                    <div class="text-center mb-5">
                        <h2 class="section-title">Our Services</h2>
                        <p class="section-subtitle">
                            Powerful tools for managing every aspect of a smart public transport system.
                        </p>
                    </div>

                    <div class="row g-4">

                        <!-- Route Management -->
                        <div class="col-lg-4 col-md-6">
                            <div class="service-card">
                                <div class="service-icon">
                                    <i class="fas fa-route"></i>
                                </div>
                                <h4>Route Management</h4>
                                <p>Create and manage bus routes and schedules.</p>
                            </div>
                        </div>

                        <!-- Fleet Management -->
                        <div class="col-lg-4 col-md-6">
                            <div class="service-card">
                                <div class="service-icon">
                                    <i class="fas fa-bus"></i>
                                </div>
                                <h4>Fleet Management</h4>
                                <p>Register buses and assign drivers to routes.</p>
                            </div>
                        </div>

                        <!-- Maintenance -->
                        <div class="col-lg-4 col-md-6">
                            <div class="service-card">
                                <div class="service-icon">
                                    <i class="fas fa-tools"></i>
                                </div>
                                <h4>Maintenance</h4>
                                <p>Schedule servicing and monitor vehicle condition.</p>
                            </div>
                        </div>

                        <!-- Ticket Services -->
                        <div class="col-lg-4 col-md-6">
                            <div class="service-card">
                                <div class="service-icon">
                                    <i class="fas fa-ticket-alt"></i>
                                </div>
                                <h4>Ticket Services</h4>
                                <p>Purchase tickets and view bus availability.</p>
                            </div>
                        </div>

                        <!-- Reports -->
                        <div class="col-lg-4 col-md-6">
                            <div class="service-card">
                                <div class="service-icon">
                                    <i class="fas fa-chart-line"></i>
                                </div>
                                <h4>Reports & Analytics</h4>
                                <p>Generate operational, maintenance and fuel reports.</p>
                            </div>
                        </div>

                        <!-- Monitoring -->
                        <div class="col-lg-4 col-md-6">
                            <div class="service-card">
                                <div class="service-icon">
                                          <i class="fas fa-map-marker-alt"></i>
                        </div>
                                <h4>Live Trip Monitoring</h4>
                                <p>Track trip progress and bus status.</p>
                            </div>
                        </div>

                    </div>

                </div>
            </section>

            <!-- ===================================== -->
            <!-- WHY CHOOSE OUR SYSTEM -->
            <!-- ===================================== -->

            <section class="why-section">

                <div class="container">

                    <div class="row align-items-center">

                        <!-- Left Side -->
                        <div class="col-lg-6">

                            <span class="section-tag">
                                WHY CHOOSE US
                            </span>

                            <h2 class="section-title">
                                Why Choose Our System?
                            </h2>

                            <p class="why-description">

                                Our BusNova Management System provides an
                                integrated platform that improves efficiency,
                                enhances passenger satisfaction and streamlines
                                daily transport operations.

                            </p>

                            <div class="feature-list">

                                <div class="feature-item">
                                    <i class="fas fa-check-circle"></i>
                                    <span>Centralized Bus Management</span>
                                </div>

                                <div class="feature-item">
                                    <i class="fas fa-check-circle"></i>
                                    <span>Faster Route Allocation</span>
                                </div>

                                <div class="feature-item">
                                    <i class="fas fa-check-circle"></i>
                                    <span>Better Fleet Monitoring</span>
                                </div>

                                <div class="feature-item">
                                    <i class="fas fa-check-circle"></i>
                                    <span>Improved Maintenance Scheduling</span>
                                </div>

                                <div class="feature-item">
                                    <i class="fas fa-check-circle"></i>
                                    <span>Reduced Operational Delays</span>
                                </div>

                                <div class="feature-item">
                                    <i class="fas fa-check-circle"></i>
                                    <span>Better Passenger Experience</span>
                                </div>

                            </div>

                        </div>

                        <!-- Right Side intentionally left as a single column —
                             see feature list above, no image needed here. -->

                    </div>

                </div>

            </section>

            <!-- ===================================== -->
            <!-- CALL TO ACTION -->
            <!-- ===================================== -->

            <section class="cta-section">

                <div class="container">

                    <div class="row justify-content-center">

                        <div class="col-lg-10">

                            <div class="cta-box">

                                <h2>
                                    Ready to Manage Public Transport More Efficiently?
                                </h2>

                                <p>

                                    Login to access your dashboard and start managing
                                    routes, buses, drivers, maintenance and passenger
                                    services through one integrated platform.

                                </p>

                                <a href="Login.aspx" class="btn btn-primary cta-button">

                                    <i class="fas fa-sign-in-alt"></i>

                                    Login Now

                                </a>

                            </div>

                        </div>

                    </div>

                </div>

            </section>

</asp:Content>
