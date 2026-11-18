// namespace OficinaEF.Models;

// public class Categoria
// {
//     public int Id { get; set; }
//     public string Nome { get; set; } = string.Empty;
//     public string Descricao { get; set; } = string.Empty;

//     // Relacionamento 1:N (Uma categoria tem vários produtos)
//     public ICollection<Produto> Produtos { get; set; } = new List<Produto>();

//     public Categoria()
//     {
//     }

//     public Categoria(int id, string nome, string descricao)
//     {
//         Id = id;
//         Nome = nome;
//         Descricao = descricao;
//     }
// }