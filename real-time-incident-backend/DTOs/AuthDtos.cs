namespace IncidentApp.Api.DTOs;

public record RegisterRequest(string Email, string DisplayName, string Password);

public record LoginRequest(string Email, string Password);

public record RefreshRequest(string RefreshToken);

public record AuthResponse(string AccessToken, string RefreshToken, UserDto User);

public record UserDto(Guid Id, string Email, string DisplayName, string GlobalRole);
