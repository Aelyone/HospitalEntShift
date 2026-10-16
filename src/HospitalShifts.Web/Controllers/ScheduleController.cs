using HospitalShifts.Application.Abstractions;
using HospitalShifts.Application.Dtos;
using HospitalShifts.Domain.Calendar;
using HospitalShifts.Domain.Common;
using HospitalShifts.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace HospitalShifts.Web.Controllers;

public sealed class ScheduleController : BaseController
{
    readonly IScheduleService _schedule;
    readonly ILedgerService _ledger;
    readonly TimeProvider _time;

    public ScheduleController(IScheduleService schedule, ILedgerService ledger, TimeProvider time)
    {
        _schedule = schedule;
        _ledger = ledger;
        _time = time;
    }

    (int Year, int Month) ResolveYearMonth(int y, int m) =>
        y >= 1300 && m is >= 1 and <= 12 ? (y, m) : Jalali.CurrentYearMonth(_time);

    IActionResult BackToBoard(int y, int m) => RedirectToAction(nameof(Index), new { y, m });

    IActionResult NeedConfirm(int y, int m)
    {
        TempData["err"] = "برای این اقدام باید گزینهٔ تأیید را علامت بزنید";
        return BackToBoard(y, m);
    }

    public async Task<IActionResult> Index(int y, int m, CancellationToken cancellationToken)
    {
        var (year, month) = ResolveYearMonth(y, m);
        return View(await _schedule.GetBoardAsync(year, month, cancellationToken));
    }

    [HttpPost]
    public Task<IActionResult> Generate(int y, int m, CancellationToken cancellationToken) =>
        TryAsync(async () =>
        {
            var report = await _schedule.GenerateAsync(y, m, Actor, cancellationToken);
            var gaps = report.Gaps.Sum(g => g.Needed - g.Filled);
            return $"چیدن خودکار انجام شد: {report.Added} تخصیص، {report.LeadersSet} سرشیفت"
                   + (gaps > 0 ? $"؛ کمبود نیرو در {gaps} جایگاه" : "")
                   + (report.LeaderMissing.Count > 0 ? $"؛ {report.LeaderMissing.Count} نوبت بدون سرشیفت" : "");
        }, () => BackToBoard(y, m));

    [HttpPost]
    public Task<IActionResult> Clear(int y, int m, bool confirm, CancellationToken cancellationToken)
    {
        if (!confirm) return Task.FromResult<IActionResult>(NeedConfirm(y, m));
        return TryAsync(async () =>
        {
            await _schedule.ClearMonthAsync(y, m, Actor, cancellationToken);
            return "برنامه ماه پاک شد";
        }, () => BackToBoard(y, m));
    }

    [HttpPost]
    public Task<IActionResult> Approve(int y, int m, bool confirm, CancellationToken cancellationToken)
    {
        if (!confirm) return Task.FromResult<IActionResult>(NeedConfirm(y, m));
        return TryAsync(async () =>
        {
            await _ledger.ApproveMonthAsync(y, m, Actor, cancellationToken);
            return "ماه تأیید شد";
        }, () => BackToBoard(y, m));
    }

    [HttpPost]
    public Task<IActionResult> Reopen(int y, int m, bool confirm, CancellationToken cancellationToken)
    {
        if (!confirm) return Task.FromResult<IActionResult>(NeedConfirm(y, m));
        return TryAsync(async () =>
        {
            await _ledger.ReopenMonthAsync(y, m, Actor, cancellationToken);
            return "ماه به پیش‌نویس برگشت";
        }, () => BackToBoard(y, m));
    }

    [HttpPost]
    public Task<IActionResult> Settle(int y, int m, bool confirm, CancellationToken cancellationToken)
    {
        if (!confirm) return Task.FromResult<IActionResult>(NeedConfirm(y, m));
        return TryAsync(async () =>
        {
            await _ledger.SettleMonthAsync(y, m, Actor, cancellationToken);
            return "ماه تسویه شد";
        }, () => RedirectToAction(nameof(Ledger), new { y, m }));
    }

    [HttpGet]
    public async Task<IActionResult> Slot(string date, string slot, CancellationToken cancellationToken)
    {
        if (!Jalali.TryParseIso(date, out var parsedDate) || !Slots.TryParse(slot, out var parsedSlot))
            return BadRequest();
        ViewBag.Date = parsedDate;
        ViewBag.Slot = parsedSlot;
        ViewBag.Context = await _schedule.GetContextAsync(cancellationToken);
        return View(await _schedule.GetSlotAssignmentsAsync(parsedDate, parsedSlot, cancellationToken));
    }

    [HttpPost]
    public async Task<IActionResult> Slot(string date, string slot, string? leader, CancellationToken cancellationToken)
    {
        if (!Jalali.TryParseIso(date, out var parsedDate) || !Slots.TryParse(slot, out var parsedSlot))
            return BadRequest();

        var cells = new List<SlotAssignmentInput>();
        foreach (var key in Request.Form.Keys.Where(k => k.StartsWith("p_", StringComparison.Ordinal)))
        {
            // نام فیلد: p_{locationId}_{roleCode}
            var parts = key.Split('_');
            if (parts.Length != 3 || !int.TryParse(parts[1], out var locationId)
                || !PersonKinds.TryParse(parts[2], out var role)) continue;
            int? personId = int.TryParse(Request.Form[key], out var value) && value > 0 ? value : null;
            cells.Add(new SlotAssignmentInput(locationId, role, personId));
        }

        var (year, month, _) = parsedDate.ToJalali();
        try
        {
            var warnings = await _schedule.SaveSlotAsync(parsedDate, parsedSlot, cells, leader, Actor, cancellationToken);
            TempData["ok"] = "ذخیره شد" + (warnings.Count > 0 ? "؛ " + string.Join("، ", warnings) : "");
            return BackToBoard(year, month);
        }
        catch (DomainException e)
        {
            TempData["err"] = e.Message;
            return RedirectToAction(nameof(Slot), new { date = Jalali.Iso(parsedDate), slot = parsedSlot.ToCode() });
        }
    }

    public async Task<IActionResult> Ledger(int y, int m, CancellationToken cancellationToken)
    {
        var (year, month) = ResolveYearMonth(y, m);
        var monthInfo = await _schedule.GetMonthAsync(year, month, cancellationToken);
        ViewBag.Month = monthInfo;
        ViewBag.Adjustments = await _ledger.GetAdjustmentsAsync(monthInfo.Id, cancellationToken);
        return View(await _ledger.GetLedgerAsync(year, month, cancellationToken));
    }

    [HttpPost]
    public Task<IActionResult> Adjust(int y, int m, int personId, int amount, string reason, CancellationToken cancellationToken) =>
        TryAsync(async () =>
        {
            await _ledger.AddAdjustmentAsync(y, m, personId, amount, reason ?? "", Actor, cancellationToken);
            return "بخشودگی/اصلاح ثبت شد";
        }, () => RedirectToAction(nameof(Ledger), new { y, m }));
}
