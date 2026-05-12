namespace Motivation.Application.DTOs
{
    public record ChangeEmailRequest(string CurrentPassword, string NewEmail);
}
