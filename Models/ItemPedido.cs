using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OficinaEF.Models;

public class ItemPedido
{
    public int Id { get; set; }

    public int PedidoId { get; set; }

    [ForeignKey(nameof(PedidoId))]
    public Pedido Pedido { get; set; } = null!;

    public int ProdutoId { get; set; }

    [Required]
    [StringLength(120)]
    public string NomeProduto { get; set; } = string.Empty;

    [Range(0.01, double.MaxValue, ErrorMessage = "O preço unitário deve ser maior que zero.")]
    public decimal PrecoUnitario { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "A quantidade deve ser maior que zero.")]
    public int Quantidade { get; set; }
}
