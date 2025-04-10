using Microsoft.AspNetCore.Mvc;
using WebApplication1.Services;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace WebApplication1.Controllers
{
    public class DatabaseController : Controller
    {
        private readonly MongoDBService _mongoService;

        public DatabaseController(MongoDBService mongoService)
        {
            _mongoService = mongoService;
        }

        // Ya está correcto. El método ListDatabasesAsync() devuelve List<string>
        public async Task<IActionResult> Index()
        {
            var databases = await _mongoService.ListDatabasesAsync();
            return View(databases); // List<string> -> Vista Index.cshtml (OK)
        }

        [HttpPost]
        public async Task<IActionResult> CreateDatabase(string databaseName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(databaseName))
                {
                    TempData["Error"] = "El nombre de la base de datos no puede estar vacío";
                    return RedirectToAction("Index");
                }

                await _mongoService.CreateDatabaseAsync(databaseName);
                TempData["Success"] = $"Base de datos '{databaseName}' creada exitosamente!";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error: {ex.Message}";
            }
            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> CreateBackup(string databaseName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(databaseName))
                {
                    TempData["Error"] = "Debe especificar una base de datos";
                    return RedirectToAction("Index");
                }

                await _mongoService.CreateBackupAsync(databaseName);
                TempData["Success"] = $"Backup de '{databaseName}' creado exitosamente!";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error en backup: {ex.Message}";
            }
            return RedirectToAction("Index");
        }
    }
}