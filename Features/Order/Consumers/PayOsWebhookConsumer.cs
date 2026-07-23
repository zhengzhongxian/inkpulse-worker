using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Transactions;
using InkPulse.Worker.Features.Order.Messages;
using InkPulse.Worker.Infrastructure.Persistence;
using InkPulse.Worker.Infrastructure.Services.Security.Interfaces;
using InkPulse.Worker.Infrastructure.Services.Caching.Interfaces;
using InkPulse.Worker.Infrastructure.Helpers;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace InkPulse.Worker.Features.Order.Consumers
{
    public class PayOsWebhookConsumer : IConsumer<PayOsWebhookMessage>
    {
        private readonly IDapperRepository _dapperRepository;
        private readonly ILogger<PayOsWebhookConsumer> _logger;
        private readonly ICryptographyService _cryptographyService;
        private readonly ICacheService _cacheService;

        public PayOsWebhookConsumer(
            IDapperRepository dapperRepository, 
            ILogger<PayOsWebhookConsumer> logger,
            ICryptographyService cryptographyService,
            ICacheService cacheService)
        {
            _dapperRepository = dapperRepository;
            _logger = logger;
            _cryptographyService = cryptographyService;
            _cacheService = cacheService;
        }

        public async Task Consume(ConsumeContext<PayOsWebhookMessage> context)
        {
            var message = context.Message;
            _logger.LogInformation("Consuming PayOsWebhookMessage for Order Code: {OrderCode}, Code: {Code}, Success: {Success}", 
                message.OrderCode, message.Code, message.Success);

            // 1. Fetch Order and User details
            var selectSql = @"
                SELECT o.order_id AS Id, o.user_id AS UserId, o.receiver_name AS ReceiverName, 
                       o.recipient_phone AS RecipientPhone, o.street_address AS StreetAddress, 
                       w.ward_name AS WardName, d.district_name AS DistrictName, p.province_name AS ProvinceName,
                       u.email AS UserEmail, u.user_name AS UserName, o.order_fee AS OrderFee, 
                       o.shipping_fee AS ShippingFee, o.payment_status AS PaymentStatus, 
                       o.order_status AS OrderStatus, o.ghn_ward_code AS WardId, 
                       o.ghn_district_id AS DistrictId, o.ghn_province_id AS ProvinceId
                FROM orders o
                JOIN users u ON o.user_id = u.id
                JOIN ghn_wards w ON o.ghn_ward_code = w.ward_code
                JOIN ghn_districts d ON o.ghn_district_id = d.district_id
                JOIN ghn_provinces p ON o.ghn_province_id = p.province_id
                WHERE o.order_code = @orderCode";

            var order = await _dapperRepository.QueryFirstOrDefaultAsync<OrderQueryResult>(selectSql, new { orderCode = message.OrderCode });

            if (order == null)
            {
                _logger.LogWarning("Order not found in DB for PayOS payment. Order Code: {OrderCode}", message.OrderCode);
                return;
            }

            if (order.PaymentStatus != "PENDING")
            {
                _logger.LogInformation("Order: {OrderCode} is already processed. Payment Status in DB: {Status}", 
                    message.OrderCode, order.PaymentStatus);
                return;
            }

            // 2. Handle Payment Outcome
            if (message.Success)
            {
                _logger.LogInformation("Processing successful payment for Order: {OrderCode}", message.OrderCode);

                using (var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
                {
                    try
                    {
                        // Update Payment Transaction
                        await _dapperRepository.ExecuteAsync(
                            "UPDATE payment_transactions SET status = 'PAID', updated_at = NOW() WHERE order_code = @orderCode", 
                            new { orderCode = message.OrderCode });

                        // Update Order status
                        await _dapperRepository.ExecuteAsync(
                            "UPDATE orders SET payment_status = 'PAID', order_status = 'PROCESSING', updated_at = NOW() WHERE order_id = @orderId", 
                            new { orderId = order.Id });

                        // Insert Order Event (Payment Completed)
                        var insertPayEventSql = @"
                            INSERT INTO order_events (event_id, order_id, event_type, event_data, created_by, created_at, updated_at, is_deleted)
                            VALUES (gen_random_uuid(), @orderId, 'PAYMENT_COMPLETED', @eventData, @createdBy, NOW(), NOW(), false)";
                        await _dapperRepository.ExecuteAsync(insertPayEventSql, new { orderId = order.Id, eventData = JsonHelper.Serialize(message), createdBy = order.UserId });

                        // Insert Order Event (Order Approved)
                        var insertApproveEventSql = @"
                            INSERT INTO order_events (event_id, order_id, event_type, event_data, created_by, created_at, updated_at, is_deleted)
                            VALUES (gen_random_uuid(), @orderId, 'ORDER_APPROVED', @eventData, @createdBy, NOW(), NOW(), false)";
                        await _dapperRepository.ExecuteAsync(insertApproveEventSql, new { orderId = order.Id, eventData = JsonHelper.Serialize(message), createdBy = order.UserId });

                        // Insert Order Log
                        var insertLogSql = @"
                            INSERT INTO order_logs (log_id, order_code, from_status, to_status, changed_by, admin_note, user_note, is_deleted, created_at, updated_at, version) 
                            VALUES (gen_random_uuid(), @orderCode, 'PENDING_PAYMENT', 'PROCESSING', @changedBy, 'Thanh toán thành công qua PayOS (asynced)', 'Thanh toán thành công qua PayOS', false, NOW(), NOW(), 0)";
                        await _dapperRepository.ExecuteAsync(insertLogSql, new { orderCode = message.OrderCode, changedBy = order.UserId });

                        scope.Complete();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError("Failed to commit DB updates for successful payment of order: {OrderCode}. Error: {Msg}", message.OrderCode, ex.Message);
                        throw;
                    }
                }

                // 3. Query items from orders_detail to build SendOrderConfirmationEmailMessage
                var selectItemsSql = @"
                    SELECT od.quantity AS Quantity, CAST(od.original_price AS integer) AS Price, 
                           be.isbn AS Code, be.weight_gram AS Weight, be.width_cm AS Width, 
                           be.height_cm AS Height, be.length_cm AS Length,
                           b.title AS Name
                    FROM orders_detail od
                    JOIN book_editions be ON od.book_edition_id = be.id
                    JOIN books b ON be.book_id = b.id
                    WHERE od.order_id = @orderId";

                var dbItems = await _dapperRepository.QueryAsync<OrderItemInfo>(selectItemsSql, new { orderId = order.Id });
                var fullAddress = $"{order.StreetAddress}, {order.WardName}, {order.DistrictName}, {order.ProvinceName}";

                var emailItems = new List<OrderItemEmailInfo>();
                foreach (var item in dbItems)
                {
                    emailItems.Add(new OrderItemEmailInfo(item.Name, item.Quantity, item.Price));
                }

                var emailMessage = new SendOrderStatusEmailMessage(
                    _cryptographyService.DecryptAes(order.UserEmail),
                    order.ReceiverName,
                    message.OrderCode,
                    "PAYOS",
                    fullAddress,
                    emailItems,
                    0, // COD is 0 for prepaid order
                    $"[InkPulse] Xác nhận đơn hàng và thanh toán thành công #{message.OrderCode}",
                    "order-confirmation-template.html"
                );

                await context.Publish(emailMessage, context.CancellationToken);
                _logger.LogInformation("Successfully published SendOrderStatusEmailMessage for Order Code: {OrderCode} on payment success", message.OrderCode);
            }
            else
            {
                _logger.LogWarning("Payment failed or cancelled for Order: {OrderCode}. Initiating cancellation & restocking.", message.OrderCode);

                using (var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
                {
                    try
                    {
                        // Update Payment Transaction
                        await _dapperRepository.ExecuteAsync(
                            "UPDATE payment_transactions SET status = 'FAILED', updated_at = NOW() WHERE order_code = @orderCode", 
                            new { orderCode = message.OrderCode });

                        // Update Order status
                        await _dapperRepository.ExecuteAsync(
                            "UPDATE orders SET payment_status = 'FAILED', order_status = 'CANCELLED', updated_at = NOW() WHERE order_id = @orderId", 
                            new { orderId = order.Id });

                        // Insert Order Event (Payment Failed)
                        var insertPayFailedSql = @"
                            INSERT INTO order_events (event_id, order_id, event_type, event_data, created_by, created_at, updated_at, is_deleted)
                            VALUES (gen_random_uuid(), @orderId, 'PAYMENT_FAILED', @eventData, @createdBy, NOW(), NOW(), false)";
                        await _dapperRepository.ExecuteAsync(insertPayFailedSql, new { orderId = order.Id, eventData = JsonHelper.Serialize(message), createdBy = order.UserId });

                        // Insert Order Event (Order Cancelled)
                        var insertCancelEventSql = @"
                            INSERT INTO order_events (event_id, order_id, event_type, event_data, created_by, created_at, updated_at, is_deleted)
                            VALUES (gen_random_uuid(), @orderId, 'ORDER_CANCELLED', @eventData, @createdBy, NOW(), NOW(), false)";
                        await _dapperRepository.ExecuteAsync(insertCancelEventSql, new { orderId = order.Id, eventData = JsonHelper.Serialize(message), createdBy = order.UserId });

                        // Insert Order Log
                        var insertLogSql = @"
                            INSERT INTO order_logs (log_id, order_code, from_status, to_status, changed_by, admin_note, user_note, is_deleted, created_at, updated_at, version) 
                            VALUES (gen_random_uuid(), @orderCode, 'PENDING_PAYMENT', 'CANCELLED', @changedBy, 'Thanh toán PayOS thất bại hoặc bị hủy (asynced)', 'Đơn hàng đã bị hủy vì thanh toán không thành công', false, NOW(), NOW(), 0)";
                        await _dapperRepository.ExecuteAsync(insertLogSql, new { orderCode = message.OrderCode, changedBy = order.UserId });

                        // Restock editions
                        var selectItemsSql = "SELECT book_edition_id AS EditionId, quantity AS Qty FROM orders_detail WHERE order_id = @orderId";
                        var itemsToRestock = await _dapperRepository.QueryAsync<RestockItem>(selectItemsSql, new { orderId = order.Id });

                        foreach (var item in itemsToRestock)
                        {
                            await _dapperRepository.ExecuteAsync(
                                "UPDATE book_editions SET stock_quantity = stock_quantity + @qty WHERE id = @editionId", 
                                new { qty = item.Qty, editionId = item.EditionId });

                            // Insert stock transaction log
                            var insertStockTxSql = @"
                                INSERT INTO stock_transactions (transaction_id, edition_id, delta, type, reference_code, note, created_by, created_at, updated_at, is_deleted)
                                VALUES (gen_random_uuid(), @editionId, @qty, 'CANCEL_RESTORE', @refCode, 'Thanh toán PayOS thất bại hoặc bị hủy - Hoàn kho', @createdBy, NOW(), NOW(), false)";
                            await _dapperRepository.ExecuteAsync(insertStockTxSql, new {
                                editionId = item.EditionId,
                                qty = item.Qty,
                                refCode = message.OrderCode,
                                createdBy = order.UserId
                            });

                            // Evict cache
                            try
                            {
                                await _cacheService.RemoveAsync("book_edition:detail:" + item.EditionId);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Failed to evict Redis cache for edition: {EditionId}", item.EditionId);
                            }
                        }

                        scope.Complete();
                        _logger.LogInformation("Cancelled order: {OrderCode} and restocked editions.", message.OrderCode);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError("Failed to commit order cancellation DB updates for order: {OrderCode}. Error: {Msg}", message.OrderCode, ex.Message);
                        throw;
                    }
                }
            }
        }

        private class OrderQueryResult
        {
            public Guid Id { get; set; }
            public Guid UserId { get; set; }
            public string ReceiverName { get; set; } = "";
            public string RecipientPhone { get; set; } = "";
            public string StreetAddress { get; set; } = "";
            public string WardName { get; set; } = "";
            public string DistrictName { get; set; } = "";
            public string ProvinceName { get; set; } = "";
            public string UserEmail { get; set; } = "";
            public string UserName { get; set; } = "";
            public decimal OrderFee { get; set; }
            public decimal ShippingFee { get; set; }
            public string PaymentStatus { get; set; } = "";
            public string OrderStatus { get; set; } = "";
            public string WardId { get; set; } = "";
            public int DistrictId { get; set; }
            public int ProvinceId { get; set; }
        }

        private class RestockItem
        {
            public Guid EditionId { get; set; }
            public int Qty { get; set; }
        }
    }
}
