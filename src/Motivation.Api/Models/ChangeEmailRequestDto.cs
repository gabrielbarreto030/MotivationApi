namespace Motivation.Api.Models
{
    public class ChangeEmailRequestDto
    {
        public string CurrentPassword { get; set; } = string.Empty;
        public string NewEmail { get; set; } = string.Empty;
    }
}
