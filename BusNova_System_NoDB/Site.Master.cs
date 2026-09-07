using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace BusNova_System
{
    public partial class Site : System.Web.UI.MasterPage
    {
        protected void Page_Load(object sender, EventArgs e)
        {

        }

        /// <summary>
        /// Returns "nav-link active" when the given page name matches the
        /// current request, otherwise just "nav-link". Used so the nav bar
        /// highlights whichever page the visitor is actually on, instead of
        /// always highlighting Home.
        /// </summary>
        protected string GetNavClass(string pageFileName)
        {
            string currentPage = System.IO.Path.GetFileName(Request.Path);
            bool isCurrent = string.Equals(currentPage, pageFileName, StringComparison.OrdinalIgnoreCase);
            return isCurrent ? "nav-link active" : "nav-link";
        }

    }
}