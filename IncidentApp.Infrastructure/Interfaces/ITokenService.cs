using IncidentApp.Domain.Entities;

namespace IncidentApp.Infrastructure.Interfaces;

public interface ITokenService
{
    string GenerateAccessToken(User user);
    string GenerateRefreshToken();
}
