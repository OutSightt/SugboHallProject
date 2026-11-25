using MySqlConnector;
using HallBook.Models;

namespace HallBook.Data;

/// <summary>
/// Data-access layer for HallBook using raw ADO.NET + MySqlConnector.
/// Demonstrates calling Stored Procedure, Function, and verifying Trigger output.
/// </summary>
public class HallBookDb
{
    private readonly string _connStr;

    public HallBookDb(string connectionString) => _connStr = connectionString;

    private MySqlConnection Open()
    {
        var conn = new MySqlConnection(_connStr);
        conn.Open();
        return conn;
    }

    // ═══════════════════════════════════════════
    //  DASHBOARD
    // ═══════════════════════════════════════════
    public DashboardViewModel GetDashboard()
    {
        var vm = new DashboardViewModel();
        using var conn = Open();

        using (var cmd = new MySqlCommand("SELECT COUNT(*) FROM residents", conn))
            vm.TotalResidents = Convert.ToInt32(cmd.ExecuteScalar());

        using (var cmd = new MySqlCommand("SELECT COUNT(*) FROM halls", conn))
            vm.TotalHalls = Convert.ToInt32(cmd.ExecuteScalar());

        using (var cmd = new MySqlCommand("SELECT COUNT(*) FROM hall_bookings", conn))
            vm.TotalBookings = Convert.ToInt32(cmd.ExecuteScalar());

        using (var cmd = new MySqlCommand("SELECT COUNT(*) FROM hall_bookings WHERE status='Pending'", conn))
            vm.PendingBookings = Convert.ToInt32(cmd.ExecuteScalar());

        using (var cmd = new MySqlCommand("SELECT COUNT(*) FROM hall_bookings WHERE status='Confirmed'", conn))
            vm.ConfirmedBookings = Convert.ToInt32(cmd.ExecuteScalar());

        using (var cmd = new MySqlCommand("SELECT COUNT(*) FROM hall_bookings WHERE status='Completed'", conn))
            vm.CompletedBookings = Convert.ToInt32(cmd.ExecuteScalar());

        using (var cmd = new MySqlCommand(@"
            SELECT hb.*, r.first_name, r.last_name, h.hall_name
            FROM hall_bookings hb
            JOIN residents r ON hb.resident_id = r.resident_id
            JOIN halls h     ON hb.hall_id     = h.hall_id
            ORDER BY hb.created_at DESC LIMIT 6", conn))
        using (var rdr = cmd.ExecuteReader())
            while (rdr.Read())
                vm.RecentBookings.Add(MapBooking(rdr));

        return vm;
    }

    // ═══════════════════════════════════════════
    //  RESIDENTS  CRUD
    // ═══════════════════════════════════════════
    public List<Resident> GetResidents(string? search = null)
    {
        var list = new List<Resident>();
        using var conn = Open();
        var sql = @"SELECT r.*, COUNT(hb.booking_id) AS booking_count
                    FROM residents r LEFT JOIN hall_bookings hb ON r.resident_id = hb.resident_id";
        if (!string.IsNullOrWhiteSpace(search))
            sql += " WHERE r.first_name LIKE @s OR r.last_name LIKE @s OR r.email LIKE @s OR r.phone LIKE @s";
        sql += " GROUP BY r.resident_id ORDER BY r.last_name, r.first_name";

        using var cmd = new MySqlCommand(sql, conn);
        if (!string.IsNullOrWhiteSpace(search))
            cmd.Parameters.AddWithValue("@s", $"%{search}%");

        using var rdr = cmd.ExecuteReader();
        while (rdr.Read())
            list.Add(new Resident
            {
                ResidentId = rdr.GetInt32("resident_id"),
                FirstName  = rdr.GetString("first_name"),
                LastName   = rdr.GetString("last_name"),
                Email      = rdr.GetString("email"),
                Phone      = rdr.GetString("phone"),
                Address    = rdr.IsDBNull(rdr.GetOrdinal("address")) ? null : rdr.GetString("address"),
                CreatedAt  = rdr.GetDateTime("created_at"),
                BookingCount = rdr.GetInt32("booking_count")
            });
        return list;
    }

    public Resident? GetResident(int id)
    {
        using var conn = Open();
        using var cmd = new MySqlCommand("SELECT *, 0 AS booking_count FROM residents WHERE resident_id=@id", conn);
        cmd.Parameters.AddWithValue("@id", id);
        using var rdr = cmd.ExecuteReader();
        if (!rdr.Read()) return null;
        return new Resident
        {
            ResidentId = rdr.GetInt32("resident_id"),
            FirstName = rdr.GetString("first_name"),
            LastName = rdr.GetString("last_name"),
            Email = rdr.GetString("email"),
            Phone = rdr.GetString("phone"),
            Address = rdr.IsDBNull(rdr.GetOrdinal("address")) ? null : rdr.GetString("address"),
            CreatedAt = rdr.GetDateTime("created_at")
        };
    }

    public (bool ok, string msg) CreateResident(Resident r)
    {
        try
        {
            using var conn = Open();
            using var cmd = new MySqlCommand(@"
                INSERT INTO residents (first_name, last_name, email, phone, address)
                VALUES (@fn, @ln, @em, @ph, @addr)", conn);
            cmd.Parameters.AddWithValue("@fn", r.FirstName);
            cmd.Parameters.AddWithValue("@ln", r.LastName);
            cmd.Parameters.AddWithValue("@em", r.Email);
            cmd.Parameters.AddWithValue("@ph", r.Phone);
            cmd.Parameters.AddWithValue("@addr", (object?)r.Address ?? DBNull.Value);
            cmd.ExecuteNonQuery();
            return (true, $"Resident {r.FullName} added successfully.");
        }
        catch (MySqlException ex) when (ex.Number == 1062)
        {
            return (false, $"Email '{r.Email}' already exists.");
        }
    }

    public (bool ok, string msg) UpdateResident(Resident r)
    {
        try
        {
            using var conn = Open();
            using var cmd = new MySqlCommand(@"
                UPDATE residents SET first_name=@fn, last_name=@ln, email=@em, phone=@ph, address=@addr
                WHERE resident_id=@id", conn);
            cmd.Parameters.AddWithValue("@fn", r.FirstName);
            cmd.Parameters.AddWithValue("@ln", r.LastName);
            cmd.Parameters.AddWithValue("@em", r.Email);
            cmd.Parameters.AddWithValue("@ph", r.Phone);
            cmd.Parameters.AddWithValue("@addr", (object?)r.Address ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@id", r.ResidentId);
            cmd.ExecuteNonQuery();
            return (true, $"Resident {r.FullName} updated successfully.");
        }
        catch (MySqlException ex) when (ex.Number == 1062)
        {
            return (false, $"Email '{r.Email}' already exists.");
        }
    }

    public (bool ok, string msg) DeleteResident(int id)
    {
        try
        {
            using var conn = Open();
            using var cmd = new MySqlCommand("DELETE FROM residents WHERE resident_id=@id", conn);
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
            return (true, "Resident deleted.");
        }
        catch (MySqlException ex) when (ex.Number == 1451)
        {
            return (false, "Cannot delete: resident has existing bookings.");
        }
    }

    // ═══════════════════════════════════════════
    //  HALLS  CRUD
    // ═══════════════════════════════════════════
    public List<Hall> GetHalls(string? search = null, string? status = null)
    {
        var list = new List<Hall>();
        using var conn = Open();
        var sql = @"SELECT h.*, COUNT(CASE WHEN hb.status IN ('Pending','Confirmed') THEN 1 END) AS active_bookings
                    FROM halls h LEFT JOIN hall_bookings hb ON h.hall_id = hb.hall_id WHERE 1=1";
        if (!string.IsNullOrWhiteSpace(search))
            sql += " AND (h.hall_name LIKE @s OR h.location LIKE @s)";
        if (!string.IsNullOrWhiteSpace(status))
            sql += " AND h.status = @st";
        sql += " GROUP BY h.hall_id ORDER BY h.hall_name";

        using var cmd = new MySqlCommand(sql, conn);
        if (!string.IsNullOrWhiteSpace(search))
            cmd.Parameters.AddWithValue("@s", $"%{search}%");
        if (!string.IsNullOrWhiteSpace(status))
            cmd.Parameters.AddWithValue("@st", status);

        using var rdr = cmd.ExecuteReader();
        while (rdr.Read()) list.Add(MapHall(rdr));
        return list;
    }

    public Hall? GetHall(int id)
    {
        using var conn = Open();
        using var cmd = new MySqlCommand("SELECT *, 0 AS active_bookings FROM halls WHERE hall_id=@id", conn);
        cmd.Parameters.AddWithValue("@id", id);
        using var rdr = cmd.ExecuteReader();
        return rdr.Read() ? MapHall(rdr) : null;
    }

    public HallDetailViewModel? GetHallDetail(int id)
    {
        var hall = GetHall(id);
        if (hall == null) return null;
        var bookings = new List<HallBooking>();
        using var conn = Open();
        using var cmd = new MySqlCommand(@"
            SELECT hb.*, r.first_name, r.last_name, h.hall_name
            FROM hall_bookings hb
            JOIN residents r ON hb.resident_id = r.resident_id
            JOIN halls h     ON hb.hall_id     = h.hall_id
            WHERE hb.hall_id = @id
            ORDER BY hb.event_date DESC", conn);
        cmd.Parameters.AddWithValue("@id", id);
        using var rdr = cmd.ExecuteReader();
        while (rdr.Read()) bookings.Add(MapBooking(rdr));
        return new HallDetailViewModel { Hall = hall, Bookings = bookings };
    }

    public (bool ok, string msg) CreateHall(Hall h)
    {
        try
        {
            using var conn = Open();
            using var cmd = new MySqlCommand(@"
                INSERT INTO halls (hall_name, location, capacity, rate_per_hour, status, description)
                VALUES (@n, @loc, @cap, @rate, @st, @desc)", conn);
            cmd.Parameters.AddWithValue("@n", h.HallName);
            cmd.Parameters.AddWithValue("@loc", h.Location);
            cmd.Parameters.AddWithValue("@cap", h.Capacity);
            cmd.Parameters.AddWithValue("@rate", h.RatePerHour);
            cmd.Parameters.AddWithValue("@st", h.Status);
            cmd.Parameters.AddWithValue("@desc", (object?)h.Description ?? DBNull.Value);
            cmd.ExecuteNonQuery();
            return (true, $"Hall '{h.HallName}' added successfully.");
        }
        catch (MySqlException ex) when (ex.Number == 1062)
        {
            return (false, $"Hall name '{h.HallName}' already exists.");
        }
    }

    public (bool ok, string msg) UpdateHall(Hall h)
    {
        try
        {
            using var conn = Open();
            using var cmd = new MySqlCommand(@"
                UPDATE halls SET hall_name=@n, location=@loc, capacity=@cap,
                rate_per_hour=@rate, status=@st, description=@desc WHERE hall_id=@id", conn);
            cmd.Parameters.AddWithValue("@n", h.HallName);
            cmd.Parameters.AddWithValue("@loc", h.Location);
            cmd.Parameters.AddWithValue("@cap", h.Capacity);
            cmd.Parameters.AddWithValue("@rate", h.RatePerHour);
            cmd.Parameters.AddWithValue("@st", h.Status);
            cmd.Parameters.AddWithValue("@desc", (object?)h.Description ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@id", h.HallId);
            cmd.ExecuteNonQuery();
            return (true, $"Hall '{h.HallName}' updated.");
        }
        catch (MySqlException ex) when (ex.Number == 1062)
        {
            return (false, $"Hall name '{h.HallName}' already exists.");
        }
    }

    public (bool ok, string msg) DeleteHall(int id)
    {
        try
        {
            using var conn = Open();
            using var cmd = new MySqlCommand("DELETE FROM halls WHERE hall_id=@id", conn);
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
            return (true, "Hall deleted.");
        }
        catch (MySqlException ex) when (ex.Number == 1451)
        {
            return (false, "Cannot delete: hall has existing bookings.");
        }
    }

    // ═══════════════════════════════════════════
    //  BOOKINGS  CRUD + Stored Procedure
    // ═══════════════════════════════════════════
    public List<HallBooking> GetBookings(string? search = null, string? status = null)
    {
        var list = new List<HallBooking>();
        using var conn = Open();
        var sql = @"SELECT hb.*, r.first_name, r.last_name, r.email, r.phone,
                           h.hall_name, h.location, h.rate_per_hour, h.capacity
                    FROM hall_bookings hb
                    JOIN residents r ON hb.resident_id = r.resident_id
                    JOIN halls h     ON hb.hall_id     = h.hall_id WHERE 1=1";
        if (!string.IsNullOrWhiteSpace(search))
            sql += " AND (hb.event_name LIKE @s OR r.first_name LIKE @s OR r.last_name LIKE @s OR h.hall_name LIKE @s)";
        if (!string.IsNullOrWhiteSpace(status))
            sql += " AND hb.status = @st";
        sql += " ORDER BY hb.event_date DESC, hb.start_time";

        using var cmd = new MySqlCommand(sql, conn);
        if (!string.IsNullOrWhiteSpace(search))
            cmd.Parameters.AddWithValue("@s", $"%{search}%");
        if (!string.IsNullOrWhiteSpace(status))
            cmd.Parameters.AddWithValue("@st", status);

        using var rdr = cmd.ExecuteReader();
        while (rdr.Read()) list.Add(MapBookingFull(rdr));
        return list;
    }

    public BookingDetailViewModel? GetBookingDetail(int id)
    {
        HallBooking? booking = null;
        using var conn = Open();
        using (var cmd = new MySqlCommand(@"
            SELECT hb.*, r.first_name, r.last_name, r.email, r.phone,
                   h.hall_name, h.location, h.rate_per_hour, h.capacity
            FROM hall_bookings hb
            JOIN residents r ON hb.resident_id = r.resident_id
            JOIN halls h     ON hb.hall_id     = h.hall_id
            WHERE hb.booking_id = @id", conn))
        {
            cmd.Parameters.AddWithValue("@id", id);
            using var rdr = cmd.ExecuteReader();
            if (rdr.Read()) booking = MapBookingFull(rdr);
        }
        if (booking == null) return null;

        var audits = new List<BookingAudit>();
        using (var cmd2 = new MySqlCommand(@"
            SELECT * FROM booking_audit WHERE booking_id=@id ORDER BY audit_timestamp DESC", conn))
        {
            cmd2.Parameters.AddWithValue("@id", id);
            using var rdr2 = cmd2.ExecuteReader();
            while (rdr2.Read())
                audits.Add(new BookingAudit
                {
                    AuditId        = rdr2.GetInt32("audit_id"),
                    BookingId      = rdr2.GetInt32("booking_id"),
                    Action         = rdr2.GetString("action"),
                    OldStatus      = rdr2.IsDBNull(rdr2.GetOrdinal("old_status")) ? null : rdr2.GetString("old_status"),
                    NewStatus      = rdr2.IsDBNull(rdr2.GetOrdinal("new_status")) ? null : rdr2.GetString("new_status"),
                    Details        = rdr2.IsDBNull(rdr2.GetOrdinal("details")) ? null : rdr2.GetString("details"),
                    AuditTimestamp = rdr2.GetDateTime("audit_timestamp")
                });
        }
        return new BookingDetailViewModel { Booking = booking, Audits = audits };
    }

    /// <summary>
    /// Calls MySQL Stored Procedure: SubmitReservation
    /// </summary>
    public SubmitReservationResult SubmitReservation(HallBooking b)
    {
        using var conn = Open();
        using var cmd = new MySqlCommand(@"
            CALL SubmitReservation(@rid, @hid, @ename, @edate, @stime, @etime, @bid, @msg);
            SELECT @bid AS booking_id, @msg AS message;", conn);
        cmd.Parameters.AddWithValue("@rid", b.ResidentId);
        cmd.Parameters.AddWithValue("@hid", b.HallId);
        cmd.Parameters.AddWithValue("@ename", b.EventName);
        cmd.Parameters.AddWithValue("@edate", b.EventDate.ToString("yyyy-MM-dd"));
        cmd.Parameters.AddWithValue("@stime", b.StartTime.ToString(@"hh\:mm\:ss"));
        cmd.Parameters.AddWithValue("@etime", b.EndTime.ToString(@"hh\:mm\:ss"));
        cmd.Parameters.AddWithValue("@bid", 0);
        cmd.Parameters.AddWithValue("@msg", "");

        using var rdr = cmd.ExecuteReader();
        // Skip CALL result set, advance to SELECT
        if (rdr.NextResult() && rdr.Read())
        {
            return new SubmitReservationResult
            {
                BookingId = rdr.IsDBNull(0) ? -1 : rdr.GetInt32(0),
                Message   = rdr.IsDBNull(1) ? "Unknown error" : rdr.GetString(1)
            };
        }
        // Fallback: try reading from the first result set
        return new SubmitReservationResult { BookingId = -1, Message = "No response from stored procedure." };
    }

    /// <summary>
    /// Calls MySQL Stored Function: CalculateRentalCharge
    /// </summary>
    public CalculateChargeResult CalculateCharge(int hallId, string startTime, string endTime)
    {
        try
        {
            using var conn = Open();
            using var cmd = new MySqlCommand(
                "SELECT CalculateRentalCharge(@hid, @st, @et) AS charge", conn);
            cmd.Parameters.AddWithValue("@hid", hallId);
            cmd.Parameters.AddWithValue("@st", startTime);
            cmd.Parameters.AddWithValue("@et", endTime);
            using var rdr = cmd.ExecuteReader();
            if (rdr.Read())
                return new CalculateChargeResult { Charge = rdr.GetDecimal("charge"), Success = true };
            return new CalculateChargeResult { Success = false, Error = "No result" };
        }
        catch (Exception ex)
        {
            return new CalculateChargeResult { Success = false, Error = ex.Message };
        }
    }

    /// <summary>
    /// Updates booking status — triggers trg_booking_status_audit automatically.
    /// </summary>
    public (bool ok, string msg) UpdateBookingStatus(int bookingId, string newStatus)
    {
        var valid = new[] { "Confirmed", "Cancelled", "Completed" };
        if (!valid.Contains(newStatus))
            return (false, "Invalid status.");
        try
        {
            using var conn = Open();
            using var cmd = new MySqlCommand(
                "UPDATE hall_bookings SET status=@st WHERE booking_id=@id", conn);
            cmd.Parameters.AddWithValue("@st", newStatus);
            cmd.Parameters.AddWithValue("@id", bookingId);
            cmd.ExecuteNonQuery();
            return (true, $"Booking #{bookingId} marked as {newStatus}. (Audit logged by trigger)");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    // ═══════════════════════════════════════════
    //  AUDIT LOG
    // ═══════════════════════════════════════════
    public List<BookingAudit> GetAuditLog()
    {
        var list = new List<BookingAudit>();
        using var conn = Open();
        using var cmd = new MySqlCommand(@"
            SELECT ba.*, hb.event_name, h.hall_name, r.first_name, r.last_name
            FROM booking_audit ba
            JOIN hall_bookings hb ON ba.booking_id = hb.booking_id
            JOIN halls h          ON hb.hall_id    = h.hall_id
            JOIN residents r      ON hb.resident_id = r.resident_id
            ORDER BY ba.audit_timestamp DESC", conn);
        using var rdr = cmd.ExecuteReader();
        while (rdr.Read())
            list.Add(new BookingAudit
            {
                AuditId        = rdr.GetInt32("audit_id"),
                BookingId      = rdr.GetInt32("booking_id"),
                Action         = rdr.GetString("action"),
                OldStatus      = rdr.IsDBNull(rdr.GetOrdinal("old_status")) ? null : rdr.GetString("old_status"),
                NewStatus      = rdr.IsDBNull(rdr.GetOrdinal("new_status")) ? null : rdr.GetString("new_status"),
                Details        = rdr.IsDBNull(rdr.GetOrdinal("details")) ? null : rdr.GetString("details"),
                AuditTimestamp = rdr.GetDateTime("audit_timestamp"),
                EventName      = rdr.GetString("event_name"),
                HallName       = rdr.GetString("hall_name"),
                ResidentFirstName = rdr.GetString("first_name"),
                ResidentLastName  = rdr.GetString("last_name")
            });
        return list;
    }

    // ═══════════════════════════════════════════
    //  MAPPERS
    // ═══════════════════════════════════════════
    private static HallBooking MapBooking(MySqlDataReader r) => new()
    {
        BookingId       = r.GetInt32("booking_id"),
        ResidentId      = r.GetInt32("resident_id"),
        HallId          = r.GetInt32("hall_id"),
        EventName       = r.GetString("event_name"),
        EventDate       = r.GetDateTime("event_date"),
        StartTime       = r.GetTimeSpan("start_time"),
        EndTime         = r.GetTimeSpan("end_time"),
        TotalCharge     = r.GetDecimal("total_charge"),
        Status          = r.GetString("status"),
        CreatedAt       = r.GetDateTime("created_at"),
        ResidentFirstName = HasCol(r, "first_name") ? r.GetString("first_name") : null,
        ResidentLastName  = HasCol(r, "last_name")  ? r.GetString("last_name")  : null,
        HallName          = HasCol(r, "hall_name")   ? r.GetString("hall_name")  : null
    };

    private static HallBooking MapBookingFull(MySqlDataReader r)
    {
        var b = MapBooking(r);
        b.ResidentEmail = HasCol(r, "email")        ? r.GetString("email") : null;
        b.ResidentPhone = HasCol(r, "phone")        ? r.GetString("phone") : null;
        b.HallLocation  = HasCol(r, "location")     ? r.GetString("location") : null;
        b.HallRate      = HasCol(r, "rate_per_hour") ? r.GetDecimal("rate_per_hour") : 0;
        b.HallCapacity  = HasCol(r, "capacity")      ? r.GetInt32("capacity") : 0;
        return b;
    }

    private static Hall MapHall(MySqlDataReader r) => new()
    {
        HallId      = r.GetInt32("hall_id"),
        HallName    = r.GetString("hall_name"),
        Location    = r.GetString("location"),
        Capacity    = r.GetInt32("capacity"),
        RatePerHour = r.GetDecimal("rate_per_hour"),
        Status      = r.GetString("status"),
        Description = r.IsDBNull(r.GetOrdinal("description")) ? null : r.GetString("description"),
        CreatedAt   = r.GetDateTime("created_at"),
        ActiveBookings = HasCol(r, "active_bookings") ? r.GetInt32("active_bookings") : 0
    };

    private static bool HasCol(MySqlDataReader r, string name)
    {
        for (int i = 0; i < r.FieldCount; i++)
            if (r.GetName(i).Equals(name, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }
}
