using System;
using System.Collections.Generic;

namespace BusNova_System
{
    public partial class ViewAssignedRoutes : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack)
            {
                BindSampleSchedule();
            }
        }

        // NOTE: This build has no database connected yet, so the grid is
        // bound to static sample rows just to demonstrate the layout. Once
        // the data layer is added, this should query RouteAssignments for
        // the logged-in driver instead.
        private void BindSampleSchedule()
        {
            var sampleRows = new List<object>
            {
                new
                {
                    AssignmentId = 1,
                    Route = "Alice to East London",
                    Bus = "CA 123-456",
                    Date = "17 Aug 2026",
                    Departure = "18:10",
                    Arrival = "20:15",
                    Passengers = 0,
                    Status = "Scheduled"
                },
                new
                {
                    AssignmentId = 2,
                    Route = "Forte Town to Gqeberha",
                    Bus = "EL 789-012",
                    Date = "18 Aug 2026",
                    Departure = "07:30",
                    Arrival = "09:45",
                    Passengers = 32,
                    Status = "Completed"
                }
            };

            gvAssignedRoutes.DataSource = sampleRows;
            gvAssignedRoutes.DataBind();
        }
    }
}
