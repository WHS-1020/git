using Microsoft.EntityFrameworkCore;
using RollerMillMonitor.Api;
using RollerMillMonitor.Data;
using RollerMillMonitor.Hubs;
using RollerMillMonitor.PlcDrivers;
using RollerMillMonitor.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseSqlite(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(o =>
    {
        // 统一错误格式：{ success: false, message, field }
        o.InvalidModelStateResponseFactory = ctx =>
        {
            var first = ctx.ModelState.Values
                .SelectMany(v => v.Errors)
                .FirstOrDefault();
            return new Microsoft.AspNetCore.Mvc.BadRequestObjectResult(
                ApiResult.Fail(first?.ErrorMessage ?? "请求参数错误"));
        };
    });

builder.Services.AddSignalR();

// CORS：允许任意来源（localhost / 127.0.0.1 / 局域网 IP 访问），SignalR 需要 AllowCredentials
builder.Services.AddCors(o => o.AddPolicy("MonitorCors", p =>
    p.SetIsOriginAllowed(_ => true)
     .AllowAnyHeader()
     .AllowAnyMethod()
     .AllowCredentials()));

// 单例服务：实时缓存 / 报警引擎 / 趋势 / 总览 / 驱动工厂 / 导入导出
builder.Services.AddSingleton<RealTimeCache>();
builder.Services.AddSingleton<AlarmEngine>();
builder.Services.AddSingleton<TrendService>();
builder.Services.AddSingleton<OverviewService>();
builder.Services.AddSingleton<PlcDriverFactory>();
builder.Services.AddSingleton<ConfigService>();

// 后台采集服务
builder.Services.AddHostedService<CollectionService>();

// API 文档（便于测试与联调）
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// 数据库迁移 + 初始化数据（首次启动自动执行，页面刷新配置不丢失）
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    Directory.CreateDirectory(Path.Combine(app.Environment.ContentRootPath, "Data"));
    db.Database.Migrate();
    DbInitializer.Seed(db);
}

// 全局异常处理：记录日志并返回统一错误格式
app.Use(async (ctx, next) =>
{
    try
    {
        await next();
    }
    catch (Exception ex)
    {
        var logger = ctx.RequestServices.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "请求处理异常：{method} {path}", ctx.Request.Method, ctx.Request.Path);

        if (ctx.Response.HasStarted)
        {
            throw;
        }

        ctx.Response.StatusCode = 500;
        ctx.Response.ContentType = "application/json; charset=utf-8";
        await ctx.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(
            ApiResult.Fail($"服务器内部错误：{ex.Message}")));
    }
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseCors("MonitorCors");

app.MapControllers();
app.MapHub<MonitorHub>("/hubs/monitor");

app.Logger.LogInformation("轧机监控平台已启动：{url}", builder.Configuration["Urls"] ?? "http://localhost:5200");
app.Run();
