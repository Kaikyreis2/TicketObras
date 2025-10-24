namespace Domain;

public class Ticket
{

    public int Id { get; set; }
    public string CEP { get; set; }
        public string Cidade { get; set; }
        public string Bairro { get; set; }
        public string Rua { get; set; }


        public string Contribuinte { get; set; }
        public string Telefone { get; set; }
        public string DataDoPedido { get; set; }
        public string StatusDoPedido { get; set; }
        public string OS { get; set; }
    
}
