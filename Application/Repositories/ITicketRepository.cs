using Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application
{
    public interface ITicketRepository
    {
        public Task<Ticket> GetByIdAsync(int id);
        public Task<List<Ticket>> GetAllAsync();
        public Task<Ticket> AddAsync(Ticket ticket);
        public Task<Ticket> UpdateAsync(Ticket ticket);
        public Task<bool> DeleteAsync(int id);
    }
}
