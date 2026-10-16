using HospitalShifts.Application.Abstractions;
using HospitalShifts.Application.Forms;
using HospitalShifts.Domain.Common;
using Microsoft.AspNetCore.Mvc;

namespace HospitalShifts.Web.Controllers;

public sealed class PersonnelController : BaseController
{
    readonly IPersonnelService _personnel;
    readonly ISettingsService _settings;
    readonly IAuditService _audit;

    public PersonnelController(IPersonnelService personnel, ISettingsService settings, IAuditService audit)
    {
        _personnel = personnel;
        _settings = settings;
        _audit = audit;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        ViewBag.DefaultRest = await _settings.GetDefaultRestHoursAsync(cancellationToken);
        return View(await _personnel.GetAllAsync(cancellationToken));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken) =>
        View(id > 0
            ? await _personnel.GetForEditAsync(id, cancellationToken)
            : new PersonForm { Allowed = new() { "morning", "afternoon", "night" } });

    [HttpPost]
    public async Task<IActionResult> Edit(PersonForm form, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _personnel.SaveAsync(form, Actor, cancellationToken);
            TempData["ok"] = "ذخیره شد" + (result.Warnings.Count > 0 ? "؛ هشدار: " + string.Join("، ", result.Warnings) : "");
            return RedirectToAction(nameof(Index));
        }
        catch (DomainException e)
        {
            ViewBag.Err = e.Message;
            return View(form);
        }
    }

    [HttpPost]
    public Task<IActionResult> Active(int id, bool active, bool confirm, CancellationToken cancellationToken)
    {
        if (!active && !confirm)
        {
            TempData["err"] = "برای غیرفعال‌سازی گزینهٔ تأیید را بزنید";
            return Task.FromResult<IActionResult>(RedirectToAction(nameof(Index)));
        }
        return TryAsync(async () =>
        {
            var removed = await _personnel.SetActiveAsync(id, active, Actor, cancellationToken);
            return active ? "فعال شد" : $"غیرفعال شد؛ {removed} تخصیص آینده حذف شد";
        }, () => RedirectToAction(nameof(Index)));
    }

    [HttpPost]
    public Task<IActionResult> DefaultRest(int hours, CancellationToken cancellationToken) =>
        TryAsync(async () =>
        {
            await _settings.SetDefaultRestHoursAsync(hours, Actor, cancellationToken);
            return "استراحت پیش‌فرض ذخیره شد";
        }, () => RedirectToAction(nameof(Index)));

    public async Task<IActionResult> Audit(CancellationToken cancellationToken) =>
        View(await _audit.GetRecentAsync(200, cancellationToken));
}
