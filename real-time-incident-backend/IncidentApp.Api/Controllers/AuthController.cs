using System.Security.Claims;
using BCrypt.Net;
using IncidentApp.Api.DTOs;
using IncidentApp.Domain.Entities;
using IncidentApp.Infrastructure.Data;
using IncidentApp.Infrastructure.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IncidentApp.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ITokenService _tokenService;

    public AuthController(AppDbContext db, ITokenService tokenService)
    {
        _db = db;
        _tokenService = tokenService;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Password) ||
            string.IsNullOrWhiteSpace(request.DisplayName))
            return BadRequest(new { error = "Email, display name, and password are required." });

        if (request.Password.Length < 8)
            return BadRequest(new { error = "Password must be at least 8 characters." });

        var emailLower = request.Email.Trim().ToLowerInvariant();
        if (await _db.Users.AnyAsync(u => u.Email == emailLower))
            return Conflict(new { error = "Email is already registered." });

        var user = new User
        {
            Email = emailLower,
            DisplayName = request.DisplayName.Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            GlobalRole = "responder"
        };

        _db.Users.Add(user);

        var refreshToken = CreateRefreshToken(user.Id);
        _db.RefreshTokens.Add(refreshToken);
        await _db.SaveChangesAsync();

        var accessToken = _tokenService.GenerateAccessToken(user);
        return Ok(new AuthResponse(accessToken, refreshToken.Token, ToDto(user)));
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { error = "Email and password are required." });

        var emailLower = request.Email.Trim().ToLowerInvariant();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == emailLower);

        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            return Unauthorized(new { error = "Invalid email or password." });

        var refreshToken = CreateRefreshToken(user.Id);
        _db.RefreshTokens.Add(refreshToken);
        await _db.SaveChangesAsync();

        var accessToken = _tokenService.GenerateAccessToken(user);
        return Ok(new AuthResponse(accessToken, refreshToken.Token, ToDto(user)));
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
            return BadRequest(new { error = "Refresh token is required." });

        var stored = await _db.RefreshTokens
            .Include(rt => rt.User)
            .FirstOrDefaultAsync(rt => rt.Token == request.RefreshToken);

        if (stored is null || !stored.IsActive)
            return Unauthorized(new { error = "Invalid or expired refresh token." });

        // Rotate: revoke old, issue new
        stored.RevokedAt = DateTime.UtcNow;
        var newRefresh = CreateRefreshToken(stored.UserId);
        _db.RefreshTokens.Add(newRefresh);
        await _db.SaveChangesAsync();

        var accessToken = _tokenService.GenerateAccessToken(stored.User);
        return Ok(new AuthResponse(accessToken, newRefresh.Token, ToDto(stored.User)));
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout([FromBody] RefreshRequest request)
    {
        var stored = await _db.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.Token == request.RefreshToken);

        if (stored is not null && stored.IsActive)
        {
            stored.RevokedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }

        return NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me()
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
                               ?? User.FindFirstValue("sub")!);
        var user = await _db.Users.FindAsync(userId);
        return user is null ? NotFound() : Ok(ToDto(user));
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private RefreshToken CreateRefreshToken(Guid userId) => new()
    {
        UserId = userId,
        Token = _tokenService.GenerateRefreshToken(),
        ExpiresAt = DateTime.UtcNow.AddDays(7)
    };

    private static UserDto ToDto(User u) => new(u.Id, u.Email, u.DisplayName, u.GlobalRole);
}
