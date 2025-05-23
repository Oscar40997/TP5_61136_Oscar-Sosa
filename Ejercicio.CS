Cliente.

using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

var baseUrl = "http://localhost:5000";
var http = new HttpClient();
var jsonOpt = new JsonSerializerOptions {
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true
};

async Task<List<Producto>> TraerProductosAsync()
{
    var json = await http.GetStringAsync($"{baseUrl}/productos");
    return JsonSerializer.Deserialize<List<Producto>>(json, jsonOpt)!;
}

async Task<List<Producto>> TraerProductosReponerAsync()
{
    var json = await http.GetStringAsync($"{baseUrl}/productos/reponer");
    return JsonSerializer.Deserialize<List<Producto>>(json, jsonOpt)!;
}

async Task AgregarStockAsync()
{
    Console.Write("ID del producto: ");
    var id = int.Parse(Console.ReadLine()!);
    Console.Write("Cantidad a agregar: ");
    var cantidad = int.Parse(Console.ReadLine()!);

    var res = await http.PostAsync($"{baseUrl}/productos/{id}/agregar?cantidad={cantidad}", null);
    var msg = await res.Content.ReadAsStringAsync();

    Console.WriteLine(res.IsSuccessStatusCode ? "✅ Stock agregado correctamente." : $"❌ Error: {msg}");
}

async Task QuitarStockAsync()
{
    Console.Write("ID del producto: ");
    var id = int.Parse(Console.ReadLine()!);
    Console.Write("Cantidad a quitar: ");
    var cantidad = int.Parse(Console.ReadLine()!);

    var res = await http.PostAsync($"{baseUrl}/productos/{id}/quitar?cantidad={cantidad}", null);
    var msg = await res.Content.ReadAsStringAsync();

    Console.WriteLine(res.IsSuccessStatusCode ? "✅ Stock quitado correctamente." : $"❌ Error: {msg}");
}

void MostrarProductos(List<Producto> productos)
{
    Console.WriteLine("\nID | Nombre                | Precio     | Stock");
    Console.WriteLine("-----------------------------------------------");
    foreach (var p in productos)
    {
        Console.WriteLine($"{p.Id,2} | {p.Nombre,-20} | {p.Precio,8:c} | {p.Stock,5}");
    }
}


while (true)
{
    Console.WriteLine("\n--- Menú de opciones ---");
    Console.WriteLine("1. Listar todos los productos");
    Console.WriteLine("2. Listar productos a reponer");
    Console.WriteLine("3. Agregar stock");
    Console.WriteLine("4. Quitar stock");
    Console.WriteLine("0. Salir");
    Console.Write("Seleccione una opción: ");
    var opcion = Console.ReadLine();

    try
    {
        switch (opcion)
        {
            case "1":
                MostrarProductos(await TraerProductosAsync());
                break;

            case "2":
                MostrarProductos(await TraerProductosReponerAsync());
                break;

            case "3":
                await AgregarStockAsync();
                break;

            case "4":
                await QuitarStockAsync();
                break;

            case "0":
                return;

            default:
                Console.WriteLine("Opción inválida.");
                break;
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ Error: {ex.Message}");
    }
}

class Producto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = null!;
    public decimal Precio { get; set; }
    public int Stock { get; set; }
}


Servidor.

#r "sdk:Microsoft.NET.Sdk.Web"
#r "nuget: Microsoft.EntityFrameworkCore, 9.0.4"
#r "nuget: Microsoft.EntityFrameworkCore.Sqlite, 9.0.4"

using System.Text.Json;                     
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder();
builder.Services.AddDbContext<AppDb>(opt => opt.UseSqlite("Data Source=./tienda.db")); 
builder.Services.Configure<JsonOptions>(opt => 
    opt.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase);

var app = builder.Build();


app.MapGet("/productos", async (AppDb db) =>
    await db.Productos.ToListAsync());

app.MapGet("/productos/reponer", async (AppDb db) =>
    await db.Productos.Where(p => p.Stock < 3).ToListAsync());

app.MapPost("/productos/{id}/agregar", async (int id, int cantidad, AppDb db) =>
{
    var producto = await db.Productos.FindAsync(id);
    if (producto is null) return Results.NotFound();

    producto.Stock += cantidad;
    await db.SaveChangesAsync();
    return Results.Ok(producto);
});

app.MapPost("/productos/{id}/quitar", async (int id, int cantidad, AppDb db) =>
{
    var producto = await db.Productos.FindAsync(id);
    if (producto is null) return Results.NotFound();

    if (producto.Stock - cantidad < 0)
        return Results.BadRequest("No se puede quitar más stock del disponible.");

    producto.Stock -= cantidad;
    await db.SaveChangesAsync();
    return Results.Ok(producto);
});

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDb>();
    db.Database.EnsureCreated();

    if (!db.Productos.Any())
    {
        for (int i = 1; i <= 10; i++)
        {
            db.Productos.Add(new Producto
            {
                Nombre = $"Producto {i}",
                Precio = 10 + i,
                Stock = 10
            });
        }
        db.SaveChanges();
    }
}

app.Run("http://localhost:5000");


class AppDb : DbContext
{
    public AppDb(DbContextOptions<AppDb> options) : base(options) { }

    public DbSet<Producto> Productos => Set<Producto>();
}

class Producto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = null!;
    public decimal Precio { get; set; }
    public int Stock { get; set; }
}
