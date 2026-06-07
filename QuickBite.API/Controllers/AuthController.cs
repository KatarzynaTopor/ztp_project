using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using QuickBite.API.DTOs;
using QuickBite.API.Models;
using QuickBite.API.Services;

namespace QuickBite.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ITokenService _tokenService;
    private readonly IConfiguration _config;

    public AuthController(UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ITokenService tokenService,
        IConfiguration config)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _tokenService = tokenService;
        _config = config;
    }

    /// <summary>Rejestracja nowego użytkownika (Customer, Restaurant lub Courier).</summary>
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterDto dto)
    {
        var user = new ApplicationUser
        {
            UserName = dto.Email,
            Email = dto.Email,
            FullName = dto.FullName,
            Role = dto.Role
        };

        var result = await _userManager.CreateAsync(user, dto.Password);
        if (!result.Succeeded)
            return BadRequest(result.Errors);

        // Przypisz użytkownika do roli Identity, żeby działały atrybuty [Authorize(Roles = "...")].
        // Role są seedowane w Program.cs przy starcie aplikacji.
        var roleResult = await _userManager.AddToRoleAsync(user, dto.Role.ToString());
        if (!roleResult.Succeeded)
        {
            await _userManager.DeleteAsync(user);
            return BadRequest(roleResult.Errors);
        }

        return Ok(BuildResponse(user));
    }

    /// <summary>Logowanie — zwraca token JWT.</summary>
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDto dto)
    {
        var user = await _userManager.FindByEmailAsync(dto.Email);
        if (user is null)
            return Unauthorized("Nieprawidłowe dane logowania.");

        var result = await _signInManager.CheckPasswordSignInAsync(user, dto.Password, false);
        if (!result.Succeeded)
            return Unauthorized("Nieprawidłowe dane logowania.");

        return Ok(BuildResponse(user));
    }

    /// <summary>Returns the currently authenticated user's profile.</summary>
    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var user = await _userManager.FindByIdAsync(userId!);
        if (user is null) return Unauthorized();

        return Ok(new
        {
            user.Id,
            user.Email,
            user.FullName,
            Role = user.Role.ToString()
        });
    }

    private AuthResponseDto BuildResponse(ApplicationUser user)
    {
        var expiresInMinutes = int.Parse(_config["Jwt:ExpiresInMinutes"] ?? "120");
        var expiresAt = DateTime.UtcNow.AddMinutes(expiresInMinutes);
        return new AuthResponseDto
        {
            Token = _tokenService.GenerateToken(user),
            Email = user.Email!,
            FullName = user.FullName,
            Role = user.Role.ToString(),
            ExpiresAt = expiresAt
        };
    }
}
