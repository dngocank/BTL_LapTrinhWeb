using BTL_PTTKYC.Data;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddHttpContextAccessor();

// Register Repositories
builder.Services.AddScoped<ProductRepository>();
builder.Services.AddScoped<OrderRepository>();
builder.Services.AddScoped<ReviewRepository>();
builder.Services.AddScoped<AccountRepository>();
builder.Services.AddScoped<WarehouseRepository>();
builder.Services.AddScoped<CustomerRepository>();
builder.Services.AddScoped<PromotionRepository>();
builder.Services.AddScoped<WarrantyRepository>();

// Register Custom Business Services (Shipping Calculator & Notification Engine)
builder.Services.AddScoped<BTL_PTTKYC.Services.IShippingService, BTL_PTTKYC.Services.ShippingService>();
builder.Services.AddScoped<BTL_PTTKYC.Services.INotificationService, BTL_PTTKYC.Services.NotificationService>();

// Add Distributed Memory Cache & Session for Cart
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(2);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Add Authentication with Cookie Scheme
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/admin/login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.Events.OnRedirectToLogin = context =>
        {
            if (context.Request.Path.StartsWithSegments("/Customer") || context.Request.Path.StartsWithSegments("/Cart"))
            {
                context.Response.Redirect("/Customer/Login?ReturnUrl=" + Uri.EscapeDataString(context.Request.Path + context.Request.QueryString));
            }
            else
            {
                context.Response.Redirect("/admin/login?ReturnUrl=" + Uri.EscapeDataString(context.Request.Path + context.Request.QueryString));
            }
            return Task.CompletedTask;
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
    options.AddPolicy("SalesOrAdmin", policy => policy.RequireRole("Admin", "Sales"));
    options.AddPolicy("WarehouseOrAdmin", policy => policy.RequireRole("Admin", "Warehouse"));
    options.AddPolicy("TechnicianOrAdmin", policy => policy.RequireRole("Admin", "Technician"));
    options.AddPolicy("CustomerOnly", policy => policy.RequireRole("Customer"));
});

var app = builder.Build();

// Run DB Initializer to seed columns and BCrypt hashes
DbInitializer.Initialize(app.Configuration);

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

// Dedicated route for customer
app.MapControllerRoute(
    name: "customer_login",
    pattern: "customer/login",
    defaults: new { controller = "Customer", action = "Login" });

app.MapControllerRoute(
    name: "customer_register",
    pattern: "customer/register",
    defaults: new { controller = "Customer", action = "Register" });

// Dedicated route for /admin/login
app.MapControllerRoute(
    name: "admin_login",
    pattern: "admin/login",
    defaults: new { controller = "Account", action = "Login" });

app.MapControllerRoute(
    name: "login",
    pattern: "login",
    defaults: new { controller = "Account", action = "Login" });

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
