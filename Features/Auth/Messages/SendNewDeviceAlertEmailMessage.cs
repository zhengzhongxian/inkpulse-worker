namespace InkPulse.Worker.Features.Auth.Messages
{
    public record SendNewDeviceAlertEmailMessage(
        string Email,
        string DeviceName,
        string IpAddress
    );
}
