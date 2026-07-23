namespace InkPulse.Worker.Features.Auth.Constants
{
    public static class QueueConstants
    {
        public const string SendOtpEmail = "send-otp-email-queue";
        public const string SendChallengeEmail = "send-challenge-email-queue";
        public const string SendDeviceAlertEmail = "send-device-alert-email-queue";
        public const string SendForgotPasswordEmail = "send-forgot-password-email-queue";
    }
}
