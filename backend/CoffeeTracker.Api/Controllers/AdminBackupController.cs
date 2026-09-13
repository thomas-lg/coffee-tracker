using CoffeeTracker.Application.Dtos;
using CoffeeTracker.Application.Ports.Driving;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoffeeTracker.Api.Controllers;

[ApiController]
[Route("api/admin/backup")]
// Administrator-only on both sides: an export carries every account's reviews, and a
// restore discards what is there.
[Authorize(Policy = AuthorizationPolicies.Admin)]
public class AdminBackupController(IBackupService backup) : ControllerBase
{
    /// <summary>Downloads the whole catalog as a JSON file.</summary>
    [HttpGet]
    public async Task<ActionResult<BackupDto>> Export(CancellationToken ct)
    {
        var export = await backup.ExportAsync(ct);

        // Named with the date so a folder of these stays sortable, and sent as an
        // attachment so a browser saves it instead of rendering it.
        var fileName = $"coffee-tracker-{export.ExportedAt:yyyy-MM-dd}.json";
        Response.Headers.ContentDisposition = $"attachment; filename=\"{fileName}\"";
        return Ok(export);
    }

    /// <summary>
    /// Replaces the catalog with an uploaded backup. Destructive, and deliberately so:
    /// the screen confirms before calling this.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ImportResultDto>> Import([FromBody] BackupDto? backupFile, CancellationToken ct)
    {
        var outcome = await backup.ImportAsync(backupFile, ct);

        return outcome.Status switch
        {
            ImportStatus.Restored => Ok(outcome.Result),
            // Both refusals are the client's file being wrong, and the reason is the
            // useful part — an administrator can act on "that is version 2", not on 400.
            ImportStatus.UnsupportedFormat or ImportStatus.Invalid =>
                Problem(statusCode: StatusCodes.Status400BadRequest, detail: outcome.Reason),
            _ => throw new InvalidOperationException($"Unexpected import status: {outcome.Status}"),
        };
    }
}
