namespace Fuse.ComputeSystem
{
    public interface ITicketHolder
    {
        public int Ticket { get; }
    }
    
    public static class Tickets
    {
        private static int _currentTicket = 0;

        public static int Next => _currentTicket++;
    }
}