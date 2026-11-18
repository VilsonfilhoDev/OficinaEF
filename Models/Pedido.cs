using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;

namespace OficinaEF.Models;

public enum StatusPedido
{
    Recebido,
    Concluido,
    Cancelado
}

public class Pedido
{
    public int Id { get; set; }

    [Required]
    public string UsuarioId { get; set; } = string.Empty;

    [ForeignKey(nameof(UsuarioId))]
    public IdentityUser Usuario { get; set; } = null!;

    [Required]
    public Guid CodigoSolicitacao { get; set; } = Guid.NewGuid();

    [Required]
    [StringLength(200)]
    public string EnderecoEntrega { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string FormaPagamento { get; set; } = string.Empty;

    [Range(0.01, double.MaxValue, ErrorMessage = "O valor total deve ser maior que zero.")]
    public decimal ValorTotal { get; set; }

    public DateTime DataPedido { get; set; } = DateTime.Now;

    public StatusPedido Status { get; set; } = StatusPedido.Recebido;

    public List<ItemPedido> Itens { get; set; } = new();
}
