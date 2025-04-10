using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using WebApplication1.Services;

namespace WebApplication1.Controllers
{
    public class BackupController : Controller
    {
        private readonly MongoDBService _mongoService;

        public BackupController(MongoDBService mongoService)
        {
            _mongoService = mongoService;
        }

        // Vista de respaldo (puedes personalizarla o redireccionar a Index de gestión de DB)
        public IActionResult Index() => View();

        [HttpPost]
        public async Task<IActionResult> Export(string database)
        {
            try
            {
                // Se define la carpeta de backup en /backup/<database>/<fecha>
                var backupFolder = Path.Combine("/backup", database, DateTime.Now.ToString("yyyy-MM-dd"));
                Directory.CreateDirectory(backupFolder);

                // Se ejecuta el backup completo de la base de datos
                await _mongoService.ExportDatabaseAsync(database, backupFolder);

                // Se crea un archivo ZIP con el contenido del backup
                var zipPath = Path.Combine("/backup", $"{database}-full-backup-{DateTime.Now:yyyyMMddHHmmss}.zip");
                if (System.IO.File.Exists(zipPath))
                    System.IO.File.Delete(zipPath);

                ZipFile.CreateFromDirectory(backupFolder, zipPath);

                return PhysicalFile(zipPath, "application/zip", $"{database}-full-backup.zip");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error en generación de backup: {ex.Message}");
            }
        }

        [HttpPost]
        public async Task<IActionResult> Import(string database, IFormFile file)
        {
            try
            {
                if (file == null || file.Length == 0)
                    return BadRequest("No se ha seleccionado archivo");

                // Definir carpeta base para la importación en /backup/imports (que debe estar en el volumen compartido)
                var importBaseFolder = "/backup/imports";
                Directory.CreateDirectory(importBaseFolder);

                var filePath = Path.Combine(importBaseFolder, file.FileName);
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                // Extraer el ZIP a una carpeta temporal
                var extractFolder = Path.Combine(importBaseFolder, Path.GetFileNameWithoutExtension(file.FileName));
                if (Directory.Exists(extractFolder))
                    Directory.Delete(extractFolder, recursive: true);
                Directory.CreateDirectory(extractFolder);

                ZipFile.ExtractToDirectory(filePath, extractFolder);
                extractFolder = extractFolder.Replace("\\", "/");

                // Restaurar el backup completo
                await _mongoService.RestoreDatabaseAsync(database, extractFolder);
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error al importar backup: {ex.Message}");
            }
        }
    }
}
