
using Domain;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure;

public class TicketRepository(Context context) : ITicketRepository
{
    private readonly Context _context = context;

    public async Task<Ticket> AddAsync(Ticket ticket)
    {
        try
        {
            _context.Tickets.Add(ticket);
            await _context.SaveChangesAsync();
            return ticket;
        }
        catch (Exception ex)
        {
            
            Console.WriteLine($"Error creating ticket: {ex.Message}");
            return new Ticket();
        }
    }

    public async Task<Ticket> UpdateAsync(Ticket ticket)
    {
        try
        {
            _context.Tickets.Update(ticket);
            await _context.SaveChangesAsync();
            return ticket;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error updating ticket: {ex.Message}");
            return new Ticket();
        }
    }

    public async Task<Ticket> GetByIdAsync(int id)
    {
        try
        {
            return await _context.Tickets.FindAsync(id);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error retrieving ticket by ID {id}: {ex.Message}");
            return new Ticket();
        }
    }

    public async Task<List<Ticket>> GetAllAsync()
    {
        try
        {
            return await _context.Tickets.ToListAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error retrieving all tickets: {ex.Message}");
            throw;
        }
    }

    public async Task<bool> DeleteAsync(int id)
    {
        try
        {
            var ticket = await _context.Tickets.FindAsync(id);
            if (ticket == null) return false;

            _context.Tickets.Remove(ticket);
            await _context.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error deleting ticket with ID {id}: {ex.Message}");
            throw;
        }
    }
}
