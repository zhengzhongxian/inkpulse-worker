using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Transactions;
using InkPulse.Worker.Features.Order.Messages;
using InkPulse.Worker.Infrastructure.Services.Caching.Interfaces;
using InkPulse.Worker.Infrastructure.Services.Caching.Models;
using InkPulse.Worker.Infrastructure.Constants;
using InkPulse.Worker.Infrastructure.Persistence;
using InkPulse.Worker.Infrastructure.Services.Security.Interfaces;
using MassTransit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using InkPulse.Worker.Infrastructure.Helpers;

namespace InkPulse.Worker.Features.Order.Consumers
{
    public class GhnStatusUpdateConsumer : IConsumer<GhnStatusUpdateMessage>
    {
        private readonly IDapperRepository _dapperRepository;
        private readonly ILogger<GhnStatusUpdateConsumer> _logger;
        private readonly ICryptographyService _cryptographyService;
        private readonly ICacheService _cacheService;
        private readonly CacheProperties _cacheProperties;

        public GhnStatusUpdateConsumer(
            IDapperRepository dapperRepository, 
            ILogger<GhnStatusUpdateConsumer> logger,
            ICryptographyService cryptographyService,
            ICacheService cacheService,
            IOptions<CacheProperties> cachePropertiesOptions)
        {
            _dapperRepository = dapperRepository;
            _logger = logger;
            _cryptographyService = cryptographyService;
            _cacheService = cacheService;
            _cacheProperties = cachePropertiesOptions.Value;
        }

        public async Task Consume(ConsumeContext<GhnStatusUpdateMessage> context)
        {
            var message = context.Message;
            _logger.LogInformation("Consuming GhnStatusUpdateMessage. GHN Code: {GhnCode}, Status: {Status}, Type: {Type}", 
                message.OrderCode, message.Status, message.Type);

            // 1. Idempotency Check (Only check SUCCESS processed status)
            var checkSql = @"
                SELECT 1 FROM ghn_webhook_traces 
                WHERE order_code = @ghnCode AND ghn_status = @status AND processed_status = 'SUCCESS' 
                LIMIT 1";
            var alreadyProcessed = await _dapperRepository.QueryFirstOrDefaultAsync<int?>(checkSql, new { ghnCode = message.OrderCode, status = message.Status });
            
            if (alreadyProcessed.HasValue)
            {
                _logger.LogInformation("GHN status update already processed for Code: {GhnCode}, Status: {Status}. Skipping.", 
                    message.OrderCode, message.Status);
                return;
            }

            // 2. Insert PENDING trace to track audit trail
            var insertTraceSql = @"
                INSERT INTO ghn_webhook_traces (order_code, ghn_status, raw_payload, processed_status, created_at, updated_at)
                VALUES (@ghnCode, @status, @rawPayload, 'PENDING', NOW(), NOW())
                RETURNING id";
            
            long traceId = 0;
            try
            {
                traceId = await _dapperRepository.QueryFirstOrDefaultAsync<long>(insertTraceSql, new {
                    ghnCode = message.OrderCode,
                    status = message.Status,
                    rawPayload = message.RawPayload
                });
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to insert pending webhook trace for GHN Code: {GhnCode}. Error: {Error}", message.OrderCode, ex.Message);
                throw;
            }

            // 3. Process business logic inside transaction scope
            try
            {
                // Fetch the corresponding order
                var selectOrderSql = @"
                    SELECT o.order_id AS Id, o.order_code AS Code, o.order_status AS OrderStatus, 
                           o.payment_method AS PaymentMethod, o.payment_status AS PaymentStatus,
                           o.user_id AS UserId, o.order_fee AS OrderFee,
                           u.email AS UserEmail, o.receiver_name AS ReceiverName,
                           o.street_address AS StreetAddress, w.ward_name AS WardName,
                           d.district_name AS DistrictName, p.province_name AS ProvinceName,
                           o.shipping_fee + o.order_fee AS CodAmount
                    FROM orders o
                    INNER JOIN users u ON o.user_id = u.id
                    INNER JOIN ghn_wards w ON o.ghn_ward_code = w.ward_code
                    INNER JOIN ghn_districts d ON o.ghn_district_id = d.district_id
                    INNER JOIN ghn_provinces p ON d.province_id = p.province_id
                    WHERE o.ghn_order_code = @ghnCode";
                
                var order = await _dapperRepository.QueryFirstOrDefaultAsync<OrderQueryResult>(selectOrderSql, new { ghnCode = message.OrderCode });
                if (order == null)
                {
                    throw new Exception($"Order with GHN Code '{message.OrderCode}' not found.");
                }

                // Map GHN status to InkPulse OrderStatus
                string newStatusStr = order.OrderStatus;
                string adminNote = "";
                string userNote = "";
                bool isStatusChanged = false;

                string ghnStatusLower = message.Status.Trim().ToLower();

                switch (ghnStatusLower)
                {
                    case "ready_to_pick":
                        // ready_to_pick matches PROCESSING. Order is already in PROCESSING when packed, so keep it.
                        newStatusStr = "PROCESSING";
                        adminNote = "GHN: Sẵn sàng lấy hàng (ready_to_pick)";
                        userNote = "Đơn hàng đã được bàn giao và sẵn sàng vận chuyển";
                        isStatusChanged = true; // Still write log entry
                        break;

                    case "delivering":
                        newStatusStr = "SHIPPED";
                        adminNote = "GHN: Đang giao hàng (delivering)";
                        userNote = "Đơn hàng của bạn đang được shipper giao đến";
                        isStatusChanged = true;
                        break;

                    case "delivered":
                        newStatusStr = "DELIVERED";
                        adminNote = "GHN: Giao hàng thành công (delivered)";
                        userNote = "Đơn hàng đã được giao thành công";
                        isStatusChanged = true;
                        break;

                    case "cancel":
                    case "return":
                        newStatusStr = "CANCELLED";
                        adminNote = $"GHN: Đơn hàng bị hủy hoặc chuyển hoàn ({ghnStatusLower})";
                        userNote = "Đơn hàng của bạn đã bị hủy hoặc hoàn trả về kho";
                        isStatusChanged = true;
                        break;
                }

                bool shouldSendEmail = isStatusChanged && order.OrderStatus != newStatusStr &&
                                      (newStatusStr == "SHIPPED" || newStatusStr == "DELIVERED" || newStatusStr == "CANCELLED");

                using (var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
                {
                    if (isStatusChanged && order.OrderStatus != newStatusStr)
                    {
                        // Update order status
                        var updateOrderSql = "UPDATE orders SET order_status = @newStatus, updated_at = NOW() WHERE order_id = @orderId";
                        await _dapperRepository.ExecuteAsync(updateOrderSql, new { newStatus = newStatusStr, orderId = order.Id });
                    }

                    // Always insert order event when isStatusChanged (including ready_to_pick → SHIPPING_UPDATED)
                    if (isStatusChanged)
                    {
                        string eventType = newStatusStr switch
                        {
                            "PROCESSING" => "SHIPPING_UPDATED",
                            "SHIPPED" => "ORDER_SHIPPED",
                            "DELIVERED" => "ORDER_DELIVERED",
                            "CANCELLED" => "ORDER_CANCELLED",
                            _ => null
                        };

                        if (eventType != null)
                        {
                            var insertEventSql = @"
                                INSERT INTO order_events (event_id, order_id, event_type, event_data, created_by, created_at, updated_at, is_deleted)
                                VALUES (gen_random_uuid(), @orderId, @eventType, @eventData::jsonb, '00000000-0000-0000-0000-000000000000', NOW(), NOW(), false)";
                            
                            await _dapperRepository.ExecuteAsync(insertEventSql, new {
                                orderId = order.Id,
                                eventType = eventType,
                                eventData = JsonHelper.Serialize(message)
                            });
                        }
                    }

                    // Always insert order log when isStatusChanged is true (e.g. ready_to_pick keeps PROCESSING but still logs)
                    if (isStatusChanged)
                    {
                        var insertLogSql = @"
                            INSERT INTO order_logs (log_id, order_code, from_status, to_status, changed_by, admin_note, user_note, is_deleted, created_at, updated_at, version) 
                            VALUES (gen_random_uuid(), @orderCode, @fromStatus, @toStatus, @changedBy, @adminNote, @userNote, false, NOW(), NOW(), 0)";
                        
                        await _dapperRepository.ExecuteAsync(insertLogSql, new {
                            orderCode = order.Code,
                            fromStatus = order.OrderStatus,
                            toStatus = newStatusStr,
                            changedBy = Guid.Empty, // System / Webhook
                            adminNote = adminNote,
                            userNote = userNote
                        });

                        // Special logic for DELIVERED: if COD, update payment status to PAID
                        if (newStatusStr == "DELIVERED" && order.PaymentMethod == "COD" && order.PaymentStatus != "PAID")
                        {
                            // Update order payment status
                            await _dapperRepository.ExecuteAsync(
                                "UPDATE orders SET payment_status = 'PAID', updated_at = NOW() WHERE order_id = @orderId", 
                                new { orderId = order.Id });

                            // Update payment transaction
                            await _dapperRepository.ExecuteAsync(
                                "UPDATE payment_transactions SET status = 'PAID', updated_at = NOW() WHERE order_code = @orderCode", 
                                new { orderCode = order.Code });

                            // Insert Payment Completed Event
                            var insertPayEventSql = @"
                                INSERT INTO order_events (event_id, order_id, event_type, event_data, created_by, created_at, updated_at, is_deleted)
                                VALUES (gen_random_uuid(), @orderId, 'PAYMENT_COMPLETED', @eventData::jsonb, '00000000-0000-0000-0000-000000000000', NOW(), NOW(), false)";
                            await _dapperRepository.ExecuteAsync(insertPayEventSql, new { orderId = order.Id, eventData = JsonHelper.Serialize(message) });

                            _logger.LogInformation("COD Order {OrderCode} marked as PAID upon GHN delivery confirmation", order.Code);
                        }

                        // Award bonus coins on order DELIVERED
                        if (newStatusStr == "DELIVERED")
                        {
                            var bonusCoinsSql = $"SELECT setting_value FROM system_settings WHERE system_setting_id = '{SystemSettingConstant.BonusCoins.Id}'";
                            var bonusCoinsStr = await _dapperRepository.QueryFirstOrDefaultAsync<string>(bonusCoinsSql);
                            if (!int.TryParse(bonusCoinsStr, out var bonusCoins))
                            {
                                bonusCoins = 10; // Default fallback if not configured
                            }

                            var coinExistsSql = "SELECT 1 FROM coin_transactions WHERE user_id = @userId AND reason LIKE @reasonMatch LIMIT 1";
                            var reasonMatch = $"%Cộng xu thưởng cho đơn hàng {order.Code}%";
                            var coinExists = await _dapperRepository.QueryFirstOrDefaultAsync<int?>(coinExistsSql, new { userId = order.UserId, reasonMatch });
                            
                            if (!coinExists.HasValue)
                            {
                                var orderFeeInt = (int)order.OrderFee;
                                var coinsAmount = (orderFeeInt / 1000) * bonusCoins;
                                if (coinsAmount > 0)
                                {
                                    var insertCoinSql = @"
                                        INSERT INTO coin_transactions (transaction_id, user_id, amount, type, reason, is_deleted, created_at, updated_at, version)
                                        VALUES (gen_random_uuid(), @userId, @amount, 'EARNED', @reason, false, NOW(), NOW(), 0)";

                                    var reasonText = $"Cộng xu thưởng cho đơn hàng {order.Code}";
                                    await _dapperRepository.ExecuteAsync(insertCoinSql, new {
                                        userId = order.UserId,
                                        amount = coinsAmount,
                                        reason = reasonText
                                    });

                                    // Push pending delta to Redis hash
                                    var cacheKey = _cacheProperties.BuildKey(KeyConstant.CacheSections.CoinPendingDeltas, "");
                                    await _cacheService.HashIncrementAsync(cacheKey, order.UserId.ToString(), coinsAmount);

                                    _logger.LogInformation("Rewarded {Coins} coins to user {UserId} for order {OrderCode} and queued delta update in Redis", coinsAmount, order.UserId, order.Code);
                                }
                            }
                        }

                        // Special logic for CANCEL/RETURN: restock book editions
                        if (newStatusStr == "CANCELLED")
                        {
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
                                    VALUES (gen_random_uuid(), @editionId, @qty, 'CANCEL_RESTORE', @refCode, @note, '00000000-0000-0000-0000-000000000000', NOW(), NOW(), false)";
                                
                                await _dapperRepository.ExecuteAsync(insertStockTxSql, new {
                                    editionId = item.EditionId,
                                    qty = item.Qty,
                                    refCode = order.Code,
                                    note = adminNote
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
                            _logger.LogInformation("Restocked editions and evicted cache for cancelled order {OrderCode}", order.Code);
                        }
                    }

                    // Complete core transaction
                    scope.Complete();
                }

                // 4. Send Email notifications outside the transaction scope
                if (shouldSendEmail)
                {
                    try
                    {
                        var selectDetailItemsSql = @"
                            SELECT od.quantity AS Quantity, CAST(od.original_price AS integer) AS Price, 
                                   b.title AS Name
                            FROM orders_detail od
                            JOIN book_editions be ON od.book_edition_id = be.id
                            JOIN books b ON be.book_id = b.id
                            WHERE od.order_id = @orderId";
                        var dbItems = await _dapperRepository.QueryAsync<OrderItemInfo>(selectDetailItemsSql, new { orderId = order.Id });
                        var emailItems = new List<OrderItemEmailInfo>();
                        foreach (var item in dbItems)
                        {
                            emailItems.Add(new OrderItemEmailInfo(item.Name, item.Quantity, item.Price));
                        }

                        var fullAddress = $"{order.StreetAddress}, {order.WardName}, {order.DistrictName}, {order.ProvinceName}";
                        var plainEmail = _cryptographyService.DecryptAes(order.UserEmail);

                        string templateName = "";
                        string subject = "";

                        if (newStatusStr == "SHIPPED")
                        {
                            templateName = "order-shipped-template.html";
                            subject = $"[InkPulse] Đơn hàng #{order.Code} đang được giao";
                        }
                        else if (newStatusStr == "DELIVERED")
                        {
                            templateName = "order-delivered-template.html";
                            subject = $"[InkPulse] Đơn hàng #{order.Code} giao thành công. Cảm ơn bạn!";
                        }
                        else if (newStatusStr == "CANCELLED")
                        {
                            templateName = "order-cancelled-template.html";
                            subject = $"[InkPulse] Thông báo hủy đơn hàng #{order.Code}";
                        }

                        if (!string.IsNullOrEmpty(templateName))
                        {
                            var emailMessage = new SendOrderStatusEmailMessage(
                                plainEmail,
                                order.ReceiverName,
                                order.Code,
                                order.PaymentMethod,
                                fullAddress,
                                emailItems,
                                order.CodAmount,
                                subject,
                                templateName
                            );
                            await context.Publish(emailMessage, context.CancellationToken);
                            _logger.LogInformation("Published SendOrderStatusEmailMessage for Order Code: {OrderCode}, Status: {Status}", order.Code, newStatusStr);
                        }
                    }
                    catch (Exception mailEx)
                    {
                        _logger.LogError("Failed to send status update email for Order Code: {OrderCode}. Error: {Error}", order.Code, mailEx.Message);
                    }
                }

                // 5. Mark trace as SUCCESS
                var updateSuccessTraceSql = "UPDATE ghn_webhook_traces SET processed_status = 'SUCCESS', updated_at = NOW() WHERE id = @id";
                await _dapperRepository.ExecuteAsync(updateSuccessTraceSql, new { id = traceId });

                _logger.LogInformation("Successfully processed GHN Webhook trace. GHN Code: {GhnCode}, Status: {Status}", 
                    message.OrderCode, message.Status);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error handling GHN status update for code {GhnCode}: {Error}. Rolling back.", message.OrderCode, ex.Message);

                // 6. Mark trace as FAILED (done outside/after rollback of core transaction)
                try
                {
                    var updateFailedTraceSql = "UPDATE ghn_webhook_traces SET processed_status = 'FAILED', error_message = @error, updated_at = NOW() WHERE id = @id";
                    await _dapperRepository.ExecuteAsync(updateFailedTraceSql, new { error = ex.Message, id = traceId });
                }
                catch (Exception traceEx)
                {
                    _logger.LogError("Failed to update trace to FAILED for trace {TraceId}. Error: {Error}", traceId, traceEx.Message);
                }

                throw; // Throw to trigger MassTransit redelivery / fallback
            }
        }

        private class OrderQueryResult
        {
            public Guid Id { get; set; }
            public string Code { get; set; } = "";
            public string OrderStatus { get; set; } = "";
            public string PaymentMethod { get; set; } = "";
            public string PaymentStatus { get; set; } = "";
            public Guid UserId { get; set; }
            public decimal OrderFee { get; set; }
            public string UserEmail { get; set; } = "";
            public string ReceiverName { get; set; } = "";
            public string StreetAddress { get; set; } = "";
            public string WardName { get; set; } = "";
            public string DistrictName { get; set; } = "";
            public string ProvinceName { get; set; } = "";
            public int CodAmount { get; set; }
        }

        private class OrderItemInfo
        {
            public string Name { get; set; } = "";
            public int Quantity { get; set; }
            public int Price { get; set; }
        }

        private class RestockItem
        {
            public Guid EditionId { get; set; }
            public int Qty { get; set; }
        }
    }
}
