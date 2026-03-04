namespace Motivation.Application.DTOs
{
    public record LoginResponse(System.Guid UserId, string Email, string Token);
}
