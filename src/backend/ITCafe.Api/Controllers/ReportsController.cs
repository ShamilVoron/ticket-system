using ITCafe.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ITCafe.Api.Controllers;

/// <summary>Отчёты по ремонтным заявкам.</summary>
[Authorize(Roles = StaffRoles.All)]
[ApiController]
[Route("api/[controller]")]
public class ReportsController : ControllerBase
{
    private readonly AppDbContext _db;

    public ReportsController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet("repairs")]
    public async Task<ActionResult<object>> GetRepairs(
        [FromQuery] string? month = null,
        [FromQuery] string? from = null,
        [FromQuery] string? to = null,
        [FromQuery] string? clientName = null,
        [FromQuery] string? equipmentType = null,
        [FromQuery] string? status = null,
        [FromQuery] string? repairType = null)
    {
        var query = _db.Tickets.AsNoTracking().Where(t => t.IsRepair);

        if (!string.IsNullOrWhiteSpace(month) &&
            DateTime.TryParseExact(month.Trim() + "-01", "yyyy-MM-dd", null,
                System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal,
                out var monthStart))
        {
            var monthEnd = monthStart.AddMonths(1);
            query = query.Where(t => t.CreatedAt >= monthStart && t.CreatedAt < monthEnd);
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(from) &&
                DateTime.TryParse(from, null, System.Globalization.DateTimeStyles.RoundtripKind, out var fromDt))
            {
                if (fromDt.Kind == DateTimeKind.Unspecified)
                    fromDt = DateTime.SpecifyKind(fromDt, DateTimeKind.Utc);
                query = query.Where(t => t.CreatedAt >= fromDt.ToUniversalTime());
            }

            if (!string.IsNullOrWhiteSpace(to) &&
                DateTime.TryParse(to, null, System.Globalization.DateTimeStyles.RoundtripKind, out var toDt))
            {
                if (toDt.Kind == DateTimeKind.Unspecified)
                    toDt = DateTime.SpecifyKind(toDt, DateTimeKind.Utc);
                // Inclusive end-of-day if date-only
                if (toDt.TimeOfDay == TimeSpan.Zero)
                    toDt = toDt.AddDays(1);
                query = query.Where(t => t.CreatedAt < toDt.ToUniversalTime());
            }
        }

        if (!string.IsNullOrWhiteSpace(clientName))
        {
            var cn = clientName.Trim();
            query = query.Where(t => t.RepairClientName.Contains(cn));
        }

        if (!string.IsNullOrWhiteSpace(equipmentType))
        {
            var et = equipmentType.Trim();
            query = query.Where(t => t.RepairEquipmentType == et);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            var st = status.Trim();
            query = query.Where(t => t.Status == st);
        }

        if (!string.IsNullOrWhiteSpace(repairType))
        {
            var rt = repairType.Trim();
            query = query.Where(t => t.RepairType == rt);
        }

        var tickets = await query.OrderByDescending(t => t.CreatedAt).ToListAsync();

        var items = tickets.Select(t => new
        {
            ticketId = t.Id,
            createdAt = t.CreatedAt,
            status = t.Status,
            equipmentId = t.EquipmentId ?? 0,
            clientName = t.RepairClientName,
            equipmentType = t.RepairEquipmentType,
            equipmentName = t.RepairEquipmentName,
            serialNumber = t.RepairSerialNumber,
            fundStatus = t.RepairFundStatus,
            location = t.RepairLocation,
            faults = t.RepairFaults,
            notes = t.RepairNotes,
            repairType = t.RepairType,
            repairCost = t.RepairCost,
        }).ToList();

        static List<object> GroupSum(IEnumerable<(string Key, decimal Cost)> rows) =>
            rows.GroupBy(x => string.IsNullOrWhiteSpace(x.Key) ? "—" : x.Key)
                .Select(g => new { key = g.Key, count = g.Count(), sum = g.Sum(x => x.Cost) })
                .OrderByDescending(x => x.sum)
                .Select(x => (object)x)
                .ToList();

        var withCost = tickets.Select(t => (
            Client: t.RepairClientName,
            EqType: t.RepairEquipmentType,
            RType: t.RepairType,
            Status: t.Status,
            Cost: t.RepairCost ?? 0m
        )).ToList();

        var summary = new
        {
            totalCount = items.Count,
            totalCost = withCost.Sum(x => x.Cost),
            byClient = GroupSum(withCost.Select(x => (x.Client, x.Cost))),
            byEquipmentType = GroupSum(withCost.Select(x => (x.EqType, x.Cost))),
            byRepairType = GroupSum(withCost.Select(x => (x.RType, x.Cost))),
            byStatus = GroupSum(withCost.Select(x => (x.Status, x.Cost))),
        };

        return Ok(new { items, summary });
    }
}
