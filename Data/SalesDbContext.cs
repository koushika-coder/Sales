using Microsoft.EntityFrameworkCore;
using Sales.Models;
using Sales.Models.Sales.Models;

namespace Sales.Data;

public class SalesDbContext : DbContext
{
    public SalesDbContext(DbContextOptions<SalesDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users { get; set; } = null!;
    public DbSet<Admin> Admins { get; set; } = null!;
    public DbSet<Order> Orders { get; set; } = null!;
    public DbSet<ShopSale> ShopSales { get; set; } = null!;
    public DbSet<CreditCardBanking> CreditCardBanking { get; set; } = null!;

    //public DbSet<Supplier> Suppliers { get; set; } 
    public DbSet<SafeDrop> SafeDrops { get; set; }

    public DbSet<Paypoint> Paypoints { get; set; }

    public DbSet<LotteryMaster> LotteryMaster { get; set; }

    public DbSet<LotteryInventory> LotteryInventory { get; set; }

    // AppDbContext.cs — add these DbSets
    public DbSet<Deduction> Deductions { get; set; }
    public DbSet<Supplier> Suppliers2 { get; set; }
    //public DbSet<Supplier> Suppliers { get; set; }
    public DbSet<SupplierInvoice> SupplierInvoices { get; set; }
    public DbSet<Lottery> Lotteries { get; set; } = null!;
    public DbSet<Reconciliation> Reconciliations { get; set; } = null!;
    public DbSet<TransactionAudit> TransactionAudits { get; set; } = null!;

    public DbSet<GmailConfiguration> GmailConfiguration { get; set; }
    public DbSet<SummaryCommit> SummaryCommits { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // User configuration
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Email).IsRequired().HasMaxLength(255);
            entity.Property(e => e.PasswordHash).IsRequired();
            entity.Property(e => e.Name).IsRequired().HasMaxLength(255);
            entity.Property(e => e.Role).IsRequired().HasMaxLength(50);
            //entity.Property(e => e.GoogleAccessToken).HasMaxLength(4000);
            //entity.Property(e => e.GoogleRefreshToken).HasMaxLength(4000);
            entity.Property(e =>e.PasswordHash).HasMaxLength(50);
            entity.HasIndex(e => e.Email).IsUnique();
            //entity.HasMany(e => e.Orders).WithOne(o => o.User).HasForeignKey(o => o.UserId);
        });

        // Admin configuration
        modelBuilder.Entity<Admin>(entity =>
        {
            entity.HasKey(e => e.AdminId);
            entity.Property(e => e.Email).IsRequired().HasMaxLength(255);
            entity.Property(e => e.PasswordHash).IsRequired();
            entity.Property(e => e.Name).IsRequired().HasMaxLength(255);
            entity.Property(e => e.Role).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Department).IsRequired().HasMaxLength(100);
            entity.HasIndex(e => e.Email).IsUnique();
        });

        // Order configuration
        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(e => e.OrderId);
            entity.Property(e => e.OrderNumber).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(50);
            entity.Property(e => e.TotalAmount).HasPrecision(18, 2);
            //entity.HasOne(e => e.User).WithMany(u => u.Orders).HasForeignKey(e => e.UserId);
        });

        // ShopSale configuration
        modelBuilder.Entity<ShopSale>(entity =>
        {
            entity.HasKey(e => e.ShopSaleId);
            entity.Property(e => e.TransactionNumber).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Amount).HasPrecision(18, 2);
            entity.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId);
            entity.HasIndex(e => e.TransactionNumber);
        });

        // CreditCardBanking configuration
        modelBuilder.Entity<CreditCardBanking>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.ManualCardAmount)
                  .IsRequired()
                  .HasPrecision(18, 2);

            entity.Property(e => e.CardAmount)
                  .IsRequired()
                  .HasPrecision(18, 2);

            entity.Property(e => e.CreatedDate)
                  .IsRequired();

            entity.HasOne(e => e.User)
                  .WithMany()
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // CashBanking configuration
        // CashBanking Configuration
        //modelBuilder.Entity<CashBanking>(entity =>
        //{
        //    entity.HasKey(e => e.Id);

        //    entity.Property(e => e.InvoiceNumber)
        //        .IsRequired()
        //        .HasMaxLength(100);

        //    entity.Property(e => e.InvoiceValue)
        //        .HasPrecision(18, 2);

        //    entity.Property(e => e.CreatedDate)
        //        .IsRequired();

        //    entity.Property(e => e.ModifiedDate)
        //        .IsRequired(false);

        //    entity.Property(e => e.IsDeleted)
        //        .HasDefaultValue(false);

        //    entity.HasOne(e => e.Supplier)
        //        .WithMany(s => s.CashBankings)
        //        .HasForeignKey(e => e.SupplierId)
        //        .OnDelete(DeleteBehavior.Restrict);

        //    entity.HasIndex(e => e.UserId);

        //    entity.HasIndex(e => e.SupplierId);

        //    entity.HasIndex(e => e.InvoiceNumber);
        //});

        // Deduction configuration
        modelBuilder.Entity<Deduction>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Cashback).HasPrecision(18, 2);
            entity.Property(e => e.PaypointPayout).HasPrecision(18, 2);
            entity.Property(e => e.InstantLotteryPayout).HasPrecision(18, 2);
            entity.Property(e => e.NewsVoucher).HasPrecision(18, 2);
            entity.Property(e => e.DDPoint).HasPrecision(18, 2);
            entity.HasOne<User>().WithMany().HasForeignKey(e => e.UserId);
        });
        modelBuilder.Entity<Supplier>(entity =>
        {
            entity.ToTable("Suppliers2");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.Name)
                  .HasMaxLength(100)
                  .IsRequired();

            entity.HasIndex(x => x.Name)
                  .IsUnique();
        });
        // AppDbContext.cs
        modelBuilder.Entity<SupplierInvoice>(entity =>
        {
            entity.ToTable("SupplierInvoices");  // ← exact name of your existing table
            entity.HasKey(e => e.Id);
            entity.Property(e => e.InvoiceNo).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Value).HasPrecision(18, 2);
            entity.HasOne<User>().WithMany().HasForeignKey(e => e.UserId);
            /*entity.HasOne(e => e.Supplier).WithMany(s => s.Invoices).HasForeignKey(e => e.SupplierId)*/;
        });

        // InstantLottery configuration
        //modelBuilder.Entity<InstantLottery>(entity =>
        //{
        //    entity.HasKey(e => e.InstantLotteryId);
        //    entity.Property(e => e.TicketNumber).IsRequired().HasMaxLength(100);
        //    entity.Property(e => e.Status).IsRequired().HasMaxLength(50);
        //    entity.Property(e => e.TicketPrice).HasPrecision(18, 2);
        //    entity.Property(e => e.PrizeAmount).HasPrecision(18, 2);
        //    entity.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId);
        //    entity.HasIndex(e => e.TicketNumber);
        //});

        // Lottery configuration
        modelBuilder.Entity<Lottery>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.LotteryValue)
                .HasPrecision(18, 2)
                .IsRequired();

            entity.Property(e => e.CreatedDate)
                .IsRequired();

            entity.Property(e => e.UpdatedDate)
                .IsRequired();

            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.UserId);
        });

        modelBuilder.Entity<LotteryMaster>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.ScratchCardNo)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(e => e.Price)
                .HasPrecision(18, 2);

            entity.Property(e => e.IsActive)
                .IsRequired();

            entity.Property(e => e.CreatedDate)
                .IsRequired();

            entity.HasIndex(e => e.ScratchCardNo)
                .IsUnique();
        });

        modelBuilder.Entity<LotteryInventory>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Sales)
                .HasPrecision(18, 2);

            entity.HasOne(e => e.LotteryMaster)
                .WithMany()
                .HasForeignKey(e => e.LotteryId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => new
            {
                e.UserId,
                e.LotteryId,
                e.InventoryDate
            }).IsUnique();
        });

        modelBuilder.Entity<SafeDrop>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.LastSafe)
                  .HasColumnType("decimal(18,2)")
                  .IsRequired();

            entity.Property(e => e.SafeDropAmount)
                  .HasColumnType("decimal(18,2)")
                  .IsRequired();

            // Cash is computed — do NOT map it to a column
            entity.Ignore(e => e.Cash);

            // One record per day
            entity.HasIndex(e => e.Date).IsUnique();

            entity.Property(e => e.Date).IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired();
        });
        // Reconciliation configuration
        modelBuilder.Entity<Reconciliation>(entity =>
        {
            entity.HasKey(e => e.ReconciliationId);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(50);
            entity.Property(e => e.TotalShopSales).HasPrecision(18, 2);
            entity.Property(e => e.TotalCreditCardTransactions).HasPrecision(18, 2);
            entity.Property(e => e.TotalCashTransactions).HasPrecision(18, 2);
            entity.Property(e => e.TotalDeductions).HasPrecision(18, 2);
            entity.Property(e => e.TotalLotteryAmount).HasPrecision(18, 2);
            entity.Property(e => e.NetAmount).HasPrecision(18, 2);
            entity.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId);
            entity.HasIndex(e => e.ReconciliationDate);
        });

        // TransactionAudit configuration
        modelBuilder.Entity<TransactionAudit>(entity =>
        {
            entity.HasKey(e => e.AuditId);
            entity.Property(e => e.TransactionType).IsRequired().HasMaxLength(100);
            entity.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => new { e.TransactionType, e.TransactionId });
        });

        // SummaryCommit configuration
        modelBuilder.Entity<SummaryCommit>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Date).IsRequired();
            entity.Property(e => e.SummaryTotal).HasPrecision(18, 2);
            entity.Property(e => e.ZReportTotal).HasPrecision(18, 2);
            entity.Property(e => e.Difference).HasPrecision(18, 2);
            entity.Property(e => e.CommittedAt).IsRequired();
            entity.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId);
            entity.HasIndex(e => new { e.UserId, e.Date }).IsUnique();
        });
    }
}
