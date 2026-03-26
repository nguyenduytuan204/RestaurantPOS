using System.ComponentModel.DataAnnotations;

namespace RestaurantPOS.API.DTOs;

public class LoginRequestDto
{
    [Required] public string Username { get; set; } = string.Empty;
    [Required] public string Password { get; set; } = string.Empty;
}

public class LoginResponseDto
{
    public string Token     { get; set; } = string.Empty;
    public string FullName  { get; set; } = string.Empty;
    public string Username  { get; set; } = string.Empty;
    public byte   Role      { get; set; }
    public string RoleLabel { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
}

public class ChangePasswordDto
{
    [Required] public string OldPassword { get; set; } = string.Empty;
    [Required, MinLength(6)] public string NewPassword { get; set; } = string.Empty;
}

public class RegisterRequestDto
{
    [Required] public string Username { get; set; } = string.Empty;
    [Required, MinLength(6)] public string Password { get; set; } = string.Empty;
    [Required] public string FullName { get; set; } = string.Empty;
    [Required] public byte Role { get; set; } = 1; // Default to Waiter
}
