using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Monexia.Data;
using Monexia.Helpers;
using Monexia.Models;
using Monexia.Services;
using OpenAI.Extensions;
using QuestPDF.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// QuestPDF Community License
QuestPDF.Settings.License = LicenseType.Community;

// OpenAI ApiKey'i User Secrets'dan okuma ve servisi kaydetme
var openAiApiKey = builder.Configuration["OpenAI:ApiKey"];
builder.Services.AddOpenAIService(settings => { settings.ApiKey = openAiApiKey; });

// Kendi özel servislerimizi sisteme tanıtma
builder.Services.AddScoped<IOpenAiService, OpenAiService>();
builder.Services.AddScoped<IReportService, ReportService>();


// Add services to the container.
builder.Services.AddControllersWithViews();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("MonexiaDB")));

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options => {
    options.SignIn.RequireConfirmedAccount = false;
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 8;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
})
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders()
    .AddDefaultUI();

builder.Services.AddHttpClient();
builder.Services.AddMemoryCache();


var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Landing}/{id?}");

app.Run();
