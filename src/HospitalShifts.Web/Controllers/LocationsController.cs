using HospitalShifts.Application.Abstractions;
using HospitalShifts.Application.Forms;
using HospitalShifts.Domain.Calendar;
using HospitalShifts.Domain.Common;
using Microsoft.AspNetCore.Mvc;

namespace HospitalShifts.Web.Controllers;

public sealed class LocationsController : BaseController
{
    readonly ILocationService _locations;

    public LocationsController(ILocationService locations) => _locations = locations;

    public async Task<IActionResult> Index(CancellationToken cancellationToken) =>
        View(await _locations.GetAllAsync(cancellationToken));

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        ViewBag.Closures = id > 0
            ? await _locations.GetClosureDatesAsync(id, cancellationToken)
            : new List<DateOnly>();
        return View(id > 0 ? await _locations.GetForEditAsync(id, cancellationToken) : new LocationForm());
    }

    [HttpPost]
    public async Task<IActionResult> Edit(LocationForm form, CancellationToken cancellationToken)
    {
        try
        {
            var id = await _locations.SaveAsync(form, Actor, cancellationToken);
            TempData["ok"] = "ذخیره شد";
            return RedirectToAction(nameof(Edit), new { id });
        }
        catch (DomainException e)
        {
            ViewBag.Err = e.Message;
            ViewBag.Closures = form.Id > 0
                ? await _locations.GetClosureDatesAsync(form.Id, cancellationToken)
                : new List<DateOnly>();
            return View(form);
        }
    }

    [HttpPost]
    public Task<IActionResult> Active(int id, bool active, CancellationToken cancellationToken) =>
        TryAsync(async () =>
        {
            await _locations.SetActiveAsync(id, active, Actor, cancellationToken);
            return active ? "فعال شد" : "غیرفعال شد";
        }, () => RedirectToAction(nameof(Index)));

    [HttpPost]
    public Task<IActionResult> AddClosure(int id, string date, CancellationToken cancellationToken) =>
        TryAsync(async () =>
        {
            var parsed = Jalali.ParseJalaliText(date)
                ?? throw new DomainException("تاریخ شمسی نامعتبر (نمونه: 1404/01/15)");
            var removed = await _locations.AddClosureAsync(id, parsed, Actor, cancellationToken);
            return $"تعطیلی ثبت شد؛ {removed} تخصیص حذف شد";
        }, () => RedirectToAction(nameof(Edit), new { id }));

    [HttpPost]
    public Task<IActionResult> RemoveClosure(int id, string date, CancellationToken cancellationToken) =>
        TryAsync(async () =>
        {
            if (!Jalali.TryParseIso(date, out var parsed)) throw new DomainException("تاریخ نامعتبر است");
            await _locations.RemoveClosureAsync(id, parsed, Actor, cancellationToken);
            return "تعطیلی حذف شد";
        }, () => RedirectToAction(nameof(Edit), new { id }));
}
