using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using WebApplication1.Services;

namespace WebApplication1.Controllers
{
    public class ExportImportController : Controller
    {
        private readonly MongoDBService _mongoService;
        private readonly IMongoClient _mongoClient;

        public ExportImportController(MongoDBService mongoService, IMongoClient mongoClient)
        {
            _mongoService = mongoService;
            _mongoClient = mongoClient;
        }

        public async Task<IActionResult> Index()
        {
            var databaseNames = (await _mongoClient.ListDatabaseNamesAsync()).ToList();
            return View(databaseNames);
        }


        [HttpPost]
        public async Task<IActionResult> Export(string database)
        {
            try
            {
                // Ruta CORREGIDA: usar el volumen compartido /backup
                var backupFolder = Path.Combine("/backup", database, DateTime.Now.ToString("yyyy-MM-dd"));
                Directory.CreateDirectory(backupFolder);

                await _mongoService.ExportDatabaseAsync(database, backupFolder);

                // Crear ZIP en el mismo volumen
                var zipPath = Path.Combine("/backup", $"{database}-backup-{DateTime.Now:yyyyMMddHHmmss}.zip");
                if (System.IO.File.Exists(zipPath))
                    System.IO.File.Delete(zipPath);

                ZipFile.CreateFromDirectory(backupFolder, zipPath);

                // Descargar desde /backup
                return PhysicalFile(zipPath, "application/zip", $"{database}.zip");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error: {ex.Message}");
            }
        }



        [HttpPost]
        public async Task<IActionResult> Import(string database, IFormFile file)
        {
            try
            {
                if (file == null || file.Length == 0)
                    return BadRequest("No se ha seleccionado archivo");

                // Usar la misma ruta que en Export (/app/backups)
                var importBaseFolder = "/app/backups/imports";
                Directory.CreateDirectory(importBaseFolder);

                // Guardar el archivo ZIP en /app/backups/imports
                var filePath = Path.Combine(importBaseFolder, file.FileName);
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                // Carpeta para extraer el ZIP (ej: /app/backups/imports/midb-2023)
                var extractFolder = Path.Combine(importBaseFolder, Path.GetFileNameWithoutExtension(file.FileName));

                // Limpiar carpeta si existe
                if (Directory.Exists(extractFolder))
                    Directory.Delete(extractFolder, recursive: true);
                Directory.CreateDirectory(extractFolder);

                // Extraer el ZIP
                ZipFile.ExtractToDirectory(filePath, extractFolder);

                // Restaurar usando mongorestore
                await _mongoService.RestoreDatabaseAsync(database, extractFolder);

                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                // Loggear el error (ex.Message)
                return StatusCode(500, $"Error al importar: {ex.Message}");
            }
        }



        public async Task<IActionResult> GetCollections(string database)
        {
            var db = _mongoClient.GetDatabase(database);
            var collections = await db.ListCollectionNamesAsync();
            return Json(collections.ToList());
        }
    }
}
