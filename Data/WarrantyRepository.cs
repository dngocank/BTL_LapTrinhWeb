using Microsoft.Data.SqlClient;
using BTL_PTTKYC.Models;
using System.Data;

namespace BTL_PTTKYC.Data
{
    public class WarrantyRepository
    {
        private readonly string _connectionString;

        public WarrantyRepository(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")!;
        }

        public TechnicianDashboardViewModel GetDashboardStats()
        {
            var vm = new TechnicianDashboardViewModel();
            using var conn = new SqlConnection(_connectionString);
            conn.Open();

            // Count by status
            var sqlCounts = @"
                SELECT 
                    COUNT(*) AS total,
                    SUM(CASE WHEN status = 'Received' THEN 1 ELSE 0 END) AS received,
                    SUM(CASE WHEN status = 'Diagnosing' THEN 1 ELSE 0 END) AS diagnosing,
                    SUM(CASE WHEN status = 'Processing' THEN 1 ELSE 0 END) AS processing,
                    SUM(CASE WHEN status = 'WaitingParts' THEN 1 ELSE 0 END) AS waiting_parts,
                    SUM(CASE WHEN status = 'Completed' THEN 1 ELSE 0 END) AS completed,
                    SUM(CASE WHEN status = 'Delivered' THEN 1 ELSE 0 END) AS delivered,
                    SUM(CASE WHEN status = 'Cancelled' THEN 1 ELSE 0 END) AS cancelled,
                    ISNULL(SUM(CASE WHEN status = 'Delivered' THEN repair_cost ELSE 0 END), 0) AS total_revenue
                FROM Warranty_Ticket";

            using (var cmd = new SqlCommand(sqlCounts, conn))
            using (var r = cmd.ExecuteReader())
            {
                if (r.Read())
                {
                    vm.TotalTickets = Convert.ToInt32(r["total"]);
                    vm.ReceivedCount = Convert.ToInt32(r["received"]);
                    vm.DiagnosingCount = Convert.ToInt32(r["diagnosing"]);
                    vm.ProcessingCount = Convert.ToInt32(r["processing"]);
                    vm.WaitingPartsCount = Convert.ToInt32(r["waiting_parts"]);
                    vm.CompletedCount = Convert.ToInt32(r["completed"]);
                    vm.DeliveredCount = Convert.ToInt32(r["delivered"]);
                    vm.CancelledCount = Convert.ToInt32(r["cancelled"]);
                    vm.TotalRepairRevenue = Convert.ToDecimal(r["total_revenue"]);
                }
            }

            // Total active warranty cards
            using (var cmd = new SqlCommand("SELECT COUNT(*) FROM Warranty WHERE warranty_status = 'Active' AND warranty_end >= CAST(GETDATE() AS DATE)", conn))
            {
                vm.TotalWarrantiesActive = Convert.ToInt32(cmd.ExecuteScalar() ?? 0);
            }

            // Pending tickets list (Received, Diagnosing, Processing, WaitingParts)
            vm.PendingTickets = GetTicketsInternal(conn, "PENDING_ACTIVE", null, 1, 10, out _);

            // Recent completed tickets
            vm.RecentCompletedTickets = GetTicketsInternal(conn, "Completed", null, 1, 5, out _);

            return vm;
        }

        public TechnicianWorkbenchViewModel GetWorkbenchData(int? currentTechId)
        {
            var vm = new TechnicianWorkbenchViewModel
            {
                Stats = GetDashboardStats(),
                Technicians = GetAllTechnicians(),
                CurrentTechnicianId = currentTechId
            };

            if (currentTechId.HasValue)
            {
                var tech = vm.Technicians.FirstOrDefault(t => t.EmployeeId == currentTechId.Value);
                vm.CurrentTechnicianName = tech?.EmployeeName;
            }

            using var conn = new SqlConnection(_connectionString);
            conn.Open();

            // Lấy tất cả các phiếu gần đây
            var allTickets = GetTicketsInternal(conn, null, null, 1, 150, out _);

            vm.AllActiveTickets = allTickets.Where(t => t.Status != "Delivered" && t.Status != "Cancelled").ToList();
            vm.ReceivedTickets = allTickets.Where(t => t.Status == "Received").ToList();
            vm.ProcessingTickets = allTickets.Where(t => t.Status == "Diagnosing" || t.Status == "Processing" || t.Status == "WaitingParts").ToList();
            vm.CompletedTickets = allTickets.Where(t => t.Status == "Completed").ToList();
            vm.UnpaidFeeTickets = allTickets.Where(t => t.RepairCost > 0 && t.PaymentStatus != "Paid").ToList();
            vm.DeliveredTickets = allTickets.Where(t => t.Status == "Delivered").OrderByDescending(t => t.CompletedDate ?? t.ReceiveDate).Take(15).ToList();

            return vm;
        }

        public List<WarrantyTicketModel> GetTickets(string? status, string? keyword, int page, int pageSize, out int totalCount)
        {
            using var conn = new SqlConnection(_connectionString);
            conn.Open();
            return GetTicketsInternal(conn, status, keyword, page, pageSize, out totalCount);
        }

        private List<WarrantyTicketModel> GetTicketsInternal(SqlConnection conn, string? status, string? keyword, int page, int pageSize, out int totalCount)
        {
            var list = new List<WarrantyTicketModel>();
            var whereClauses = new List<string>();
            var parameters = new List<SqlParameter>();

            if (!string.IsNullOrWhiteSpace(status))
            {
                if (status == "PENDING_ACTIVE")
                {
                    whereClauses.Add("status IN ('Received', 'Diagnosing', 'Processing', 'WaitingParts')");
                }
                else
                {
                    whereClauses.Add("status = @status");
                    parameters.Add(new SqlParameter("@status", status));
                }
            }

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                whereClauses.Add(@"(ticket_code LIKE @kw 
                                  OR customer_name LIKE @kw 
                                  OR customer_phone LIKE @kw 
                                  OR imei_number LIKE @kw 
                                  OR serial_number LIKE @kw 
                                  OR product_name LIKE @kw)");
                parameters.Add(new SqlParameter("@kw", $"%{keyword.Trim()}%"));
            }

            var whereSql = whereClauses.Count > 0 ? "WHERE " + string.Join(" AND ", whereClauses) : "";

            // Count total
            var countSql = $"SELECT COUNT(*) FROM vw_WarrantyTicketDetail {whereSql}";
            using (var countCmd = new SqlCommand(countSql, conn))
            {
                foreach (var p in parameters) countCmd.Parameters.Add(new SqlParameter(p.ParameterName, p.Value));
                totalCount = Convert.ToInt32(countCmd.ExecuteScalar() ?? 0);
            }

            // Paging query
            int offset = (page - 1) * pageSize;
            var dataSql = $@"
                SELECT * FROM vw_WarrantyTicketDetail
                {whereSql}
                ORDER BY 
                    CASE WHEN status IN ('Received', 'Diagnosing', 'Processing') THEN 0 
                         WHEN status = 'WaitingParts' THEN 1 
                         WHEN status = 'Completed' THEN 2 
                         ELSE 3 END ASC,
                    receive_date DESC
                OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY";

            using (var cmd = new SqlCommand(dataSql, conn))
            {
                foreach (var p in parameters) cmd.Parameters.Add(new SqlParameter(p.ParameterName, p.Value));
                cmd.Parameters.AddWithValue("@offset", offset);
                cmd.Parameters.AddWithValue("@pageSize", pageSize);

                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    list.Add(MapTicketFromReader(r));
                }
            }

            return list;
        }

        public WarrantyTicketModel? GetTicketById(int ticketId)
        {
            using var conn = new SqlConnection(_connectionString);
            conn.Open();

            var sql = "SELECT * FROM vw_WarrantyTicketDetail WHERE ticket_id = @id";
            WarrantyTicketModel? ticket = null;

            using (var cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@id", ticketId);
                using var r = cmd.ExecuteReader();
                if (r.Read())
                {
                    ticket = MapTicketFromReader(r);
                }
            }

            if (ticket != null)
            {
                // Load components
                var compSql = "SELECT * FROM Warranty_Ticket_Component WHERE ticket_id = @id ORDER BY component_id ASC";
                using var compCmd = new SqlCommand(compSql, conn);
                compCmd.Parameters.AddWithValue("@id", ticketId);
                using var compReader = compCmd.ExecuteReader();
                while (compReader.Read())
                {
                    ticket.Components.Add(new WarrantyTicketComponentModel
                    {
                        ComponentId = Convert.ToInt32(compReader["component_id"]),
                        TicketId = Convert.ToInt32(compReader["ticket_id"]),
                        ComponentName = compReader["component_name"].ToString()!,
                        Quantity = Convert.ToInt32(compReader["quantity"]),
                        Price = Convert.ToDecimal(compReader["price"]),
                        Note = compReader["note"] != DBNull.Value ? compReader["note"].ToString() : null
                    });
                }

                // Load notification log
                var notifSql = "SELECT * FROM Warranty_Notification WHERE ticket_id = @id ORDER BY sent_at DESC";
                using var notifCmd = new SqlCommand(notifSql, conn);
                notifCmd.Parameters.AddWithValue("@id", ticketId);
                using var notifReader = notifCmd.ExecuteReader();
                while (notifReader.Read())
                {
                    ticket.Notifications.Add(new WarrantyNotificationModel
                    {
                        NotificationId = Convert.ToInt32(notifReader["notification_id"]),
                        TicketId = Convert.ToInt32(notifReader["ticket_id"]),
                        TicketCode = notifReader["ticket_code"].ToString()!,
                        CustomerId = notifReader["customer_id"] != DBNull.Value ? Convert.ToInt32(notifReader["customer_id"]) : null,
                        Channel = notifReader["channel"].ToString()!,
                        Recipient = notifReader["recipient"].ToString()!,
                        Title = notifReader["title"].ToString()!,
                        MessageBody = notifReader["message_body"].ToString()!,
                        Status = notifReader["status"].ToString()!,
                        SentAt = Convert.ToDateTime(notifReader["sent_at"])
                    });
                }
            }

            return ticket;
        }

        public List<WarrantyTicketModel> GetTicketsByCustomerId(int customerId, string? phone = null)
        {
            var list = new List<WarrantyTicketModel>();
            using var conn = new SqlConnection(_connectionString);
            conn.Open();

            var sql = @"
                SELECT * FROM vw_WarrantyTicketDetail 
                WHERE customer_id = @cid OR (@phone IS NOT NULL AND customer_phone = @phone)
                ORDER BY receive_date DESC";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@cid", customerId);
            cmd.Parameters.AddWithValue("@phone", (object?)phone?.Trim() ?? DBNull.Value);

            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                list.Add(MapTicketFromReader(r));
            }
            return list;
        }

        public bool CreateHomeWarrantyTicket(HomeWarrantyRegisterViewModel model, out string ticketCode, out int newTicketId, out string errorMessage)
        {
            ticketCode = string.Empty;
            newTicketId = 0;
            errorMessage = string.Empty;

            try
            {
                using var conn = new SqlConnection(_connectionString);
                conn.Open();

                int? foundSerialId = null;
                int? foundWarrantyId = null;
                int? foundCustId = model.CustomerId;

                if (!string.IsNullOrWhiteSpace(model.SerialNumberOrImei))
                {
                    var sqlFind = @"
                        SELECT TOP 1 ps.serial_id, w.warranty_id, i.customer_id
                        FROM Invoice i
                        JOIN Invoice_Detail id ON i.invoice_id = id.invoice_id
                        JOIN Product p ON id.product_id = p.product_id
                        LEFT JOIN Warranty w ON w.invoice_detail_id = id.invoice_detail_id
                        LEFT JOIN Product_Serial ps ON w.serial_id = ps.serial_id
                        WHERE ps.imei_number = @q 
                           OR ps.serial_number = @q
                           OR ('INV-' + RIGHT('000' + CAST(i.invoice_id AS VARCHAR), 3) + '-P' + CAST(p.product_id AS VARCHAR)) = @q
                           OR ('INV-' + CAST(i.invoice_id AS VARCHAR) + '-P' + CAST(p.product_id AS VARCHAR)) = @q";

                    using var cmdFind = new SqlCommand(sqlFind, conn);
                    cmdFind.Parameters.AddWithValue("@q", model.SerialNumberOrImei.Trim());
                    using var r = cmdFind.ExecuteReader();
                    if (r.Read())
                    {
                        foundSerialId = r["serial_id"] != DBNull.Value ? Convert.ToInt32(r["serial_id"]) : null;
                        foundWarrantyId = r["warranty_id"] != DBNull.Value ? Convert.ToInt32(r["warranty_id"]) : null;
                        if (!foundCustId.HasValue && r["customer_id"] != DBNull.Value)
                        {
                            foundCustId = Convert.ToInt32(r["customer_id"]);
                        }
                    }
                }

                if (!foundCustId.HasValue && !string.IsNullOrWhiteSpace(model.CustomerPhone))
                {
                    using var cmdCust = new SqlCommand("SELECT TOP 1 customer_id FROM Customer WHERE phone = @p", conn);
                    cmdCust.Parameters.AddWithValue("@p", model.CustomerPhone.Trim());
                    var res = cmdCust.ExecuteScalar();
                    if (res != null && res != DBNull.Value)
                    {
                        foundCustId = Convert.ToInt32(res);
                    }
                }

                // Sinh mã phiếu tự động: BH-YYYYMMDD-XXXX
                string todayStr = DateTime.Now.ToString("yyyyMMdd");
                int nextNum = 1;
                using (var cmdSeq = new SqlCommand("SELECT ISNULL(MAX(CAST(RIGHT(ticket_code, 4) AS INT)), 0) + 1 FROM Warranty_Ticket WHERE ticket_code LIKE 'BH-' + @today + '-%'", conn))
                {
                    cmdSeq.Parameters.AddWithValue("@today", todayStr);
                    var res = cmdSeq.ExecuteScalar();
                    if (res != null && res != DBNull.Value)
                    {
                        nextNum = Convert.ToInt32(res);
                    }
                }

                ticketCode = $"BH-{todayStr}-{nextNum:D4}";

                DateTime pickupDateTime = model.PickupDate.Date;
                if (model.PickupTimeSlot.Contains("14:00")) pickupDateTime = pickupDateTime.AddHours(14);
                else if (model.PickupTimeSlot.Contains("18:00")) pickupDateTime = pickupDateTime.AddHours(18);
                else pickupDateTime = pickupDateTime.AddHours(9).AddMinutes(30);

                var insertSql = @"
                    INSERT INTO Warranty_Ticket (
                        ticket_code, warranty_id, serial_id, customer_id, customer_name, customer_phone, customer_email,
                        receive_date, expected_return_date, customer_issue, device_condition, status, notes,
                        is_home_service, pickup_address, pickup_time, service_type, shipping_carrier, shipping_fee, shipping_distance_km,
                        payment_status
                    )
                    VALUES (
                        @tcode, @wid, @sid, @cid, @cname, @cphone, @cemail,
                        GETDATE(), DATEADD(DAY, 3, CAST(GETDATE() AS DATE)), @issue, @cond, 'Received', @notes,
                        1, @pickupAddr, @pickupTime, @svcType, @carrier, @shipFee, @distKm,
                        'Free'
                    );
                    SELECT SCOPE_IDENTITY();";

                using (var insCmd = new SqlCommand(insertSql, conn))
                {
                    insCmd.Parameters.AddWithValue("@tcode", ticketCode);
                    insCmd.Parameters.AddWithValue("@wid", (object?)foundWarrantyId ?? DBNull.Value);
                    insCmd.Parameters.AddWithValue("@sid", (object?)foundSerialId ?? DBNull.Value);
                    insCmd.Parameters.AddWithValue("@cid", (object?)foundCustId ?? DBNull.Value);
                    insCmd.Parameters.AddWithValue("@cname", model.CustomerName.Trim());
                    insCmd.Parameters.AddWithValue("@cphone", model.CustomerPhone.Trim());
                    insCmd.Parameters.AddWithValue("@cemail", (object?)model.CustomerEmail?.Trim() ?? DBNull.Value);
                    insCmd.Parameters.AddWithValue("@issue", $"[BẢO HÀNH TẠI NHÀ - {model.DeviceName}] {model.CustomerIssue.Trim()}");
                    insCmd.Parameters.AddWithValue("@cond", (object?)model.DeviceCondition?.Trim() ?? DBNull.Value);
                    insCmd.Parameters.AddWithValue("@notes", (object?)model.Notes?.Trim() ?? DBNull.Value);
                    insCmd.Parameters.AddWithValue("@pickupAddr", model.PickupAddress.Trim());
                    insCmd.Parameters.AddWithValue("@pickupTime", pickupDateTime);
                    insCmd.Parameters.AddWithValue("@svcType", (object?)model.ServiceType ?? "Kỹ thuật viên KittyPhone qua tận nơi");
                    insCmd.Parameters.AddWithValue("@carrier", (object?)model.ShippingCarrier ?? "Xanh SM Express");
                    insCmd.Parameters.AddWithValue("@shipFee", model.EstimatedShippingFee);
                    insCmd.Parameters.AddWithValue("@distKm", model.EstimatedDistanceKm);

                    newTicketId = Convert.ToInt32(insCmd.ExecuteScalar());
                }

                return true;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }
        }

        public bool CreateTicket(WarrantyTicketCreateViewModel model, int? defaultTechId, out string ticketCode, out string errorMessage)
        {
            ticketCode = string.Empty;
            errorMessage = string.Empty;

            try
            {
                using var conn = new SqlConnection(_connectionString);
                using var cmd = new SqlCommand("usp_CreateWarrantyTicket", conn);
                cmd.CommandType = CommandType.StoredProcedure;

                cmd.Parameters.AddWithValue("@serial_number_or_imei", (object?)model.SerialNumberOrImei?.Trim() ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@customer_name", model.CustomerName.Trim());
                cmd.Parameters.AddWithValue("@customer_phone", model.CustomerPhone.Trim());
                cmd.Parameters.AddWithValue("@technician_id", (object?)model.TechnicianId ?? (object?)defaultTechId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@customer_issue", model.CustomerIssue.Trim());
                cmd.Parameters.AddWithValue("@device_condition", (object?)model.DeviceCondition?.Trim() ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@expected_days", model.ExpectedDays > 0 ? model.ExpectedDays : 3);
                cmd.Parameters.AddWithValue("@notes", (object?)model.Notes?.Trim() ?? DBNull.Value);

                var outCode = new SqlParameter("@new_ticket_code", SqlDbType.VarChar, 50)
                {
                    Direction = ParameterDirection.Output
                };
                cmd.Parameters.Add(outCode);

                conn.Open();
                cmd.ExecuteNonQuery();

                ticketCode = outCode.Value?.ToString() ?? string.Empty;
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }
        }

        public bool UpdateTicket(WarrantyTicketUpdateViewModel model, out string errorMessage)
        {
            errorMessage = string.Empty;

            try
            {
                using var conn = new SqlConnection(_connectionString);
                conn.Open();
                using var trans = conn.BeginTransaction();

                try
                {
                    using (var cmd = new SqlCommand("usp_UpdateWarrantyTicket", conn, trans))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@ticket_id", model.TicketId);
                        cmd.Parameters.AddWithValue("@technician_id", (object?)model.TechnicianId ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@diagnosis", (object?)model.Diagnosis?.Trim() ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@repair_action", (object?)model.RepairAction?.Trim() ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@repair_cost", model.RepairCost);
                        cmd.Parameters.AddWithValue("@status", model.Status);
                        cmd.Parameters.AddWithValue("@notes", (object?)model.Notes?.Trim() ?? DBNull.Value);

                        cmd.ExecuteNonQuery();
                    }

                    // Update components if provided
                    if (model.Components != null && model.Components.Count > 0)
                    {
                        // Clean existing components and re-insert
                        using var delCmd = new SqlCommand("DELETE FROM Warranty_Ticket_Component WHERE ticket_id = @id", conn, trans);
                        delCmd.Parameters.AddWithValue("@id", model.TicketId);
                        delCmd.ExecuteNonQuery();

                        foreach (var comp in model.Components.Where(c => !string.IsNullOrWhiteSpace(c.ComponentName)))
                        {
                            using var insComp = new SqlCommand(@"
                                INSERT INTO Warranty_Ticket_Component (ticket_id, component_name, quantity, price, note)
                                VALUES (@tid, @cname, @qty, @price, @note)", conn, trans);
                            insComp.Parameters.AddWithValue("@tid", model.TicketId);
                            insComp.Parameters.AddWithValue("@cname", comp.ComponentName.Trim());
                            insComp.Parameters.AddWithValue("@qty", comp.Quantity > 0 ? comp.Quantity : 1);
                            insComp.Parameters.AddWithValue("@price", comp.Price >= 0 ? comp.Price : 0);
                            insComp.Parameters.AddWithValue("@note", (object?)comp.Note?.Trim() ?? DBNull.Value);
                            insComp.ExecuteNonQuery();
                        }
                    }

                    trans.Commit();
                    return true;
                }
                catch
                {
                    trans.Rollback();
                    throw;
                }
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }
        }

        public bool DeliverTicket(int ticketId, string? notes, out string errorMessage)
        {
            errorMessage = string.Empty;
            try
            {
                using var conn = new SqlConnection(_connectionString);
                using var cmd = new SqlCommand("usp_DeliverWarrantyTicket", conn);
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@ticket_id", ticketId);
                cmd.Parameters.AddWithValue("@notes", (object?)notes?.Trim() ?? DBNull.Value);

                conn.Open();
                cmd.ExecuteNonQuery();
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }
        }

        public bool UpdatePayment(int ticketId, string paymentMethod, decimal paidAmount, decimal discountAmount, string? voucherCode, out string errorMessage)
        {
            errorMessage = string.Empty;
            try
            {
                using var conn = new SqlConnection(_connectionString);
                conn.Open();
                var sql = @"
                    UPDATE Warranty_Ticket
                    SET payment_status = 'Paid',
                        payment_method = @method,
                        paid_amount = @amount,
                        discount_amount = @discount,
                        voucher_code = @voucher,
                        paid_at = GETDATE()
                    WHERE ticket_id = @id";
                using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@id", ticketId);
                cmd.Parameters.AddWithValue("@method", (object?)paymentMethod ?? "ZaloPay QR");
                cmd.Parameters.AddWithValue("@amount", paidAmount);
                cmd.Parameters.AddWithValue("@discount", discountAmount);
                cmd.Parameters.AddWithValue("@voucher", (object?)voucherCode ?? DBNull.Value);

                return cmd.ExecuteNonQuery() > 0;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }
        }

        public WarrantyLookupResultViewModel LookupWarranty(string query)
        {
            var res = new WarrantyLookupResultViewModel
            {
                SearchQuery = query.Trim()
            };

            if (string.IsNullOrWhiteSpace(query)) return res;

            using var conn = new SqlConnection(_connectionString);
            conn.Open();

            // 1. Try search by IMEI or Serial in fn_CheckWarrantyByImei
            var sqlCheck = "SELECT * FROM fn_CheckWarrantyByImei(@imei)";
            using (var cmd = new SqlCommand(sqlCheck, conn))
            {
                cmd.Parameters.AddWithValue("@imei", query.Trim());
                using var r = cmd.ExecuteReader();
                if (r.Read())
                {
                    res.Found = true;
                    res.ProductId = r["product_id"] != DBNull.Value ? Convert.ToInt32(r["product_id"]) : null;
                    res.ProductName = r["product_name"]?.ToString();
                    res.BrandName = r["brand_name"]?.ToString();
                    res.ImeiNumber = r["imei_number"]?.ToString();
                    res.SerialNumber = r["serial_number"]?.ToString();
                    res.WarrantyId = r["warranty_id"] != DBNull.Value ? Convert.ToInt32(r["warranty_id"]) : null;
                    res.WarrantyStart = r["warranty_start"] != DBNull.Value ? Convert.ToDateTime(r["warranty_start"]) : null;
                    res.WarrantyEnd = r["warranty_end"] != DBNull.Value ? Convert.ToDateTime(r["warranty_end"]) : null;
                    res.WarrantyStatus = r["warranty_status"]?.ToString();
                    res.IsEligible = r["warranty_eligibility"]?.ToString() == "Còn trong hạn bảo hành";
                    res.DaysLeft = Convert.ToInt32(r["days_left"]);
                    res.InvoiceId = r["invoice_id"] != DBNull.Value ? Convert.ToInt32(r["invoice_id"]) : null;
                    res.InvoiceDate = r["invoice_date"] != DBNull.Value ? Convert.ToDateTime(r["invoice_date"]) : null;
                    res.CustomerId = r["customer_id"] != DBNull.Value ? Convert.ToInt32(r["customer_id"]) : null;
                    res.CustomerName = r["customer_name"]?.ToString();
                    res.CustomerPhone = r["customer_phone"]?.ToString();
                }
            }

            // 2. If not found by IMEI, fallback to search by invoice identifier INV-004-P82 or Product_Serial
            if (!res.Found)
            {
                var sqlFallbackInv = @"
                    SELECT TOP 1 
                        w.warranty_id, w.warranty_start, w.warranty_end, w.warranty_status, w.warranty_note,
                        id.invoice_id, i.invoice_date, c.customer_id, c.customer_name, c.phone AS customer_phone,
                        p.product_id, p.product_name, b.brand_name,
                        ps.imei_number, ps.serial_number
                    FROM Invoice i
                    JOIN Customer c         ON i.customer_id = c.customer_id
                    JOIN Invoice_Detail id  ON i.invoice_id = id.invoice_id
                    JOIN Product p          ON id.product_id = p.product_id
                    JOIN Brand b            ON p.brand_id = b.brand_id
                    LEFT JOIN Warranty w    ON id.invoice_detail_id = w.invoice_detail_id
                    LEFT JOIN Product_Serial ps ON w.serial_id = ps.serial_id
                    WHERE ('INV-' + RIGHT('000' + CAST(i.invoice_id AS VARCHAR), 3) + '-P' + CAST(p.product_id AS VARCHAR)) = @q
                       OR ('INV-' + CAST(i.invoice_id AS VARCHAR) + '-P' + CAST(p.product_id AS VARCHAR)) = @q
                       OR ps.imei_number = @q 
                       OR ps.serial_number = @q
                    ORDER BY i.invoice_date DESC";

                using (var cmdInv = new SqlCommand(sqlFallbackInv, conn))
                {
                    cmdInv.Parameters.AddWithValue("@q", query.Trim());
                    using var r = cmdInv.ExecuteReader();
                    if (r.Read())
                    {
                        res.Found = true;
                        int invId = Convert.ToInt32(r["invoice_id"]);
                        int prodId = Convert.ToInt32(r["product_id"]);
                        res.ProductId = prodId;
                        res.ProductName = r["product_name"]?.ToString();
                        res.BrandName = r["brand_name"]?.ToString();
                        res.ImeiNumber = r["imei_number"] != DBNull.Value ? r["imei_number"].ToString() : $"INV-{invId:D3}-P{prodId}";
                        res.SerialNumber = r["serial_number"] != DBNull.Value ? r["serial_number"].ToString() : $"SN-INV{invId:D3}";
                        res.WarrantyId = r["warranty_id"] != DBNull.Value ? Convert.ToInt32(r["warranty_id"]) : null;
                        res.InvoiceId = invId;
                        res.InvoiceDate = r["invoice_date"] != DBNull.Value ? Convert.ToDateTime(r["invoice_date"]) : null;
                        res.CustomerId = r["customer_id"] != DBNull.Value ? Convert.ToInt32(r["customer_id"]) : null;
                        res.CustomerName = r["customer_name"]?.ToString();
                        res.CustomerPhone = r["customer_phone"]?.ToString();

                        DateTime start = r["warranty_start"] != DBNull.Value ? Convert.ToDateTime(r["warranty_start"]) : (res.InvoiceDate ?? DateTime.Today);
                        DateTime end = r["warranty_end"] != DBNull.Value ? Convert.ToDateTime(r["warranty_end"]) : start.AddMonths(12);
                        res.WarrantyStart = start;
                        res.WarrantyEnd = end;
                        res.WarrantyStatus = r["warranty_status"]?.ToString() ?? (DateTime.Today <= end ? "Active" : "Expired");
                        res.IsEligible = end >= DateTime.Today;
                        res.DaysLeft = res.IsEligible ? (end - DateTime.Today).Days : 0;
                    }
                }
            }

            // 3. If not found by IMEI or Invoice ID, fallback to search by Customer Phone or Ticket Code
            if (!res.Found)
            {
                var sqlFallback = @"
                    SELECT TOP 1 * FROM vw_WarrantyTicketDetail 
                    WHERE ticket_code = @q OR customer_phone = @q 
                    ORDER BY ticket_id DESC";

                using var cmd = new SqlCommand(sqlFallback, conn);
                cmd.Parameters.AddWithValue("@q", query.Trim());
                using var r = cmd.ExecuteReader();
                if (r.Read())
                {
                    res.Found = true;
                    res.ProductName = r["product_name"]?.ToString();
                    res.BrandName = r["brand_name"]?.ToString();
                    res.ImeiNumber = r["imei_number"]?.ToString();
                    res.CustomerName = r["customer_name"]?.ToString();
                    res.CustomerPhone = r["customer_phone"]?.ToString();
                    res.WarrantyStart = r["warranty_start"] != DBNull.Value ? Convert.ToDateTime(r["warranty_start"]) : null;
                    res.WarrantyEnd = r["warranty_end"] != DBNull.Value ? Convert.ToDateTime(r["warranty_end"]) : null;
                    res.WarrantyStatus = r["original_warranty_status"]?.ToString();
                    res.DaysLeft = Convert.ToInt32(r["warranty_days_remaining"]);
                    res.IsEligible = res.DaysLeft > 0;
                }
            }

            // Also load any ticket history associated
            if (res.Found)
            {
                var sqlHistory = @"
                    SELECT * FROM vw_WarrantyTicketDetail
                    WHERE (imei_number = @imei OR serial_number = @imei OR customer_phone = @phone)
                    ORDER BY receive_date DESC";

                using var histCmd = new SqlCommand(sqlHistory, conn);
                histCmd.Parameters.AddWithValue("@imei", (object?)res.ImeiNumber ?? DBNull.Value);
                histCmd.Parameters.AddWithValue("@phone", (object?)res.CustomerPhone ?? DBNull.Value);

                using var r = histCmd.ExecuteReader();
                while (r.Read())
                {
                    res.TicketHistory.Add(MapTicketFromReader(r));
                }
            }

            return res;
        }

        public List<WarrantyCardModel> GetAllWarrantyCards(string? keyword, string? status)
        {
            var list = new List<WarrantyCardModel>();
            using var conn = new SqlConnection(_connectionString);
            conn.Open();

            var whereClauses = new List<string>();
            var parameters = new List<SqlParameter>();

            if (!string.IsNullOrWhiteSpace(status))
            {
                if (status == "Active")
                {
                    whereClauses.Add("w.warranty_end >= CAST(GETDATE() AS DATE) AND w.warranty_status <> 'Expired'");
                }
                else if (status == "Expired")
                {
                    whereClauses.Add("(w.warranty_end < CAST(GETDATE() AS DATE) OR w.warranty_status = 'Expired')");
                }
                else if (status == "Claimed")
                {
                    whereClauses.Add("w.warranty_status = 'Claimed'");
                }
            }

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                whereClauses.Add(@"(p.product_name LIKE @kw 
                                  OR ps.imei_number LIKE @kw 
                                  OR ps.serial_number LIKE @kw 
                                  OR c.customer_name LIKE @kw 
                                  OR c.phone LIKE @kw)");
                parameters.Add(new SqlParameter("@kw", $"%{keyword.Trim()}%"));
            }

            var whereSql = whereClauses.Count > 0 ? "WHERE " + string.Join(" AND ", whereClauses) : "";

            var sql = $@"
                SELECT 
                    w.warranty_id, w.invoice_detail_id, w.serial_id, w.warranty_start, w.warranty_end,
                    w.warranty_status, w.warranty_note,
                    ps.imei_number, ps.serial_number,
                    p.product_id, p.product_name, b.brand_name,
                    i.invoice_id, c.customer_name, c.phone AS customer_phone
                FROM Warranty w
                JOIN Invoice_Detail id  ON w.invoice_detail_id = id.invoice_detail_id
                JOIN Invoice i          ON id.invoice_id = i.invoice_id
                JOIN Customer c         ON i.customer_id = c.customer_id
                JOIN Product p          ON id.product_id = p.product_id
                JOIN Brand b            ON p.brand_id = b.brand_id
                LEFT JOIN Product_Serial ps ON w.serial_id = ps.serial_id
                {whereSql}
                ORDER BY w.warranty_end DESC";

            using var cmd = new SqlCommand(sql, conn);
            foreach (var p in parameters) cmd.Parameters.Add(new SqlParameter(p.ParameterName, p.Value));

            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                int invId = Convert.ToInt32(r["invoice_id"]);
                int prodId = Convert.ToInt32(r["product_id"]);
                string? imei = r["imei_number"] != DBNull.Value ? r["imei_number"].ToString() : $"INV-{invId:D3}-P{prodId}";
                string? serial = r["serial_number"] != DBNull.Value ? r["serial_number"].ToString() : $"SN-INV{invId:D3}";

                list.Add(new WarrantyCardModel
                {
                    WarrantyId = Convert.ToInt32(r["warranty_id"]),
                    InvoiceDetailId = Convert.ToInt32(r["invoice_detail_id"]),
                    SerialId = r["serial_id"] != DBNull.Value ? Convert.ToInt32(r["serial_id"]) : null,
                    ImeiNumber = imei,
                    SerialNumber = serial,
                    ProductId = prodId,
                    ProductName = r["product_name"].ToString()!,
                    BrandName = r["brand_name"].ToString()!,
                    InvoiceId = invId,
                    CustomerName = r["customer_name"].ToString()!,
                    CustomerPhone = r["customer_phone"].ToString()!,
                    WarrantyStart = Convert.ToDateTime(r["warranty_start"]),
                    WarrantyEnd = Convert.ToDateTime(r["warranty_end"]),
                    WarrantyStatus = r["warranty_status"].ToString()!,
                    WarrantyNote = r["warranty_note"] != DBNull.Value ? r["warranty_note"].ToString() : null
                });
            }

            return list;
        }

        /// <summary>
        /// Truy vấn danh sách thiết bị đang được bảo hành chính hãng của Khách hàng theo CustomerId
        /// Hỗ trợ hóa đơn có trạng thái Delivered HOẶC Paid. Tự động kích hoạt sổ bảo hành và fallback mã định danh INV-{InvoiceId}-P{ProductId}.
        /// </summary>
        public List<WarrantyCardModel> GetWarrantedDevicesByCustomerId(int customerId)
        {
            var list = new List<WarrantyCardModel>();
            using var conn = new SqlConnection(_connectionString);
            conn.Open();

            // 1. Tự động kiểm tra và đồng bộ / kích hoạt sổ bảo hành cho các sản phẩm thuộc hóa đơn Delivered hoặc Paid
            // Nếu đơn hàng chưa có bản ghi trong Warranty, tự động kích hoạt bảo hành điện tử chính hãng 12 tháng.
            var sqlUnsynced = @"
                SELECT 
                    id.invoice_detail_id,
                    id.invoice_id,
                    id.product_id,
                    id.quantity,
                    i.invoice_date
                FROM Invoice i
                JOIN Invoice_Detail id ON i.invoice_id = id.invoice_id
                LEFT JOIN Warranty w ON id.invoice_detail_id = w.invoice_detail_id
                WHERE i.customer_id = @customerId
                  AND i.status IN ('Delivered', 'Paid')
                  AND w.warranty_id IS NULL";

            var unactivatedItems = new List<(int detailId, int invoiceId, int productId, int quantity, DateTime invoiceDate)>();
            using (var cmdUnsynced = new SqlCommand(sqlUnsynced, conn))
            {
                cmdUnsynced.Parameters.AddWithValue("@customerId", customerId);
                using var rUnsynced = cmdUnsynced.ExecuteReader();
                while (rUnsynced.Read())
                {
                    unactivatedItems.Add((
                        Convert.ToInt32(rUnsynced["invoice_detail_id"]),
                        Convert.ToInt32(rUnsynced["invoice_id"]),
                        Convert.ToInt32(rUnsynced["product_id"]),
                        Convert.ToInt32(rUnsynced["quantity"]),
                        Convert.ToDateTime(rUnsynced["invoice_date"])
                    ));
                }
            }

            // Tự động kích hoạt bản ghi Warranty cho các mục chưa có
            foreach (var item in unactivatedItems)
            {
                int? foundSerialId = null;
                var sqlCheckSerial = @"
                    SELECT TOP 1 serial_id 
                    FROM Product_Serial 
                    WHERE product_id = @pid AND status = 'In Stock' 
                    ORDER BY serial_id ASC";
                using (var cmdSerial = new SqlCommand(sqlCheckSerial, conn))
                {
                    cmdSerial.Parameters.AddWithValue("@pid", item.productId);
                    var res = cmdSerial.ExecuteScalar();
                    if (res != null && res != DBNull.Value)
                    {
                        foundSerialId = Convert.ToInt32(res);
                    }
                }

                if (foundSerialId.HasValue)
                {
                    using var cmdMarkSold = new SqlCommand("UPDATE Product_Serial SET status = 'Sold' WHERE serial_id = @sid", conn);
                    cmdMarkSold.Parameters.AddWithValue("@sid", foundSerialId.Value);
                    cmdMarkSold.ExecuteNonQuery();
                }

                DateTime startDate = item.invoiceDate;
                DateTime endDate = startDate.AddMonths(12);
                string status = (DateTime.Today <= endDate) ? "Active" : "Expired";
                string note = $"Kích hoạt bảo hành điện tử chính hãng 12 tháng KittyCare theo hóa đơn #{item.invoiceId}";

                var sqlInsertWarranty = @"
                    INSERT INTO Warranty (invoice_detail_id, serial_id, warranty_start, warranty_end, warranty_status, warranty_note)
                    VALUES (@detailId, @serialId, @start, @end, @status, @note)";
                using var cmdInsert = new SqlCommand(sqlInsertWarranty, conn);
                cmdInsert.Parameters.AddWithValue("@detailId", item.detailId);
                cmdInsert.Parameters.AddWithValue("@serialId", (object?)foundSerialId ?? DBNull.Value);
                cmdInsert.Parameters.AddWithValue("@start", startDate);
                cmdInsert.Parameters.AddWithValue("@end", endDate);
                cmdInsert.Parameters.AddWithValue("@status", status);
                cmdInsert.Parameters.AddWithValue("@note", note);
                cmdInsert.ExecuteNonQuery();
            }

            // 2. Truy vấn danh sách thiết bị bảo hành đầy đủ của khách hàng (LEFT JOIN Warranty để đảm bảo luôn hiển thị)
            var sql = @"
                SELECT 
                    ISNULL(w.warranty_id, 0) AS warranty_id,
                    id.invoice_detail_id, 
                    w.serial_id, 
                    ISNULL(w.warranty_start, i.invoice_date) AS warranty_start, 
                    ISNULL(w.warranty_end, DATEADD(month, 12, i.invoice_date)) AS warranty_end,
                    ISNULL(w.warranty_status, CASE WHEN DATEADD(month, 12, i.invoice_date) >= CAST(GETDATE() AS DATE) THEN 'Active' ELSE 'Expired' END) AS warranty_status, 
                    w.warranty_note,
                    ps.imei_number, 
                    ps.serial_number,
                    p.product_id, 
                    p.product_name, 
                    p.image_url, 
                    b.brand_name,
                    i.invoice_id, 
                    i.invoice_date, 
                    c.customer_name, 
                    c.phone AS customer_phone
                FROM Invoice i
                JOIN Customer c         ON i.customer_id = c.customer_id
                JOIN Invoice_Detail id  ON i.invoice_id = id.invoice_id
                JOIN Product p          ON id.product_id = p.product_id
                JOIN Brand b            ON p.brand_id = b.brand_id
                LEFT JOIN Warranty w    ON id.invoice_detail_id = w.invoice_detail_id
                LEFT JOIN Product_Serial ps ON w.serial_id = ps.serial_id
                WHERE i.customer_id = @customerId
                  AND i.status IN ('Delivered', 'Paid')
                ORDER BY ISNULL(w.warranty_end, DATEADD(month, 12, i.invoice_date)) DESC, id.invoice_detail_id DESC";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@customerId", customerId);

            using var rdr = cmd.ExecuteReader();
            while (rdr.Read())
            {
                int invId = Convert.ToInt32(rdr["invoice_id"]);
                int pId = Convert.ToInt32(rdr["product_id"]);

                string? imei = rdr["imei_number"] != DBNull.Value ? rdr["imei_number"].ToString() : null;
                string? serial = rdr["serial_number"] != DBNull.Value ? rdr["serial_number"].ToString() : null;

                // Fallback tự động nếu chưa có IMEI trong kho: INV-004-P{ProductId}
                if (string.IsNullOrWhiteSpace(imei))
                {
                    imei = $"INV-{invId:D3}-P{pId}";
                }
                if (string.IsNullOrWhiteSpace(serial))
                {
                    serial = $"SN-INV{invId:D3}";
                }

                list.Add(new WarrantyCardModel
                {
                    WarrantyId = Convert.ToInt32(rdr["warranty_id"]),
                    InvoiceDetailId = Convert.ToInt32(rdr["invoice_detail_id"]),
                    SerialId = rdr["serial_id"] != DBNull.Value ? Convert.ToInt32(rdr["serial_id"]) : null,
                    ImeiNumber = imei,
                    SerialNumber = serial,
                    ProductId = pId,
                    ProductName = rdr["product_name"].ToString()!,
                    ImageUrl = rdr["image_url"] != DBNull.Value ? rdr["image_url"].ToString() : null,
                    BrandName = rdr["brand_name"].ToString()!,
                    InvoiceId = invId,
                    PurchaseDate = Convert.ToDateTime(rdr["invoice_date"]),
                    CustomerName = rdr["customer_name"].ToString()!,
                    CustomerPhone = rdr["customer_phone"].ToString()!,
                    WarrantyStart = Convert.ToDateTime(rdr["warranty_start"]),
                    WarrantyEnd = Convert.ToDateTime(rdr["warranty_end"]),
                    WarrantyStatus = rdr["warranty_status"].ToString()!,
                    WarrantyNote = rdr["warranty_note"] != DBNull.Value ? rdr["warranty_note"].ToString() : null
                });
            }

            return list;
        }

        public List<EmployeeOptionModel> GetAllTechnicians()
        {
            var list = new List<EmployeeOptionModel>();
            using var conn = new SqlConnection(_connectionString);
            conn.Open();

            var sql = @"
                SELECT employee_id, employee_name, position, phone 
                FROM Employee 
                WHERE position IN (N'Technician', N'Kỹ thuật viên', N'Technical Staff') 
                   OR employee_name LIKE N'%Kỹ Thuật%'
                ORDER BY employee_id ASC";

            using var cmd = new SqlCommand(sql, conn);
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                list.Add(new EmployeeOptionModel
                {
                    EmployeeId = Convert.ToInt32(r["employee_id"]),
                    EmployeeName = r["employee_name"].ToString()!,
                    Position = r["position"] != DBNull.Value ? r["position"].ToString() : null,
                    Phone = r["phone"] != DBNull.Value ? r["phone"].ToString() : null
                });
            }

            // Fallback: If no dedicated technician found, return all employees
            if (list.Count == 0)
            {
                r.Close();
                var fallbackSql = "SELECT employee_id, employee_name, position, phone FROM Employee ORDER BY employee_id ASC";
                using var fbCmd = new SqlCommand(fallbackSql, conn);
                using var fbReader = fbCmd.ExecuteReader();
                while (fbReader.Read())
                {
                    list.Add(new EmployeeOptionModel
                    {
                        EmployeeId = Convert.ToInt32(fbReader["employee_id"]),
                        EmployeeName = fbReader["employee_name"].ToString()!,
                        Position = fbReader["position"] != DBNull.Value ? fbReader["position"].ToString() : null,
                        Phone = fbReader["phone"] != DBNull.Value ? fbReader["phone"].ToString() : null
                    });
                }
            }

            return list;
        }

        private static WarrantyTicketModel MapTicketFromReader(SqlDataReader r)
        {
            return new WarrantyTicketModel
            {
                TicketId = Convert.ToInt32(r["ticket_id"]),
                TicketCode = r["ticket_code"].ToString()!,
                WarrantyId = r["warranty_id"] != DBNull.Value ? Convert.ToInt32(r["warranty_id"]) : null,
                SerialId = r["serial_id"] != DBNull.Value ? Convert.ToInt32(r["serial_id"]) : null,
                CustomerId = r["customer_id"] != DBNull.Value ? Convert.ToInt32(r["customer_id"]) : null,
                CustomerName = r["customer_name"].ToString()!,
                CustomerPhone = r["customer_phone"].ToString()!,
                TechnicianId = r["technician_id"] != DBNull.Value ? Convert.ToInt32(r["technician_id"]) : null,
                TechnicianName = r["technician_name"] != DBNull.Value ? r["technician_name"].ToString() : null,
                TechnicianPhone = r["technician_phone"] != DBNull.Value ? r["technician_phone"].ToString() : null,
                ReceiveDate = Convert.ToDateTime(r["receive_date"]),
                ExpectedReturnDate = r["expected_return_date"] != DBNull.Value ? Convert.ToDateTime(r["expected_return_date"]) : null,
                CompletedDate = r["completed_date"] != DBNull.Value ? Convert.ToDateTime(r["completed_date"]) : null,
                CustomerIssue = r["customer_issue"].ToString()!,
                DeviceCondition = r["device_condition"] != DBNull.Value ? r["device_condition"].ToString() : null,
                Diagnosis = r["diagnosis"] != DBNull.Value ? r["diagnosis"].ToString() : null,
                RepairAction = r["repair_action"] != DBNull.Value ? r["repair_action"].ToString() : null,
                RepairCost = Convert.ToDecimal(r["repair_cost"]),
                Status = r["status"].ToString()!,
                Notes = r["notes"] != DBNull.Value ? r["notes"].ToString() : null,
                CreatedAt = Convert.ToDateTime(r["created_at"]),

                ImeiNumber = r["imei_number"] != DBNull.Value ? r["imei_number"].ToString() : null,
                SerialNumber = r["serial_number"] != DBNull.Value ? r["serial_number"].ToString() : null,
                ProductId = r["product_id"] != DBNull.Value ? Convert.ToInt32(r["product_id"]) : null,
                ProductName = r["product_name"] != DBNull.Value ? r["product_name"].ToString() : null,
                BrandName = r["brand_name"] != DBNull.Value ? r["brand_name"].ToString() : null,

                WarrantyStart = r["warranty_start"] != DBNull.Value ? Convert.ToDateTime(r["warranty_start"]) : null,
                WarrantyEnd = r["warranty_end"] != DBNull.Value ? Convert.ToDateTime(r["warranty_end"]) : null,
                OriginalWarrantyStatus = r["original_warranty_status"] != DBNull.Value ? r["original_warranty_status"].ToString() : null,
                WarrantyCoverageText = r["warranty_coverage_text"] != DBNull.Value ? r["warranty_coverage_text"].ToString()! : "Chưa xác định",
                WarrantyDaysRemaining = r["warranty_days_remaining"] != DBNull.Value ? Convert.ToInt32(r["warranty_days_remaining"]) : 0,

                // Customer email & Home Service
                CustomerEmail = HasColumn(r, "customer_email") && r["customer_email"] != DBNull.Value ? r["customer_email"].ToString() : null,
                IsHomeService = HasColumn(r, "is_home_service") && r["is_home_service"] != DBNull.Value && Convert.ToBoolean(r["is_home_service"]),
                PickupAddress = HasColumn(r, "pickup_address") && r["pickup_address"] != DBNull.Value ? r["pickup_address"].ToString() : null,
                PickupTime = HasColumn(r, "pickup_time") && r["pickup_time"] != DBNull.Value ? Convert.ToDateTime(r["pickup_time"]) : null,
                ServiceType = HasColumn(r, "service_type") && r["service_type"] != DBNull.Value ? r["service_type"].ToString() : null,
                ShippingCarrier = HasColumn(r, "shipping_carrier") && r["shipping_carrier"] != DBNull.Value ? r["shipping_carrier"].ToString() : null,
                ShippingFee = HasColumn(r, "shipping_fee") && r["shipping_fee"] != DBNull.Value ? Convert.ToDecimal(r["shipping_fee"]) : 0,
                ShippingDistanceKm = HasColumn(r, "shipping_distance_km") && r["shipping_distance_km"] != DBNull.Value ? Convert.ToDecimal(r["shipping_distance_km"]) : null,

                // Payment columns
                PaymentStatus = HasColumn(r, "payment_status") && r["payment_status"] != DBNull.Value ? r["payment_status"].ToString()! : (Convert.ToDecimal(r["repair_cost"]) == 0 ? "Free" : "Unpaid"),
                PaymentMethod = HasColumn(r, "payment_method") && r["payment_method"] != DBNull.Value ? r["payment_method"].ToString() : null,
                PaidAmount = HasColumn(r, "paid_amount") && r["paid_amount"] != DBNull.Value ? Convert.ToDecimal(r["paid_amount"]) : 0,
                PaidAt = HasColumn(r, "paid_at") && r["paid_at"] != DBNull.Value ? Convert.ToDateTime(r["paid_at"]) : null,
                DiscountAmount = HasColumn(r, "discount_amount") && r["discount_amount"] != DBNull.Value ? Convert.ToDecimal(r["discount_amount"]) : 0,
                VoucherCode = HasColumn(r, "voucher_code") && r["voucher_code"] != DBNull.Value ? r["voucher_code"].ToString() : null
            };
        }

        private static bool HasColumn(SqlDataReader reader, string columnName)
        {
            for (int i = 0; i < reader.FieldCount; i++)
            {
                if (reader.GetName(i).Equals(columnName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }
    }
}
