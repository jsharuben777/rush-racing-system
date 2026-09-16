using Microsoft.EntityFrameworkCore;
using RushRacing.Core.Interfaces;
using RushRacing.Data;
using RushRacing.Data.Services;
using RushRacing.Web.BackgroundServices;
using RushRacing.Web.Hubs;
using Microsoft.AspNetCore.Authentication;

var builder = WebApplication.CreateBuilder(args);

// MVC
builder.Services.AddControllersWithViews();

//// Database
//builder.Services.AddDbContext<RushRacingDbContext>(options =>
//    options.UseSqlServer(
//        builder.Configuration.GetConnectionString("DefaultConnection")));

//builder.Services.AddDbContext<RushRacingDbContext>(options =>
//    options.UseInMemoryDatabase("RushRacing"));

builder.Services.AddDbContext<RushRacingDbContext>(options =>
    options.UseInMemoryDatabase("RushRacing")
           .ConfigureWarnings(w =>
               w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning)));

// Services
builder.Services.AddScoped<ISessionService, SessionService>();
builder.Services.AddScoped<IMachineService, MachineService>();
builder.Services.AddScoped<ISessionEventService, SessionEventService>();
builder.Services.AddScoped<IAuditService, AuditService>();

// SignalR (we will add the hub later)
builder.Services.AddSignalR();

// Background services
builder.Services.AddHostedService<SessionCleanupService>();

// Authentication
builder.Services.AddAuthentication("AdminCookie")
    .AddCookie("AdminCookie", options =>
    {
        options.LoginPath = "/admin/login";
        options.LogoutPath = "/admin/logout";
        options.ExpireTimeSpan = TimeSpan.FromHours(12);
        options.Cookie.Name = "RushRacing.Admin";
    });

var app = builder.Build();

// Seed data in development
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<RushRacingDbContext>();
    await SeedData.SeedAsync(db);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

//app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Map SignalR hub
app.MapHub<MachineHub>("/hub/machine");

app.Run();