
using Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure;

public class Context(DbContextOptions<Context> options) : DbContext(options)
{
    public DbSet<Ticket> Tickets { get; set; }
    public DbSet<Role> Roles { get; set; }
    public DbSet<User> Users { get; set; }

    /*protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseNpgsql();
    }*/

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {

        modelBuilder.Entity<Ticket>(ticket =>
        {
            ticket.HasKey(t => t.Id)
             .HasName("PK_Ticket_Id");

            ticket
                .Property(t => t.Id).HasColumnName("Id")
                .HasColumnType("INT")
                .UseIdentityColumn()
                .ValueGeneratedOnAdd();

            ticket.Property(e => e.CEP).HasMaxLength(9);
            ticket.Property(e => e.Cidade).HasMaxLength(100);
            ticket.Property(e => e.Bairro).HasMaxLength(100);
            ticket.Property(e => e.Rua).HasMaxLength(200);
            ticket.Property(e => e.Contribuinte).HasMaxLength(150);
            ticket.Property(e => e.Telefone).HasMaxLength(20);
            ticket.Property(e => e.StatusDoPedido).HasMaxLength(50);
            ticket.Property(e => e.OS).HasMaxLength(50);
        });

        modelBuilder.Entity<User>(u =>
        {
            u.HasKey(p => p.Id)
                .HasName("PK_User_Id");

            u.Property(p => p.PasswordHash).HasMaxLength(100);
            u.Property(p => p.Email).HasMaxLength(50);
      
            u.HasMany(u => u.Roles)
            .WithMany(r => r.Users)
            .UsingEntity<Dictionary<string, object>>(
                    "UserRole",
                    j => j.HasOne<Role>().WithMany().HasForeignKey("RoleId").HasPrincipalKey(r => r.Id),
                    j => j.HasOne<User>().WithMany().HasForeignKey("UserId").HasPrincipalKey(u => u.Id),
                    j =>
                    {
                        j.HasKey("UserId", "RoleId");
                        j.ToTable("UserRole");
                    }
                );



            u.HasIndex(p => p.Email).IsUnique();

        });

    }
}
