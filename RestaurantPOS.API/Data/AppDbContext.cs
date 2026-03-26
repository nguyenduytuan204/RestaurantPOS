using Microsoft.EntityFrameworkCore;
using RestaurantPOS.API.Models;

namespace RestaurantPOS.API.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Area>          Areas          { get; set; }
    public DbSet<DiningTable>   DiningTables   { get; set; }
    public DbSet<Category>      Categories     { get; set; }
    public DbSet<Product>       Products       { get; set; }
    public DbSet<Order>         Orders         { get; set; }
    public DbSet<OrderDetail>   OrderDetails   { get; set; }
    public DbSet<PaymentMethod> PaymentMethods { get; set; }
    public DbSet<User>          Users          { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Khai báo Primary Key rõ ràng (EF chỉ tự nhận Id hoặc {ClassName}Id)
        modelBuilder.Entity<Area>()         .HasKey(e => e.AreaID);
        modelBuilder.Entity<DiningTable>()  .HasKey(e => e.TableID);
        modelBuilder.Entity<Category>()     .HasKey(e => e.CategoryID);
        modelBuilder.Entity<Product>()      .HasKey(e => e.ProductID);
        modelBuilder.Entity<Order>()        .HasKey(e => e.OrderID);
        modelBuilder.Entity<OrderDetail>()  .HasKey(e => e.OrderDetailID);
        modelBuilder.Entity<PaymentMethod>().HasKey(e => e.PaymentMethodID);
        modelBuilder.Entity<User>()         .HasKey(e => e.UserID);

        // Tên bảng tường minh
        modelBuilder.Entity<DiningTable>() .ToTable("DiningTables");
        modelBuilder.Entity<Area>()        .ToTable("Areas");
        modelBuilder.Entity<Order>()       .ToTable("Orders");
        modelBuilder.Entity<OrderDetail>() .ToTable("OrderDetails");

        // Kiểu decimal cho VND

        // SubTotal tính trong C#, không lưu DB
        modelBuilder.Entity<OrderDetail>()
            .Ignore(od => od.SubTotal);

        // Quan hệ Area → DiningTable
        modelBuilder.Entity<DiningTable>()
            .HasOne(t => t.Area)
            .WithMany(a => a.DiningTables)
            .HasForeignKey(t => t.AreaID)
            .OnDelete(DeleteBehavior.Restrict);

        // Quan hệ DiningTable → Order
        modelBuilder.Entity<Order>()
            .HasOne(o => o.DiningTable)
            .WithMany(t => t.Orders)
            .HasForeignKey(o => o.TableID)
            .OnDelete(DeleteBehavior.Restrict);

        // Quan hệ Order → OrderDetail
        modelBuilder.Entity<OrderDetail>()
            .HasOne(od => od.Order)
            .WithMany(o => o.OrderDetails)
            .HasForeignKey(od => od.OrderID)
            .OnDelete(DeleteBehavior.Cascade);

        // Quan hệ Category → Product
        modelBuilder.Entity<Product>()
            .HasOne(p => p.Category)
            .WithMany(c => c.Products)
            .HasForeignKey(p => p.CategoryID)
            .OnDelete(DeleteBehavior.Restrict);

        // Quan hệ PaymentMethod → Order (optional)
        modelBuilder.Entity<Order>()
            .HasOne(o => o.PaymentMethod)
            .WithMany()
            .HasForeignKey(o => o.PaymentMethodID)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
