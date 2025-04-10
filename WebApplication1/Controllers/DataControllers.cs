using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using WebApplication1.Services;

namespace WebApplication1.Controllers
{
    public class DataController : Controller
    {
        private readonly MongoDBService _mongoService;

        public DataController(MongoDBService mongoService)
        {
            _mongoService = mongoService;
        }

        public async Task<IActionResult> ExportacionImportacion()
        {
            var databases = await _mongoService.ListDatabasesAsync(); // List<string>
            return View(databases);
        }



        [HttpPost]
        public async Task<IActionResult> ExportData(string database, string collection)
        {
            try
            {
                var exportPath = await _mongoService.ExportCollectionAsync(database, collection);
                var zipBytes = System.IO.File.ReadAllBytes(exportPath);
                return File(zipBytes, "application/zip", $"{database}-{collection}-export.zip");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error al exportar: {ex.Message}";
                return RedirectToAction("ExportacionImportacion");
            }
        }

        [HttpPost]
        public async Task<IActionResult> ImportData(string database, string collection, IFormFile file)
        {
            try
            {
                if (file == null || file.Length == 0)
                    return BadRequest("Archivo no válido");

                await _mongoService.ImportCollectionAsync(database, collection, file);
                TempData["Success"] = "Importación exitosa!";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error al importar: {ex.Message}";
            }
            return RedirectToAction("Index");
        }
    }
}
