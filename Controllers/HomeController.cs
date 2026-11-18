using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OficinaEF.Data;
using OficinaEF.Models;

namespace OficinaEF.Controllers;

public class HomeController : Controller
{
    private readonly ApplicationDbContext _context;

    public HomeController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        HomeViewModel modelo = new()
        {
            Categorias = await _context.Categorias
                .AsNoTracking()
                .OrderBy(c => c.Nome)
                .ToListAsync(),

            // Vitrine: só produtos com estoque, os mais recentes primeiro
            Produtos = await _context.Produtos
                .AsNoTracking()
                .Include(p => p.Categoria)
                .Where(p => p.Estoque > 0)
                .OrderByDescending(p => p.Id)
                .Take(12)
                .ToListAsync()
        };

        return View(modelo);
    }

    [Authorize(Roles = "Atendente")]
    public IActionResult Atendente()
    {
        return View();
    }

    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Admin()
    {
        var pedidos = await _context.Pedidos
            .AsNoTracking()
            .Include(p => p.Itens)
            .OrderByDescending(p => p.DataPedido)
            .ToListAsync();

        var produtos = await _context.Produtos
            .AsNoTracking()
            .Include(p => p.Categoria)
            .ToListAsync();

        ViewBag.FaturamentoTotal = pedidos
            .Where(p => p.Status != StatusPedido.Cancelado)
            .Sum(p => p.ValorTotal);

        ViewBag.PedidosRecebidos = pedidos.Count(p => p.Status == StatusPedido.Recebido);
        ViewBag.PedidosConcluidos = pedidos.Count(p => p.Status == StatusPedido.Concluido);
        ViewBag.PedidosCancelados = pedidos.Count(p => p.Status == StatusPedido.Cancelado);
        ViewBag.TotalPedidos = pedidos.Count;

        ViewBag.TotalProdutos = produtos.Count;
        ViewBag.EstoqueBaixo = produtos.Where(p => p.Estoque <= 3).OrderBy(p => p.Estoque).ToList();
        ViewBag.TotalClientes = await _context.Users.CountAsync();
        ViewBag.UltimosPedidos = pedidos.Take(6).ToList();

        // Dados para gráfico de faturamento por categoria
        var categoriasDados = await _context.Categorias
            .AsNoTracking()
            .Select(c => new
            {
                Nome = c.Nome,
                QtdProdutos = c.Produtos.Count
            })
            .ToListAsync();

        ViewBag.CategoriasNomes = categoriasDados.Select(c => c.Nome).ToList();
        ViewBag.CategoriasQtd = categoriasDados.Select(c => c.QtdProdutos).ToList();

        return View();
    }

    [Authorize(Roles = "Cliente")]
    public IActionResult Cliente()
    {
        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}