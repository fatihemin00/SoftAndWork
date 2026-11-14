using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using SoftAndWork.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

// İŞTE BURAYI DEĞİŞTİRDİK: UseSqlite yerine UseSqlServer yaptık
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddHttpClient<SoftAndWork.Services.GeminiApiService>(client =>
{
    client.Timeout = System.Threading.Timeout.InfiniteTimeSpan;
});

// Cookie tabanlı kimlik doğrulama servisini ekliyoruz
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login"; // Giriş yapmamış kullanıcı buraya yönlendirilir
        options.LogoutPath = "/Account/Logout";
        options.Cookie.Name = "SoftAndWorkAuth";
        options.ExpireTimeSpan = TimeSpan.FromDays(7); // Oturum 7 gün açık kalır
    });

var builderApp = builder.Build();

using (var scope = builderApp.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

if (!builderApp.Environment.IsDevelopment())
{
    builderApp.UseExceptionHandler("/Home/Error");
    builderApp.UseHsts();
}

builderApp.UseHttpsRedirection();
builderApp.UseStaticFiles();

builderApp.UseRouting();

// Doğrulama ve yetkilendirme middleware'leri
builderApp.UseAuthentication();
builderApp.UseAuthorization();

builderApp.MapControllerRoute(
    name: "default",
    pattern: "{controller=Courses}/{action=Index}/{id?}");

builderApp.Run();