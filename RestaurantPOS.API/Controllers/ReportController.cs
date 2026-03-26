using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantPOS.API.Services;

namespace RestaurantPOS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "ManagerUp")]
public class ReportController : ControllerBase
{
    private readonly IReportService _reportService;
    public ReportController(IReportService reportService) => _reportService = reportService;

    // GET /api/report/daily?date=2025-03-22
    [HttpGet("daily")]
    public async Task<IActionResult> GetDailyReport([FromQuery] DateTime? date = null)
    {
        var todayVn = DateTime.UtcNow.AddHours(7).Date;
        var report = await _reportService.GetDailyReportAsync(date ?? todayVn);
        return Ok(report);
    }
}
