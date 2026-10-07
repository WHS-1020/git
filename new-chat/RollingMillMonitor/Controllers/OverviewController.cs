using Microsoft.AspNetCore.Mvc;
using RollerMillMonitor.Api;
using RollerMillMonitor.Data;
using RollerMillMonitor.Services;

namespace RollerMillMonitor.Controllers;

[ApiController]
[Route("api/overview")]
public class OverviewController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly OverviewService _overview;

    public OverviewController(AppDbContext db, OverviewService overview)
    {
        _db = db;
        _overview = overview;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var dto = await _overview.BuildAsync(_db, HttpContext.RequestAborted);
        return Ok(ApiResult.Ok(dto));
    }
}
