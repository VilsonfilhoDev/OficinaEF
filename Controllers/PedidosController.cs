using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using OficinaEF.Data;
using OficinaEF.Models;

namespace OficinaEF.Controllers;

[Authorize]
public class PedidosController : Controller
{
    private readonly ApplicationDbContext _context;

    public PedidosController(ApplicationDbContext context)
    {
        _context = context;
    }

    public class ItemCarrinhoViewModel
    {
        public string ProdutoId { get; set; } = string.Empty;
        public string Nome { get; set; } = string.Empty;
        public decimal Preco { get; set; }
        public int Quantidade { get; set; }
    }

    public class CheckoutViewModel
    {
        public string? ItensJson { get; set; }
        public List<ItemCarrinhoViewModel> Itens { get; set; } = new();
        public string EnderecoEntrega { get; set; } = string.Empty;
        public string FormaPagamento { get; set; } = string.Empty;
        public Guid CodigoSolicitacao { get; set; } = Guid.NewGuid();
        public decimal ValorTotal => Itens.Sum(i => i.Preco * i.Quantidade);
    }

    private static string? UsuarioId(System.Security.Claims.ClaimsPrincipal user) =>
        user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
        ?? user.FindFirst("sub")?.Value
        ?? string.Empty;

    [HttpGet]
    [Authorize(Roles = "Cliente")]
    public async Task<IActionResult> Checkout(string? itens, Guid? codigoSolicitacao)
    {
        var itensLista = new List<ItemCarrinhoViewModel>();

        if (!string.IsNullOrWhiteSpace(itens))
        {
            var pares = itens.Split(',', StringSplitOptions.RemoveEmptyEntries);
            foreach (var par in pares)
            {
                var partes = par.Split(':');
                if (partes.Length == 2 &&
                    int.TryParse(partes[0], out int produtoId) &&
                    int.TryParse(partes[1], out int quantidade) &&
                    quantidade > 0)
                {
                    var produto = await _context.Produtos
                        .AsNoTracking()
                        .FirstOrDefaultAsync(p => p.Id == produtoId);

                    if (produto is not null && produto.Estoque >= quantidade)
                    {
                        itensLista.Add(new ItemCarrinhoViewModel
                        {
                            ProdutoId = produto.Id.ToString(),
                            Nome = produto.Nome,
                            Preco = produto.Preco,
                            Quantidade = quantidade
                        });
                    }
                }
            }
        }

        var model = new CheckoutViewModel
        {
            Itens = itensLista,
            CodigoSolicitacao = codigoSolicitacao ?? Guid.NewGuid(),
            ItensJson = null
        };

        return View(model);
    }

    [HttpPost]
    [Authorize(Roles = "Cliente")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Checkout(CheckoutViewModel model)
    {
        if (model.Itens == null || model.Itens.Count == 0)
        {
            ModelState.AddModelError(string.Empty, "O carrinho está vazio.");
            return View(model);
        }

        if (!ModelState.IsValid)
            return View(model);

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            bool jaExiste = await _context.Pedidos
                .AsNoTracking()
                .AnyAsync(p => p.CodigoSolicitacao == model.CodigoSolicitacao);
            if (jaExiste)
            {
                ModelState.AddModelError(string.Empty, "Este pedido já foi processado (código de solicitação duplicado).");
                await transaction.RollbackAsync();
                return View(model);
            }

            var produtosParaBaixar = new List<(int ProdutoId, int Quantidade)>();

            foreach (var item in model.Itens)
            {
                if (!int.TryParse(item.ProdutoId, out int produtoId))
                {
                    ModelState.AddModelError(string.Empty, $"Produto inválido: {item.Nome}");
                    await transaction.RollbackAsync();
                    return View(model);
                }

                var produto = await _context.Produtos
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.Id == produtoId);

                if (produto is null)
                {
                    ModelState.AddModelError(string.Empty, $"Produto não encontrado: {item.Nome}");
                    await transaction.RollbackAsync();
                    return View(model);
                }

                if (produto.Estoque < item.Quantidade)
                {
                    ModelState.AddModelError(string.Empty,
                        $"Estoque insuficiente para {produto.Nome}. Disponível: {produto.Estoque}.");
                    await transaction.RollbackAsync();
                    return View(model);
                }

                produtosParaBaixar.Add((produtoId, item.Quantidade));
            }

            var pedido = new Pedido
            {
                UsuarioId = UsuarioId(User),
                CodigoSolicitacao = model.CodigoSolicitacao,
                EnderecoEntrega = model.EnderecoEntrega,
                FormaPagamento = model.FormaPagamento,
                ValorTotal = model.ValorTotal,
                DataPedido = DateTime.Now,
                Status = StatusPedido.Recebido,
                Itens = model.Itens.Select(i => new ItemPedido
                {
                    ProdutoId = int.Parse(i.ProdutoId),
                    NomeProduto = i.Nome,
                    PrecoUnitario = i.Preco,
                    Quantidade = i.Quantidade
                }).ToList()
            };

            _context.Pedidos.Add(pedido);

            foreach (var (produtoId, quantidade) in produtosParaBaixar)
            {
                await _context.Produtos
                    .Where(p => p.Id == produtoId)
                    .ExecuteUpdateAsync(p => p.SetProperty(pr => pr.Estoque, pr => pr.Estoque - quantidade));
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            TempData["Sucesso"] = "Pedido finalizado com sucesso!";
            // Limpa o carrinho no cliente e redireciona para os detalhes do pedido
            var urlDetalhes = Url.Action(nameof(Details), new { id = pedido.Id })!;
            return Content(
                $"<script>localStorage.removeItem('void.bag.v1'); window.location.href = '{urlDetalhes}';</script>",
                "text/html");
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException sqlEx &&
            (sqlEx.Number == 1205 || sqlEx.Number == 1222))
        {
            await transaction.RollbackAsync();
            ModelState.AddModelError(string.Empty, "O sistema está momentaneamente ocupado. Tente novamente em instantes.");
            return View(model);
        }
        catch (DbUpdateException ex)
        {
            await transaction.RollbackAsync();
            ModelState.AddModelError(string.Empty, "Não foi possível concluir o pedido. Tente novamente.");
            return View(model);
        }
        catch (SqlException ex) when (ex.Number == 1205 || ex.Number == 1222)
        {
            await transaction.RollbackAsync();
            ModelState.AddModelError(string.Empty, "O sistema está momentaneamente ocupado. Tente novamente em instantes.");
            return View(model);
        }
        catch (Exception)
        {
            await transaction.RollbackAsync();
            ModelState.AddModelError(string.Empty, "Erro inesperado ao finalizar a compra.");
            return View(model);
        }
    }

    [HttpGet]
    [Authorize(Roles = "Cliente")]
    public async Task<IActionResult> Index()
    {
        var usuarioId = UsuarioId(User);

        var pedidos = await _context.Pedidos
            .AsNoTracking()
            .Include(p => p.Itens)
            .Where(p => p.UsuarioId == usuarioId)
            .OrderByDescending(p => p.DataPedido)
            .ToListAsync();

        return View(pedidos);
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> Details(int id)
    {
        var pedido = await _context.Pedidos
            .AsNoTracking()
            .Include(p => p.Itens)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (pedido is null)
            return NotFound();

        var usuarioId = UsuarioId(User);
        bool isDono = pedido.UsuarioId == usuarioId;
        bool isAdmin = User.IsInRole("Admin");
        bool isAtendente = User.IsInRole("Atendente");

        if (!isDono && !isAdmin && !isAtendente)
            return Forbid();

        return View(pedido);
    }

    [HttpGet]
    [Authorize(Roles = "Admin,Atendente")]
    public async Task<IActionResult> Gestao()
    {
        var pedidos = await _context.Pedidos
            .AsNoTracking()
            .Include(p => p.Itens)
            .OrderByDescending(p => p.DataPedido)
            .ToListAsync();

        return View(pedidos);
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Atendente")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Concluir(int id)
    {
        var linhas = await _context.Pedidos
            .Where(p => p.Id == id && p.Status == StatusPedido.Recebido)
            .ExecuteUpdateAsync(p => p.SetProperty(p => p.Status, StatusPedido.Concluido));

        if (linhas == 0)
            TempData["Erro"] = "Não foi possível concluir o pedido (status inválido ou pedido não encontrado).";
        else
            TempData["Sucesso"] = "Pedido concluído com sucesso!";

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancelar(int id)
    {
        var pedido = await _context.Pedidos
            .Include(p => p.Itens)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (pedido is null)
            return NotFound();

        var usuarioId = UsuarioId(User);
        bool isDono = pedido.UsuarioId == usuarioId;
        bool isAdmin = User.IsInRole("Admin");
        bool isAtendente = User.IsInRole("Atendente");

        if (!isDono && !isAdmin && !isAtendente)
            return Forbid();

        if (pedido.Status != StatusPedido.Recebido)
        {
            TempData["Erro"] = "Somente pedidos com status 'Recebido' podem ser cancelados.";
            return RedirectToAction(nameof(Details), new { id });
        }

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            foreach (var item in pedido.Itens)
            {
                await _context.Produtos
                    .Where(p => p.Id == item.ProdutoId)
                    .ExecuteUpdateAsync(p => p.SetProperty(pr => pr.Estoque, pr => pr.Estoque + item.Quantidade));
            }

            pedido.Status = StatusPedido.Cancelado;
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            TempData["Sucesso"] = "Pedido cancelado e estoque devolvido.";
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException sqlEx &&
            (sqlEx.Number == 1205 || sqlEx.Number == 1222))
        {
            await transaction.RollbackAsync();
            TempData["Erro"] = "O sistema está momentaneamente ocupado. Tente novamente em instantes.";
        }
        catch (SqlException ex) when (ex.Number == 1205 || ex.Number == 1222)
        {
            await transaction.RollbackAsync();
            TempData["Erro"] = "O sistema está momentaneamente ocupado. Tente novamente em instantes.";
        }
        catch (Exception)
        {
            await transaction.RollbackAsync();
            TempData["Erro"] = "Erro ao cancelar o pedido.";
        }

        return RedirectToAction(nameof(Details), new { id });
    }
}
