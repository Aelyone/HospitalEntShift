<div dir="rtl">

# سامانه شیفت انتظامات — نسخه بازنویسی‌شده (Clean Architecture)

بازنویسی کامل پروژه `HospitalShifts` با معماری تمیز چهارلایه، EF Core اصولی و تزریق وابستگی درست.
**سیستم‌عامل اجرا همان .NET 8 و دیتابیس همان SQL Server است و با دیتابیس فعلی شما سازگار است** (همان جدول‌ها، ستون‌ها، ایندکس‌ها و قیود؛ بدون نیاز به تغییر دیتابیس).

## ساختار

```
src/
├── HospitalShifts.Domain/          # قلب برنامه؛ بدون هیچ وابستگی خارجی
│   ├── Common/DomainException.cs   # خطای قاعده کسب‌وکار
│   ├── Enums/                      # PersonKind, Slot, MonthStatus (+ کد/برچسب فارسی)
│   ├── Calendar/Jalali.cs          # تقویم جلالی روی DateOnly
│   ├── Entities/                   # انتیتی‌های دامنه
│   └── Scheduling/                 # قواعد چیدمان: زنجیره، استراحت، اعتبارسنجی، پوشش
│
├── HospitalShifts.Application/     # موارد استفاده (Use-Cases)
│   ├── Abstractions/               # IApplicationDbContext + اینترفیس سرویس‌ها
│   ├── Dtos/                       # مدل‌های خروجی (MonthDto, ScheduleBoardDto, ...)
│   ├── Forms/                      # مدل‌های ورودی فرم
│   ├── Security/PasswordHasher.cs  # PBKDF2 (قابل تعویض از طریق IPasswordHasher)
│   └── Services/                   # Auth, Personnel, Location, Schedule, Ledger, Settings, Audit
│
├── HospitalShifts.Infrastructure/  # EF Core و دیتابیس
│   └── Persistence/
│       ├── ApplicationDbContext.cs # پیاده‌سازی IApplicationDbContext + تراکنش
│       ├── Configurations/         # کانفیگ Fluent جداگانه برای هر انتیتی
│       ├── Converters/             # DateOnly↔ISO string، Enum↔code، DateTimeOffset↔ISO
│       └── DatabaseInitializer.cs  # EnsureCreated + Seed اولیه
│
└── HospitalShifts.Web/             # لایه نمایش MVC
    ├── Controllers/                # کنترلرهای نازک (فقط هماهنگی)
    ├── Middleware/                 # هدرهای امنیتی + اجبار تغییر رمز
    ├── Views/                      # Razor Views
    └── Program.cs                  # نقطه ترکیب (Composition Root)
```

## تزریق وابستگی (DI)

| سرویس | اینترفیس | طول‌عمر |
|---|---|---|
| `TimeProvider.System` | `TimeProvider` | Singleton |
| `PasswordHasher` | `IPasswordHasher` | Singleton |
| `ApplicationDbContext` | `IApplicationDbContext` | Scoped |
| Auth / Personnel / Location / Schedule / Ledger / Settings / Audit | اینترفیس مخصوص هرکدام | Scoped |

کنترلرها فقط به اینترفیس‌ها وابسته‌اند؛ ثبت‌ها در `AddApplication()`، `AddInfrastructure()` و `AddWebServices()` انجام می‌شود.

## اصول EF Core به‌کاررفته

- **کانفیگ جداگانه** با `IEntityTypeConfiguration` به‌جای `OnModelCreating` ۲۰۰ خطی؛ اسم ستون‌ها صریح است (به‌جای حلقه snake_case جادویی).
- **همه کوئری‌ها async** با `CancellationToken` و پروژکشن به DTO؛ `AsNoTracking` برای خواندن‌ها.
- **Value Converter** برای `DateOnly` (ذخیره به‌صورت `nvarchar(10)` با قالب `yyyy-MM-dd`) و enumها (ذخیره به‌صورت کدهای قبلی `official/morning/draft`) — پس مدل دامنه تایپ‌دار است اما **دیتابیس هیچ تغییری نکرده**.
- تراکنش سطح سرویس با `ExecuteInTransactionAsync` (Commit/Rollback خودکار + Execution Strategy)؛ رول‌بک کامل همراه با `ChangeTracker.Clear()`.
- حذف **`AsEnumerable()` وسط کوئری** (کلاینت-EVAL) که باعث می‌شد کل جدول به حافظه بیاید.

## رفع مشکلات نسخه قبلی

1. **پروژه قبلی کامپایل نمی‌شد**: `LedgerOps` به `ScheduleMonths`/`MonthlyLedgers`/`LedgerAdjustments` اشاره می‌کرد که روی `AppDb` وجود نداشتند (نام‌هایش `Months`/`Ledgers`/`Adjustments` بود). — برطرف شد.
2. **دو لایه داده موازی**: کد خام ADO (`Db.cs`/`Conn`/`Row`/`Schema.cs`) مرده بود؛ حذف شد.
3. **سرویس خدا با partial class در ۴ فایل** → دو سرویس منسجم `ScheduleService` و `LedgerService` با مسئولیت روشن.
4. **متدهای static با پارامتر DbContext** → کوئری‌های مشترک در `SchedulingQueries` داخلی لایه Application.
5. **تراکنش داخل DbContext + قفل سراسری Semaphore** → تراکنش سطح سرویس؛ SQL Server خود همروندی را مدیریت می‌کند (سیستم تک‌کاربره است).
6. **Primitive Obsession**: تاریخ به‌صورت string و مقایسه با `CompareOrdinal` → `DateOnly`؛ رشته‌های جادویی `official/morning/...` → enum با برچسب فارسی.
7. **مدل‌های دوتایی دامنه** (`Assign/Person/Loc` در مقابل انتیتی‌ها) → مدل‌های خواندنی یکپارچه در `Domain.Scheduling`.
8. **از کار افتادن DI**: سرویس‌ها از کانستراکتور تزریق می‌شوند؛ Rate-Limit ورود به‌جای دیکشنری static با `IMemoryCache`.
9. کنترلرها async شدند و پارس دستی فرم تمیزتر شد؛ میدلورها به کلاس مستقل منتقل شدند.
10. **بهداشت ریپو**: `.vs/`، `App_Data/hospital.db` (بقایای SQLite) و فولدر Migrations بلااستفاده حذف شد و `.gitignore` اضافه شد.

### تغییرات رفتاری کوچک (آگاهانه)

- دکمه «تأیید نهایی ماه» حالا واقعاً تیک «مطمئنم» را هم سمت سرور چک می‌کند (قبلاً چک باکس در UI بود ولی سرور نادیده‌اش می‌گرفت).
- مقدار `settled_at` و `created_at` حالا با قالب ISO استاندارد `DateTimeOffset` ذخیره/خوانده می‌شود (مقادیر قدیمی هم خوانده می‌شود).
- `ChainTypeMap` و `Forgive` (متدهای بدون استفاده) حذف شدند.

## اجرا

```bash
dotnet restore
dotnet run --project src/HospitalShifts.Web
```

- رشته اتصال در `src/HospitalShifts.Web/appsettings.json` (مانند قبل: `ConnectionStrings:Default`).
- در اولین اجرا دیتابیس و جدول‌ها ساخته و کاربر `admin` با رمز `1234` (تغییر اجباری) ایجاد می‌شود.
- **اگر دیتابیس فعلی را دارید**: نیازی به هیچ کاری نیست؛ اسکیما عیناً همان است.

</div>
