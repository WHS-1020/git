using Microsoft.EntityFrameworkCore;
using RollerMillMonitor.Models;

namespace RollerMillMonitor.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Plc> Plcs => Set<Plc>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<Point> Points => Set<Point>();
    public DbSet<AlarmRecord> Alarms => Set<AlarmRecord>();
    public DbSet<TrendRecord> Trends => Set<TrendRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // 设备 -> 测点（删除设备级联删除测点）
        modelBuilder.Entity<Point>()
            .HasOne(p => p.Device)
            .WithMany(d => d.Points)
            .HasForeignKey(p => p.DeviceId)
            .OnDelete(DeleteBehavior.Cascade);

        // 测点 -> PLC（删除 PLC 后测点 PlcId 置空，界面显示"未配置"，不丢失测点）
        modelBuilder.Entity<Point>()
            .HasOne(p => p.Plc)
            .WithMany()
            .HasForeignKey(p => p.PlcId)
            .OnDelete(DeleteBehavior.SetNull);

        // 报警 -> 测点（删除测点后相关报警一并删除，处理干净）
        modelBuilder.Entity<AlarmRecord>()
            .HasOne(a => a.Point)
            .WithMany()
            .HasForeignKey(a => a.PointId)
            .OnDelete(DeleteBehavior.Cascade);

        // 历史趋势 -> 测点（删除测点后相关历史数据一并删除）
        modelBuilder.Entity<TrendRecord>()
            .HasOne<Point>()
            .WithMany()
            .HasForeignKey(t => t.PointId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Point>()
            .HasIndex(p => new { p.DeviceId, p.PlcId });

        modelBuilder.Entity<Point>()
            .HasIndex(p => new { p.DeviceId, p.Name })
            .IsUnique(false);

        modelBuilder.Entity<TrendRecord>()
            .HasIndex(t => new { t.PointId, t.Timestamp });

        modelBuilder.Entity<AlarmRecord>()
            .HasIndex(a => new { a.IsActive, a.AlarmTime });

        modelBuilder.Entity<AlarmRecord>()
            .HasIndex(a => new { a.PointId, a.AlarmType, a.IsActive });
    }
}
