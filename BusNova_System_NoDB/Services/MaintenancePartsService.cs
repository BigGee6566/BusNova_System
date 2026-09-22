using System;
using System.Collections.Generic;
using BusNova_System.DataAccess;
using BusNova_System.Helpers;

namespace BusNova_System.Services
{
    /// <summary>One part fitted during one job.</summary>
    public class MaintenancePart
    {
        public int MaintenancePartId { get; set; }
        public int MaintenanceLogId { get; set; }
        public string PartName { get; set; }
        public string PartNumber { get; set; }
        public string Supplier { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitCost { get; set; }
        public decimal LineTotal { get; set; }
    }

    /// <summary>What a supplier has cost over a period.</summary>
    public class SupplierSpendRow
    {
        public string Supplier { get; set; }
        public int Lines { get; set; }
        public decimal Total { get; set; }
    }

    /// <summary>
    /// WP-6. Itemised parts.
    ///
    /// MaintenanceLog.PartsUsed is free text and stays that way - existing
    /// records have notes in it, and inventing structured rows from
    /// "brake pads + fluid" would be making data up. New work records parts
    /// as rows, which is what makes "what did we spend on brake pads this
    /// year" and "which supplier do we use most" answerable at all.
    /// </summary>
    public static class MaintenancePartsService
    {
        public static List<MaintenancePart> GetForLog(int maintenanceLogId)
        {
            const string sql = @"
SELECT MaintenancePartID, MaintenanceLogID, PartName, PartNumber, Supplier,
       Quantity, UnitCost, LineTotal
FROM dbo.MaintenancePart
WHERE MaintenanceLogID = @id
ORDER BY MaintenancePartID;";

            return Db.Query(sql, r => new MaintenancePart
            {
                MaintenancePartId = Db.GetInt(r, "MaintenancePartID"),
                MaintenanceLogId = Db.GetInt(r, "MaintenanceLogID"),
                PartName = Db.GetString(r, "PartName"),
                PartNumber = Db.GetString(r, "PartNumber"),
                Supplier = Db.GetString(r, "Supplier"),
                Quantity = Db.GetDecimal(r, "Quantity"),
                UnitCost = Db.GetDecimal(r, "UnitCost"),
                LineTotal = Db.GetDecimal(r, "LineTotal")
            }, Db.P("@id", maintenanceLogId));
        }

        public static OperationResult Add(int maintenanceLogId, string partName, string partNumber,
                                          string supplier, decimal quantity, decimal unitCost)
        {
            partName = (partName ?? string.Empty).Trim();

            if (partName.Length == 0)
                return OperationResult.Fail("Name the part.");

            if (quantity <= 0)
                return OperationResult.Fail("Quantity must be greater than zero.");

            if (unitCost < 0)
                return OperationResult.Fail("Unit cost cannot be negative.");

            const string sql = @"
INSERT INTO dbo.MaintenancePart
    (MaintenanceLogID, PartName, PartNumber, Supplier, Quantity, UnitCost, CreatedAt)
VALUES
    (@logId, @name, @number, @supplier, @qty, @cost, @now);
SELECT CAST(SCOPE_IDENTITY() AS INT);";

            var id = Convert.ToInt32(Db.Scalar(sql,
                Db.P("@logId", maintenanceLogId),
                Db.P("@name", partName),
                Db.P("@number", Blank(partNumber)),
                Db.P("@supplier", Blank(supplier)),
                Db.P("@qty", quantity),
                Db.P("@cost", unitCost),
                Db.P("@now", AppClock.Now())));

            AuditService.Write("MaintenancePartAdded", "MaintenancePart", id.ToString(),
                partName + " x" + quantity);

            return OperationResult.Ok(id);
        }

        public static bool Remove(int maintenancePartId)
        {
            var rows = Db.Execute(
                "DELETE FROM dbo.MaintenancePart WHERE MaintenancePartID = @id;",
                Db.P("@id", maintenancePartId));

            if (rows > 0)
                AuditService.Write("MaintenancePartRemoved", "MaintenancePart", maintenancePartId.ToString());

            return rows > 0;
        }

        /// <summary>Total parts cost for one job, for the close-out screen.</summary>
        public static decimal TotalForLog(int maintenanceLogId)
        {
            var value = Db.Scalar(
                "SELECT ISNULL(SUM(LineTotal), 0) FROM dbo.MaintenancePart WHERE MaintenanceLogID = @id;",
                Db.P("@id", maintenanceLogId));

            return value == null || value == DBNull.Value ? 0m : Convert.ToDecimal(value);
        }

        /// <summary>Spend by supplier over a window, biggest first.</summary>
        public static List<SupplierSpendRow> GetSupplierSpend(DateTime from, DateTime to)
        {
            const string sql = @"
SELECT ISNULL(p.Supplier, '(not recorded)') AS Supplier,
       COUNT(*) AS Lines,
       SUM(p.LineTotal) AS Total
FROM dbo.MaintenancePart p
INNER JOIN dbo.MaintenanceLog m ON m.MaintenanceLogID = p.MaintenanceLogID
WHERE CAST(p.CreatedAt AS DATE) BETWEEN @from AND @to
GROUP BY ISNULL(p.Supplier, '(not recorded)')
ORDER BY SUM(p.LineTotal) DESC;";

            return Db.Query(sql, r => new SupplierSpendRow
            {
                Supplier = Db.GetString(r, "Supplier"),
                Lines = Db.GetInt(r, "Lines"),
                Total = Db.GetDecimal(r, "Total")
            }, Db.P("@from", from.Date), Db.P("@to", to.Date));
        }

        private static object Blank(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : (object)value.Trim();
        }
    }
}
