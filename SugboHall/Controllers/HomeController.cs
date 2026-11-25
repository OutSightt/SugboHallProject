using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HallBook.Data;
using HallBook.Models;

namespace HallBook.Controllers;

public class HomeController : Controller
{
    private readonly HallBookDb _db;
    public HomeController(HallBookDb db) => _db = db;

    public IActionResult Index()
    {
        // Public landing page. If MySQL is not ready yet, the page still opens (empty lists).
        try
        {
            ViewBag.Halls = _db.GetHalls(status: "Available");
            ViewBag.Events = _db.GetBookings(status: "Confirmed")
                .Where(b => b.EventDate.Date >= DateTime.Today)
                .OrderBy(b => b.EventDate).ThenBy(b => b.StartTime)
                .Take(6).ToList();
        }
        catch
        {
            ViewBag.Halls = new List<Hall>();
            ViewBag.Events = new List<HallBooking>();
            ViewBag.DbError = true;
        }
        return View();
    }
}

// ═══════════════════════════════════════════
//  RESIDENTS CONTROLLER
// ═══════════════════════════════════════════
[Authorize]
public class ResidentsController : Controller
{
    private readonly HallBookDb _db;
    public ResidentsController(HallBookDb db) => _db = db;

    public IActionResult Index(string? search)
    {
        ViewBag.Search = search;
        return View(_db.GetResidents(search));
    }

    public IActionResult Create() => View(new Resident());

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Create(Resident r)
    {
        if (!ModelState.IsValid) return View(r);
        var (ok, msg) = _db.CreateResident(r);
        TempData[ok ? "Success" : "Error"] = msg;
        return ok ? RedirectToAction("Index") : View(r);
    }

    public IActionResult Edit(int id)
    {
        var r = _db.GetResident(id);
        if (r == null) { TempData["Error"] = "Resident not found."; return RedirectToAction("Index"); }
        return View(r);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Edit(Resident r)
    {
        if (!ModelState.IsValid) return View(r);
        var (ok, msg) = _db.UpdateResident(r);
        TempData[ok ? "Success" : "Error"] = msg;
        return ok ? RedirectToAction("Index") : View(r);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Delete(int id)
    {
        var (ok, msg) = _db.DeleteResident(id);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction("Index");
    }
}

// ═══════════════════════════════════════════
//  HALLS CONTROLLER
// ═══════════════════════════════════════════
[Authorize]
public class HallsController : Controller
{
    private readonly HallBookDb _db;
    public HallsController(HallBookDb db) => _db = db;

    public IActionResult Index(string? search, string? status)
    {
        ViewBag.Search = search;
        ViewBag.StatusFilter = status;
        return View(_db.GetHalls(search, status));
    }

    public IActionResult Details(int id)
    {
        var vm = _db.GetHallDetail(id);
        if (vm == null) { TempData["Error"] = "Hall not found."; return RedirectToAction("Index"); }
        return View(vm);
    }

    public IActionResult Create() => View(new Hall());

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Create(Hall h)
    {
        if (!ModelState.IsValid) return View(h);
        var (ok, msg) = _db.CreateHall(h);
        TempData[ok ? "Success" : "Error"] = msg;
        return ok ? RedirectToAction("Index") : View(h);
    }

    public IActionResult Edit(int id)
    {
        var h = _db.GetHall(id);
        if (h == null) { TempData["Error"] = "Hall not found."; return RedirectToAction("Index"); }
        return View(h);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Edit(Hall h)
    {
        if (!ModelState.IsValid) return View(h);
        var (ok, msg) = _db.UpdateHall(h);
        TempData[ok ? "Success" : "Error"] = msg;
        return ok ? RedirectToAction("Index") : View(h);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Delete(int id)
    {
        var (ok, msg) = _db.DeleteHall(id);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction("Index");
    }
}

// ═══════════════════════════════════════════
//  BOOKINGS CONTROLLER
// ═══════════════════════════════════════════
[Authorize]
public class BookingsController : Controller
{
    private readonly HallBookDb _db;
    public BookingsController(HallBookDb db) => _db = db;

    public IActionResult Index(string? search, string? status)
    {
        ViewBag.Search = search;
        ViewBag.StatusFilter = status;
        return View(_db.GetBookings(search, status));
    }

    public IActionResult Details(int id)
    {
        var vm = _db.GetBookingDetail(id);
        if (vm == null) { TempData["Error"] = "Booking not found."; return RedirectToAction("Index"); }
        return View(vm);
    }

    public IActionResult Create(int? hall_id)
    {
        var vm = new BookingFormViewModel
        {
            Residents = _db.GetResidents(),
            Halls = _db.GetHalls(status: "Available")
        };
        if (hall_id.HasValue) vm.Booking.HallId = hall_id.Value;
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Create(BookingFormViewModel vm)
    {
        // Call Stored Procedure: SubmitReservation
        var result = _db.SubmitReservation(vm.Booking);
        if (result.Success)
        {
            TempData["Success"] = $"[Stored Procedure: SubmitReservation] {result.Message}";
            return RedirectToAction("Details", new { id = result.BookingId });
        }
        TempData["Error"] = $"[Stored Procedure Validation] {result.Message}";
        vm.Residents = _db.GetResidents();
        vm.Halls = _db.GetHalls(status: "Available");
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult UpdateStatus(int id, string newStatus)
    {
        // This UPDATE triggers trg_booking_status_audit automatically
        var (ok, msg) = _db.UpdateBookingStatus(id, newStatus);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction("Details", new { id });
    }

    // AJAX endpoint → calls Stored Function: CalculateRentalCharge
    [HttpGet]
    public IActionResult CalculateCharge(int hallId, string startTime, string endTime)
    {
        var result = _db.CalculateCharge(hallId, startTime, endTime);
        return Json(result);
    }
}

// ═══════════════════════════════════════════
//  AUDIT CONTROLLER
// ═══════════════════════════════════════════
[Authorize]
public class AuditController : Controller
{
    private readonly HallBookDb _db;
    public AuditController(HallBookDb db) => _db = db;

    public IActionResult Index() => View(_db.GetAuditLog());
}
