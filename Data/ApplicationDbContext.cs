using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using OficinaEF.Models;

namespace OficinaEF.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext(options)
{
    public DbSet<Categoria> Categorias => Set<Categoria>();
    public DbSet<Produto> Produtos => Set<Produto>();
    public DbSet<Pedido> Pedidos => Set<Pedido>();
    public DbSet<ItemPedido> ItensPedido => Set<ItemPedido>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Define a precisão do Preço para o SQL Server
        builder.Entity<Produto>().Property(p => p.Preco).HasPrecision(10, 2);
        builder.Entity<Produto>().Property(p => p.PrecoAntigo).HasPrecision(10, 2);

        // Relacionamento 1 para N
        builder.Entity<Produto>()
            .HasOne(p => p.Categoria)
            .WithMany(c => c.Produtos)
            .HasForeignKey(p => p.CategoriaId)
            .OnDelete(DeleteBehavior.Restrict);

        // Pedido
        builder.Entity<Pedido>()
            .Property(p => p.ValorTotal)
            .HasPrecision(10, 2);

        builder.Entity<Pedido>()
            .HasIndex(p => p.CodigoSolicitacao)
            .IsUnique();

        builder.Entity<Pedido>()
            .HasOne(p => p.Usuario)
            .WithMany()
            .HasForeignKey(p => p.UsuarioId)
            .OnDelete(DeleteBehavior.Restrict);

        // ItemPedido
        builder.Entity<ItemPedido>()
            .Property(i => i.PrecoUnitario)
            .HasPrecision(10, 2);

        builder.Entity<ItemPedido>()
            .HasOne(i => i.Pedido)
            .WithMany(p => p.Itens)
            .HasForeignKey(i => i.PedidoId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
