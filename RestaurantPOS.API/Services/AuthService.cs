using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using RestaurantPOS.API.Data;
using RestaurantPOS.API.DTOs;
using RestaurantPOS.API.Models;

namespace RestaurantPOS.API.Services;

public interface IAuthService
{
    Task<LoginResponseDto> LoginAsync(LoginRequestDto dto);
    Task<User> RegisterAsync(RegisterRequestDto dto);
    Task<bool> ChangePasswordAsync(int userId, ChangePasswordDto dto);
    Task SeedAdminAsync();
    Task<IEnumerable<object>> GetStaffAsync();
}

public class AuthService : IAuthService
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;

    public AuthService(AppDbContext db, IConfiguration config)
    {
        _db = db; _config = config;
    }

    public async Task<LoginResponseDto> LoginAsync(LoginRequestDto dto)
    {
        var user = await _db.Users
            .FirstOrDefaultAsync(u =>
                u.Username.ToLower() == dto.Username.ToLower() && u.IsActive);

        if (user == null)
            throw new UnauthorizedAccessException("Tên đăng nhập hoặc mật khẩu không đúng.");

        if (!BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
            throw new UnauthorizedAccessException("Tên đăng nhập hoặc mật khẩu không đúng.");

        var token   = GenerateJwtToken(user);
        var expires = DateTime.UtcNow.AddHours(10);

        return new LoginResponseDto
        {
            UserID    = user.UserID,
            Token     = token,
            FullName  = user.FullName,
            Username  = user.Username,
            Role      = user.Role,
            RoleLabel = GetRoleLabel(user.Role),
            ExpiresAt = expires
        };
    }

    public async Task<User> RegisterAsync(RegisterRequestDto dto)
    {
        if (await _db.Users.AnyAsync(u => u.Username.ToLower() == dto.Username.ToLower()))
            throw new InvalidOperationException("Tên đăng nhập đã tồn tại.");

        var user = new User
        {
            Username = dto.Username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            FullName = dto.FullName,
            Role = dto.Role,
            IsActive = true
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return user;
    }

    public async Task<bool> ChangePasswordAsync(int userId, ChangePasswordDto dto)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return false;

        if (!BCrypt.Net.BCrypt.Verify(dto.OldPassword, user.PasswordHash))
            throw new UnauthorizedAccessException("Mật khẩu cũ không đúng.");

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword, workFactor: 11);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task SeedAdminAsync()
    {
        if (!await _db.Users.AnyAsync())
        {
            _db.Users.Add(
                new User { Username = "admin", PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123", 11), FullName = "Quản trị viên", Role = 3 }
            );
            await _db.SaveChangesAsync();
        }
    }

    public async Task<IEnumerable<object>> GetStaffAsync()
    {
        return await _db.Users
            .Where(u => u.IsActive)
            .OrderBy(u => u.Role)
            .Select(u => new
            {
                userId = u.UserID,
                fullName = u.FullName,
                roleLabel = GetRoleLabel(u.Role)
            })
            .ToListAsync();
    }

    private string GenerateJwtToken(User user)
    {
        var key   = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.UserID.ToString()),
            new Claim(ClaimTypes.Name,           user.Username),
            new Claim(ClaimTypes.GivenName,      user.FullName),
            new Claim(ClaimTypes.Role,           GetRoleLabel(user.Role)),
            new Claim("RoleCode",                user.Role.ToString()),
        };
        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"], audience: _config["Jwt:Audience"],
            claims: claims, expires: DateTime.UtcNow.AddHours(10),
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string GetRoleLabel(byte role) => role switch
    {
        3 => "Admin",
        2 => "Manager",
        1 => "Waiter",
        0 => "Cashier",
        _ => "Unknown"
    };
}
