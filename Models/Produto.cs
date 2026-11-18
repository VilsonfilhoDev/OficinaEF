using System.ComponentModel.DataAnnotations;

namespace OficinaEF.Models;

public class Produto
{
    public int Id { get; set; }
    
    [Required, StringLength(120)]
    public string Nome { get; set; } = string.Empty;

    [Range(typeof(decimal), "0.01", "99999999.99")]
    public decimal Preco { get; set; }

    [Range(0, int.MaxValue)]
    public int Estoque { get; set; }

    // Chave Estrangeira para Categoria
    public int CategoriaId { get; set; }
    public Categoria? Categoria { get; set; }

    public Produto()
    {
    }

    public Produto(int id, string nome, decimal preco, int estoque, int categoriaId)
    {
        Id = id;
        Nome = nome;
        Preco = preco;
        Estoque = estoque;
        CategoriaId = categoriaId;
    }
}