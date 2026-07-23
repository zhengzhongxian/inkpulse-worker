namespace InkPulse.Worker.Features.Auth.Messages
{
    public record SendForgotPasswordEmailMessage(
        string Email,
        string Subject,
        string Name,
        string ResetLink,
        int ExpiryMinutes
    );
}
