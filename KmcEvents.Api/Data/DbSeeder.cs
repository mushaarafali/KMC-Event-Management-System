using KmcEvents.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
namespace KmcEvents.Api.Data;
public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope=services.CreateScope(); var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
        if (!await db.Users.AnyAsync(x=>x.Role==Roles.Admin)){
            var hasher=scope.ServiceProvider.GetRequiredService<IPasswordHasher<AppUser>>();
            var u=new AppUser{FullName="KMC System Administrator",Nic="KMCADMIN001",Email="admin@kmc.lk",Role=Roles.Admin,IsActive=true};u.PasswordHash=hasher.HashPassword(u,"Admin@123");db.Users.Add(u);await db.SaveChangesAsync();
        }
    }
}
