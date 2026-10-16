using HospitalShifts.Application;
using HospitalShifts.Infrastructure;
using HospitalShifts.Infrastructure.Persistence;
using HospitalShifts.Web;
using HospitalShifts.Web.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddWebServices();

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var initializer = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
    await initializer.InitializeAsync();
}

app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseMiddleware<ForcePasswordChangeMiddleware>();
app.UseAuthorization();
app.MapControllerRoute("default", "{controller=Schedule}/{action=Index}/{id?}");
app.Run();
