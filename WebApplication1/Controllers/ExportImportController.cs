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
            // Se crea la carpeta para el backup (en la ruta montada en el volumen /backup)
            var backupFolder = $"/backup/{database}";
            Directory.CreateDirectory(backupFolder);

            // Se exporta la base de datos completa
            await _mongoService.ExportDatabaseAsync(database, backupFolder);

            // Se crea un archivo ZIP a partir de la carpeta de backup
            var zipPath = $"/backup/{database}.zip";
            if (System.IO.File.Exists(zipPath))
                System.IO.File.Delete(zipPath);

            ZipFile.CreateFromDirectory(backupFolder, zipPath);

            return PhysicalFile(zipPath, "application/zip", $"{database}.zip");
        }



        [HttpPost]
        public async Task<IActionResult> Import(string database, IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("No se ha seleccionado archivo");

            // Define la carpeta base para la importación dentro de /backup (volumen compartido)
            var importFolder = "/backup/imports";
            Directory.CreateDirectory(importFolder);

            // Guarda el archivo ZIP en la carpeta de importación
            var filePath = Path.Combine(importFolder, file.FileName);
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            // Define la carpeta a la que se extraerá el contenido del ZIP
            var extractFolder = Path.Combine(importFolder, Path.GetFileNameWithoutExtension(file.FileName));
            if (Directory.Exists(extractFolder))
                Directory.Delete(extractFolder, true);
            Directory.CreateDirectory(extractFolder);

            // Extrae el ZIP a la carpeta (usando rutas de Linux)
            ZipFile.ExtractToDirectory(filePath, extractFolder);

            // Asegura que la ruta extraída use '/' (aunque en Linux ya lo hace, lo forzamos por seguridad)
            extractFolder = extractFolder.Replace("\\", "/");

            // Restaura el backup completo usando la carpeta extraída
            await _mongoService.RestoreDatabaseAsync(database, extractFolder);
            return RedirectToAction("Index");
        }



        public async Task<IActionResult> GetCollections(string database)
        {
            var db = _mongoClient.GetDatabase(database);
            var collections = await db.ListCollectionNamesAsync();
            return Json(collections.ToList());
        }
    }
}
