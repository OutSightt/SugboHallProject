using System.ComponentModel.DataAnnotations;

namespace HallBook.Models;

public class Resident
{
    public int ResidentId { get; set; }

    [Required, Display(Name = "First Name"), MaxLength(50)]
    public string FirstName { get; set; } = "";

    [Required, Display(Name = "Last Name"), MaxLength(50)]
    public string LastName { get; set; } = "";

    [Required, EmailAddress, MaxLength(100)]
    public string Email { get; set; } = "";

    [Required, Phone, MaxLength(20)]
    public string Phone { get; set; } = "";

    [MaxLength(200)]
    public string? Address { get; set; }

    public DateTime CreatedAt { get; set; }

    // Computed
    public string FullName => $"{FirstName} {LastName}";
    public int BookingCount { get; set; }
}

public class Hall
{
    public int HallId { get; set; }

    [Required, Display(Name = "Hall Name"), MaxLength(100)]
    public string HallName { get; set; } = "";

    [Required, MaxLength(200)]
    public string Location { get; set; } = "";

    [Required, Range(1, 10000)]
    public int Capacity { get; set; }

    [Required, Display(Name = "Rate per Hour"), Range(0.01, 100000)]
    public decimal RatePerHour { get; set; }

    [Required]
    public string Status { get; set; } = "Available";

    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; }
    public int ActiveBookings { get; set; }
}

public class HallBooking
{
    public int BookingId { get; set; }

    [Required, Display(Name = "Resident")]
    public int ResidentId { get; set; }

    [Required, Display(Name = "Hall")]
    public int HallId { get; set; }

    [Required, Display(Name = "Event Name"), MaxLength(100)]
    public string EventName { get; set; } = "";

    [Required, Display(Name = "Event Date"), DataType(DataType.Date)]
    public DateTime EventDate { get; set; } = DateTime.Today;

    [Required, Display(Name = "Start Time")]
    public TimeSpan StartTime { get; set; } = new(9, 0, 0);

    [Required, Display(Name = "End Time")]
    public TimeSpan EndTime { get; set; } = new(12, 0, 0);

    public decimal TotalCharge { get; set; }

    public string Status { get; set; } = "Pending";

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Joined fields
    public string? ResidentFirstName { get; set; }
    public string? ResidentLastName { get; set; }
    public string? ResidentEmail { get; set; }
    public string? ResidentPhone { get; set; }
    public string? HallName { get; set; }
    public string? HallLocation { get; set; }
    public decimal HallRate { get; set; }
    public int HallCapacity { get; set; }

    public string ResidentFullName => $"{ResidentFirstName} {ResidentLastName}";
}

public class BookingAudit
{
    public int AuditId { get; set; }
    public int BookingId { get; set; }
    public string Action { get; set; } = "";
    public string? OldStatus { get; set; }
    public string? NewStatus { get; set; }
    public string? Details { get; set; }
    public DateTime AuditTimestamp { get; set; }

    // Joined fields
    public string? EventName { get; set; }
    public string? HallName { get; set; }
    public string? ResidentFirstName { get; set; }
    public string? ResidentLastName { get; set; }
    public string ResidentFullName => $"{ResidentFirstName} {ResidentLastName}";
}

// ── View Models ──────────────────────────────

public class DashboardViewModel
{
    public int TotalResidents { get; set; }
    public int TotalHalls { get; set; }
    public int TotalBookings { get; set; }
    public int PendingBookings { get; set; }
    public int ConfirmedBookings { get; set; }
    public int CompletedBookings { get; set; }
    public List<HallBooking> RecentBookings { get; set; } = [];
}

public class BookingFormViewModel
{
    public HallBooking Booking { get; set; } = new();
    public List<Resident> Residents { get; set; } = [];
    public List<Hall> Halls { get; set; } = [];
}

public class BookingDetailViewModel
{
    public HallBooking Booking { get; set; } = new();
    public List<BookingAudit> Audits { get; set; } = [];
}

public class HallDetailViewModel
{
    public Hall Hall { get; set; } = new();
    public List<HallBooking> Bookings { get; set; } = [];
}

public class SubmitReservationResult
{
    public int BookingId { get; set; }
    public string Message { get; set; } = "";
    public bool Success => BookingId > 0;
}

public class CalculateChargeResult
{
    public decimal Charge { get; set; }
    public bool Success { get; set; }
    public string? Error { get; set; }
}
