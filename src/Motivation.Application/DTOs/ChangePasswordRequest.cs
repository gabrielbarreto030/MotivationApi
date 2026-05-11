namespace Motivation.Application.DTOs
{
    public record ChangePasswordRequest(string CurrentPassword, string NewPassword);
}
