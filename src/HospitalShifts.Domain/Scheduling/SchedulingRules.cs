using HospitalShifts.Domain.Calendar;
using HospitalShifts.Domain.Enums;

namespace HospitalShifts.Domain.Scheduling;

/// <summary>زنجیره = نوبت‌های پشت‌سرهم یک فرد.</summary>
public sealed class Chain
{
    public List<AssignmentEntry> Rows { get; } = new();
    public long StartIndex { get; set; }
    public long EndIndex { get; set; }
    public long StartMinute { get; set; }
    public long EndMinute { get; set; }
    public string? TypeCode { get; set; }
    public int Units { get; set; }
    public DateOnly StartDate => Rows[0].Date;
}

/// <summary>قواعد دامنه: زنجیره‌ها، استراحت بین شیفت، اعتبارسنجی و پوشش نیرو.</summary>
public static class SchedulingRules
{
    public const int DefaultRestHours = 12;

    static long SlotIndex(DateOnly date, Slot slot) => date.DayNumber * 3L + slot.Index();
    static long SlotStartMinute(DateOnly date, Slot slot) => date.DayNumber * 1440L + slot.Minutes().Start;
    static long SlotEndMinute(DateOnly date, Slot slot) => date.DayNumber * 1440L + slot.Minutes().End;

    /// <summary>ساخت زنجیره‌های پیوسته از نوبت‌های یک فرد و تشخیص نوع شیفت هر زنجیره.</summary>
    public static List<Chain> BuildChains(IEnumerable<AssignmentEntry> rows)
    {
        var chains = new List<Chain>();
        Chain? current = null;
        foreach (var r in rows.OrderBy(r => SlotIndex(r.Date, r.Slot)))
        {
            var index = SlotIndex(r.Date, r.Slot);
            if (current != null && index == current.EndIndex + 1)
            {
                current.Rows.Add(r);
                current.EndIndex = index;
            }
            else
            {
                current = new Chain { StartIndex = index, EndIndex = index };
                current.Rows.Add(r);
                chains.Add(current);
            }
        }
        foreach (var c in chains)
        {
            c.TypeCode = ShiftDefinitions.CodeFor(c.Rows.Select(r => r.Slot));
            c.Units = c.Rows.Sum(r => r.Slot.Units());
            c.StartMinute = SlotStartMinute(c.Rows[0].Date, c.Rows[0].Slot);
            var last = c.Rows[^1];
            c.EndMinute = SlotEndMinute(last.Date, last.Slot);
        }
        return chains;
    }

    /// <summary>قواعد فردی: شیفت مجاز، زنجیره معتبر، صبح سربازان، تغییر محل، سرشیفت و استراحت.</summary>
    public static List<RuleViolation> ValidatePerson(List<AssignmentEntry> rows, PersonProfile person, int defaultRestHours)
    {
        var violations = new List<RuleViolation>();
        void Add(AssignmentEntry r, string message) =>
            violations.Add(new RuleViolation(message, r.Date, r.Slot, person.Id, r.LocationId));

        var chains = BuildChains(rows);
        foreach (var chain in chains)
        {
            var first = chain.Rows[0];
            if (chain.TypeCode is null)
                Add(first, $"ترکیب نوبت‌های {string.Join(" + ", chain.Rows.Select(r => r.Slot.ToLabel()))} از {chain.StartDate.ToJalaliLabel()} شیفت معتبری نیست");
            else if (!person.Allowed.Contains(chain.TypeCode))
                Add(first, $"شیفت «{ShiftDefinitions.ByCode[chain.TypeCode].Name}» در {chain.StartDate.ToJalaliLabel()} برای {person.Name} مجاز نیست");

            for (var i = 0; i < chain.Rows.Count; i++)
            {
                var r = chain.Rows[i];
                // تاریخ واقعی هر نوبت ملاک است (صبحِ بعد از شب، روز بعد است)
                if (person.Type == PersonKind.Soldier && r.Slot == Slot.Morning && !r.Date.IsFriday())
                    Add(r, $"سرباز {person.Name} شنبه تا پنجشنبه صبح در بخش خود است ({r.Date.ToJalaliLabel()})");
                if (i > 0 && chain.Rows[i - 1].LocationId == r.LocationId)
                    Add(r, $"در شیفت ترکیبی {chain.StartDate.ToJalaliLabel()} محل باید بین نوبت‌ها تغییر کند");
                if (r.IsLeader && (person.Type != PersonKind.Official || !person.CanLeader))
                    Add(r, $"{person.Name} صلاحیت سرشیفتی ندارد");
            }
        }
        for (var i = 1; i < chains.Count; i++)
        {
            var prev = chains[i - 1];
            var next = chains[i];
            var hours = prev.TypeCode is not null && person.Rest.TryGetValue(prev.TypeCode, out var h) ? h : defaultRestHours;
            if (next.StartMinute - prev.EndMinute < hours * 60L)
                Add(next.Rows[0], $"استراحت {person.Name} بین شیفت {prev.StartDate.ToJalaliLabel()} و {next.StartDate.ToJalaliLabel()} کافی نیست (حداقل {hours} ساعت)");
        }
        return violations;
    }

    /// <summary>ظرفیت، محل فعال، تعطیلی، نقش، سرشیفت و قواعد فردی — برای همه تخصیص‌های داده‌شده.</summary>
    public static List<RuleViolation> ValidateRows(IEnumerable<AssignmentEntry> rows, SchedulingContext ctx)
    {
        var violations = new List<RuleViolation>();
        var cellCount = new Dictionary<string, int>();
        var leaderCount = new Dictionary<string, int>();
        var personSlot = new HashSet<string>();
        var byPerson = new Dictionary<int, List<AssignmentEntry>>();

        foreach (var r in rows)
        {
            void Add(string message) => violations.Add(new RuleViolation(message, r.Date, r.Slot, r.PersonnelId, r.LocationId));

            if (!ctx.People.TryGetValue(r.PersonnelId, out var person)) { Add("فرد نامعتبر"); continue; }
            if (!ctx.Locations.TryGetValue(r.LocationId, out var loc)) { Add("محل نامعتبر"); continue; }
            if (!loc.Active) { Add($"محل «{loc.Name}» فعال نیست"); continue; }
            if (ctx.IsClosed(r.LocationId, r.Date)) Add($"محل «{loc.Name}» در {r.Date.ToJalaliLabel()} بسته است");
            if (person.Type != r.Role) Add($"{person.Name} برای جایگاه {r.Role.ToLabel()} نیست");

            var need = loc.Need(r.Slot, r.Role);
            if (need == 0) Add($"محل «{loc.Name}» در نوبت {r.Slot.ToLabel()} به {r.Role.ToLabel()} نیاز ندارد");

            var cellKey = CellKeys.Of(r.Date, r.Slot, r.LocationId, r.Role);
            cellCount[cellKey] = cellCount.GetValueOrDefault(cellKey) + 1;
            if (cellCount[cellKey] > need && need > 0) Add($"ظرفیت جایگاه «{loc.Name}» در نوبت {r.Slot.ToLabel()} تکمیل است");

            if (!personSlot.Add($"{r.PersonnelId}|{r.CellKey}")) Add($"{person.Name} در یک نوبت بیش از یک جایگاه دارد");

            if (r.IsLeader)
            {
                var leaderKey = r.CellKey;
                leaderCount[leaderKey] = leaderCount.GetValueOrDefault(leaderKey) + 1;
                if (leaderCount[leaderKey] > 1) Add($"بیش از یک سرشیفت در نوبت {r.Slot.ToLabel()} {r.Date.ToJalaliLabel()}");
            }

            if (!byPerson.TryGetValue(r.PersonnelId, out var list)) byPerson[r.PersonnelId] = list = new();
            list.Add(r);
        }
        foreach (var (personId, personRows) in byPerson)
            violations.AddRange(ValidatePerson(personRows, ctx.People[personId], ctx.DefaultRestHours));
        return violations;
    }

    /// <summary>جایگاه‌های کم‌پُر و نوبت‌های بدون سرشیفت در بازه داده‌شده.</summary>
    public static (List<CoverageGap> Gaps, List<SlotKey> LeaderMissing)
        Coverage(List<AssignmentEntry> monthRows, IEnumerable<DateOnly> dates, SchedulingContext ctx)
    {
        var gaps = new List<CoverageGap>();
        var missing = new List<SlotKey>();
        var filled = monthRows
            .GroupBy(r => CellKeys.Of(r.Date, r.Slot, r.LocationId, r.Role))
            .ToDictionary(g => g.Key, g => g.Count());
        var led = monthRows.Where(r => r.IsLeader).Select(r => r.CellKey).ToHashSet();

        foreach (var date in dates)
            foreach (var slot in Slots.All)
            {
                foreach (var loc in ctx.Locations.Values)
                {
                    if (!loc.Active || ctx.IsClosed(loc.Id, date)) continue;
                    foreach (var role in PersonKinds.All)
                    {
                        var need = loc.Need(slot, role);
                        if (need == 0) continue;
                        var fill = filled.GetValueOrDefault(CellKeys.Of(date, slot, loc.Id, role));
                        if (fill < need) gaps.Add(new CoverageGap(date, slot, loc.Id, loc.Name, role, need, fill));
                    }
                }
                if (!led.Contains(CellKeys.Of(date, slot))) missing.Add(new SlotKey(date, slot));
            }
        return (gaps, missing);
    }
}
