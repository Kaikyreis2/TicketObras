
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



    }
}
