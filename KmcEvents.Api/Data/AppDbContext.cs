using KmcEvents.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace KmcEvents.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<CityEvent> Events => Set<CityEvent>();
    public DbSet<TicketCategory> TicketCategories => Set<TicketCategory>();
    public DbSet<Reservation> Reservations => Set<Reservation>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<ContactMessage> ContactMessages => Set<ContactMessage>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<AppUser>().HasIndex(x => x.Email).IsUnique();
        b.Entity<CityEvent>().HasOne(x => x.Organizer).WithMany().HasForeignKey(x => x.OrganizerId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<TicketCategory>().HasOne(x => x.Event).WithMany(x => x.TicketCategories).HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<Reservation>().HasOne(x => x.Event).WithMany().HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Reservation>().HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Reservation>().HasOne(x => x.TicketCategory).WithMany().HasForeignKey(x => x.TicketCategoryId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Reservation>().HasOne(x => x.Payment).WithOne(x => x.Reservation).HasForeignKey<Payment>(x => x.ReservationId);
    }
}
