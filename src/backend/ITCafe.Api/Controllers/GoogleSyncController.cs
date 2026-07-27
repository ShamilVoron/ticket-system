using System.Text.Json;
using ITCafe.Api.Data;
using ITCafe.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ITCafe.Api.Controllers;

/// <summary>Синхронизация компаний и объектов из Google/TSV (X-Sync-Key).</summary>
[ApiController]
[Route("api/sync")]
public class GoogleSyncController : ControllerBase
{
    private const string SyncApiKeySettingKey = "Sync:ApiKey";

    private readonly AppDbContext _db;
    private readonly IConfiguration _config;

    public GoogleSyncController(AppDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    [AllowAnonymous]
    [HttpPost("google/companies-objects")]
    public async Task<ActionResult<object>> SyncCompaniesObjects(
        [FromBody] JsonElement body,
        [FromHeader(Name = "X-Sync-Key")] string? syncKey)
    {
        if (!await ValidateSyncKeyAsync(syncKey))
            return Unauthorized(new { error = "Invalid or missing X-Sync-Key." });

        var (rows, source, dryRun) = ParseRequest(body);
        if (rows.Count == 0)
            return BadRequest(new { error = "No rows to sync." });

        var now = DateTime.UtcNow;
        var syncSource = string.IsNullOrWhiteSpace(source) ? "google_sync" : source.Trim();

        var companiesCreated = 0;
        var companiesUpdated = 0;
        var objectsCreated = 0;
        var objectsUpdated = 0;
        var errors = new List<string>();
        var preview = new List<object>();

        // Load existing for upsert lookups
        var allCompanies = await _db.Companies.ToListAsync();
        var allObjects = await _db.ServiceObjects.ToListAsync();

        foreach (var row in rows)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(row.CompanyName) && string.IsNullOrWhiteSpace(row.CompanyCode))
                {
                    errors.Add("Row skipped: companyName/companyCode empty.");
                    continue;
                }

                var companyCode = row.CompanyCode?.Trim();
                var companyName = row.CompanyName?.Trim() ?? string.Empty;

                Company? company = null;
                if (!string.IsNullOrEmpty(companyCode))
                {
                    company = allCompanies.FirstOrDefault(c =>
                        string.Equals(c.ExternalCode, companyCode, StringComparison.OrdinalIgnoreCase));
                }

                if (company == null && !string.IsNullOrEmpty(companyName))
                {
                    company = allCompanies.FirstOrDefault(c =>
                        string.Equals(c.Name, companyName, StringComparison.OrdinalIgnoreCase));
                }

                var companyAction = "unchanged";
                if (company == null)
                {
                    company = new Company
                    {
                        Name = string.IsNullOrEmpty(companyName) ? (companyCode ?? "Unknown") : companyName,
                        ExternalCode = companyCode,
                        IsActive = true,
                        SyncSource = syncSource,
                        LastSyncedAtUtc = now,
                    };
                    if (!dryRun)
                    {
                        _db.Companies.Add(company);
                        allCompanies.Add(company);
                    }
                    companiesCreated++;
                    companyAction = "create";
                }
                else
                {
                    var changed = false;
                    if (!string.IsNullOrEmpty(companyName) && company.Name != companyName)
                    {
                        company.Name = companyName;
                        changed = true;
                    }
                    if (!string.IsNullOrEmpty(companyCode) && company.ExternalCode != companyCode)
                    {
                        company.ExternalCode = companyCode;
                        changed = true;
                    }
                    company.LastSyncedAtUtc = now;
                    company.SyncSource = syncSource;
                    if (!company.IsActive)
                    {
                        company.IsActive = true;
                        changed = true;
                    }
                    if (changed)
                    {
                        companiesUpdated++;
                        companyAction = "update";
                    }
                }

                var objectAction = "skip";
                if (!string.IsNullOrWhiteSpace(row.ObjectName) || !string.IsNullOrWhiteSpace(row.ObjectCode))
                {
                    var objectCode = row.ObjectCode?.Trim();
                    var objectName = row.ObjectName?.Trim() ?? string.Empty;

                    ServiceObject? obj = null;
                    if (!string.IsNullOrEmpty(objectCode))
                    {
                        obj = allObjects.FirstOrDefault(o =>
                            string.Equals(o.ExternalCode, objectCode, StringComparison.OrdinalIgnoreCase));
                    }

                    if (obj == null && !string.IsNullOrEmpty(objectName) && company.Id != 0)
                    {
                        obj = allObjects.FirstOrDefault(o =>
                            o.ClientId == company.Id &&
                            string.Equals(o.Name, objectName, StringComparison.OrdinalIgnoreCase));
                    }

                    // dry-run create: company.Id may be 0 — still report create
                    if (obj == null)
                    {
                        obj = new ServiceObject
                        {
                            Name = string.IsNullOrEmpty(objectName) ? (objectCode ?? "Object") : objectName,
                            ExternalCode = objectCode,
                            Address = string.Empty,
                            LegalEntity = company.Name,
                            ClientId = company.Id == 0 ? null : company.Id,
                            MaintenanceStatus = row.MaintenanceStatus?.Trim() ?? string.Empty,
                            MaintenanceComment = row.MaintenanceComment?.Trim() ?? string.Empty,
                            DirectoriesOwner = row.DirectoriesOwner?.Trim() ?? string.Empty,
                            SysAdmin = row.SysAdmin?.Trim() ?? string.Empty,
                            ServerServices = row.ServerServices?.Trim() ?? string.Empty,
                            IsActive = true,
                            SyncSource = syncSource,
                            LastSyncedAtUtc = now,
                        };
                        if (!dryRun)
                        {
                            // Ensure company has Id before linking
                            if (company.Id == 0)
                                await _db.SaveChangesAsync();
                            obj.ClientId = company.Id;
                            _db.ServiceObjects.Add(obj);
                            allObjects.Add(obj);
                        }
                        objectsCreated++;
                        objectAction = "create";
                    }
                    else
                    {
                        if (!string.IsNullOrEmpty(objectName))
                            obj.Name = objectName;
                        if (!string.IsNullOrEmpty(objectCode))
                            obj.ExternalCode = objectCode;
                        if (company.Id != 0)
                            obj.ClientId = company.Id;
                        obj.LegalEntity = company.Name;
                        if (row.MaintenanceStatus != null)
                            obj.MaintenanceStatus = row.MaintenanceStatus.Trim();
                        if (row.MaintenanceComment != null)
                            obj.MaintenanceComment = row.MaintenanceComment.Trim();
                        if (row.DirectoriesOwner != null)
                            obj.DirectoriesOwner = row.DirectoriesOwner.Trim();
                        if (row.SysAdmin != null)
                            obj.SysAdmin = row.SysAdmin.Trim();
                        if (row.ServerServices != null)
                            obj.ServerServices = row.ServerServices.Trim();
                        obj.IsActive = true;
                        obj.SyncSource = syncSource;
                        obj.LastSyncedAtUtc = now;
                        objectsUpdated++;
                        objectAction = "update";
                    }
                }

                preview.Add(new
                {
                    companyName = company.Name,
                    companyCode = company.ExternalCode,
                    companyAction,
                    objectName = row.ObjectName,
                    objectCode = row.ObjectCode,
                    objectAction,
                });
            }
            catch (Exception ex)
            {
                errors.Add(ex.Message);
            }
        }

        if (!dryRun)
            await _db.SaveChangesAsync();

        return Ok(new
        {
            dryRun,
            source = syncSource,
            rowsProcessed = rows.Count,
            companiesCreated,
            companiesUpdated,
            objectsCreated,
            objectsUpdated,
            errors,
            preview = preview.Take(50),
        });
    }

    private async Task<bool> ValidateSyncKeyAsync(string? provided)
    {
        if (string.IsNullOrWhiteSpace(provided))
            return false;

        var configured = _config["Sync:ApiKey"]?.Trim();
        if (string.IsNullOrEmpty(configured))
        {
            var row = await _db.SystemSettings.AsNoTracking()
                .FirstOrDefaultAsync(s => s.Key == SyncApiKeySettingKey || s.Key == "SyncApiKey");
            configured = row?.Value?.Trim();
        }

        if (string.IsNullOrEmpty(configured))
            return false;

        return string.Equals(configured, provided.Trim(), StringComparison.Ordinal);
    }

    private static (List<SyncRow> Rows, string? Source, bool DryRun) ParseRequest(JsonElement body)
    {
        var dryRun = false;
        string? source = null;
        var rows = new List<SyncRow>();

        if (body.ValueKind == JsonValueKind.Array)
        {
            foreach (var el in body.EnumerateArray())
            {
                var row = ParseRow(el);
                if (row != null) rows.Add(row);
            }
            return (rows, source, dryRun);
        }

        if (body.ValueKind == JsonValueKind.Object)
        {
            if (body.TryGetProperty("dryRun", out var dr) &&
                (dr.ValueKind == JsonValueKind.True || dr.ValueKind == JsonValueKind.False))
                dryRun = dr.GetBoolean();

            if (body.TryGetProperty("source", out var src) && src.ValueKind == JsonValueKind.String)
                source = src.GetString();

            JsonElement rowsEl = default;
            if (body.TryGetProperty("rows", out rowsEl) || body.TryGetProperty("items", out rowsEl))
            {
                if (rowsEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var el in rowsEl.EnumerateArray())
                    {
                        var row = ParseRow(el);
                        if (row != null) rows.Add(row);
                    }
                }
            }
        }

        return (rows, source, dryRun);
    }

    private static SyncRow? ParseRow(JsonElement el)
    {
        if (el.ValueKind != JsonValueKind.Object) return null;

        static string? Str(JsonElement obj, params string[] names)
        {
            foreach (var n in names)
            {
                if (obj.TryGetProperty(n, out var p) && p.ValueKind == JsonValueKind.String)
                    return p.GetString();
            }
            return null;
        }

        return new SyncRow(
            Str(el, "companyName", "CompanyName"),
            Str(el, "companyCode", "CompanyCode"),
            Str(el, "objectName", "ObjectName"),
            Str(el, "objectCode", "ObjectCode"),
            Str(el, "maintenanceStatus", "MaintenanceStatus"),
            Str(el, "maintenanceComment", "MaintenanceComment"),
            Str(el, "directoriesOwner", "DirectoriesOwner"),
            Str(el, "sysAdmin", "SysAdmin"),
            Str(el, "serverServices", "ServerServices"));
    }

    private record SyncRow(
        string? CompanyName,
        string? CompanyCode,
        string? ObjectName,
        string? ObjectCode,
        string? MaintenanceStatus,
        string? MaintenanceComment,
        string? DirectoriesOwner,
        string? SysAdmin,
        string? ServerServices);
}
