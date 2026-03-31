using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using RestaurantPOS.API.DTOs;
using RestaurantPOS.API.Services;

namespace RestaurantPOS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    public AuthController(IAuthService authService) => _authService = authService;

    // POST /api/auth/login
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto dto)
    {
        try
        {
            var result = await _authService.LoginAsync(dto);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
    }

    // POST /api/auth/register
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register([FromBody] RegisterRequestDto dto)
    {
        try
        {
            var user = await _authService.RegisterAsync(dto);
            return Ok(new { message = "Đăng ký thành công", userId = user.UserID });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // GET /api/auth/me
    [HttpGet("me")]
    [Authorize]
    public IActionResult Me()
    {
        return Ok(new
        {
            userId   = User.FindFirst(ClaimTypes.NameIdentifier)?.Value,
            username = User.FindFirst(ClaimTypes.Name)?.Value,
            fullName = User.FindFirst(ClaimTypes.GivenName)?.Value,
            roleCode = User.FindFirst("RoleCode")?.Value,
            roleLabel = User.FindFirst(ClaimTypes.Role)?.Value,
        });
    }

    // POST /api/auth/change-password
    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto dto)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        try
        {
            await _authService.ChangePasswordAsync(userId, dto);
            return Ok(new { message = "Đổi mật khẩu thành công." });
        }
        catch (UnauthorizedAccessException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // GET /api/auth/staff
    [HttpGet("staff")]
    [Authorize(Roles = "Admin, Manager, Cashier")]
    public async Task<IActionResult> GetStaff()
    {
        var staff = await _authService.GetStaffAsync();
        return Ok(staff);
    }
}
