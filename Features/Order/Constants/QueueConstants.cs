namespace InkPulse.Worker.Features.Order.Constants
{
    public static class QueueConstants
    {
        public const string CreateGhnOrder = "create-ghn-order-queue";
        public const string GhnStatusUpdate = "ghn-status-update-queue";
        public const string SendOrderConfirmationEmail = "send-order-confirmation-email-queue";
        public const string PayOsWebhook = "payos-webhook-queue";
        public const string CancelGhnOrder = "cancel-ghn-order-queue";
        public const string ReturnGhnOrder = "return-ghn-order-queue";
    }
}
