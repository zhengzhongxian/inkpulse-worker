namespace InkPulse.Worker.Features.Auth.Messages
{
    public record SendOtpEmailMessage(
        string Email,
        string Subject,
        string Name,
        string Otp,
        int ExpiryMinutes
    );
}
