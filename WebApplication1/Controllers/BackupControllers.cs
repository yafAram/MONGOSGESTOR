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

        public async Task<IActionResult> Backups()
        {
            var databases = await _mongoService.ListDatabasesAsync(); // Devuelve List<string>
            return View(databases);
        }




        [HttpPost]
        public async Task<IActionResult> Export(string database)
        {
            try
            {
                // Definir carpeta de backup en /backup/<database>/<fecha>
                // En BackupController.Export
                var backupFolder = Path.Combine("/app/backups", database, DateTime.Now.ToString("yyyy-MM-dd"));

                await _mongoService.CreateBackupAsync(database, backupFolder);

                // Comprimir la carpeta de backup
                var zipPath = Path.Combine("/app/backups", $"{database}-full-backup-{DateTime.Now:yyyyMMddHHmmss}.zip"); Directory.CreateDirectory(backupFolder);
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

                // Definir carpeta base para la importación en /backup/imports
                var importBaseFolder = "/app/backups/imports";
                Directory.CreateDirectory(importBaseFolder);

                // Guardar el archivo ZIP subido
                var filePath = Path.Combine(importBaseFolder, file.FileName);
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                // Extraer el ZIP en una carpeta temporal
                var extractFolder = Path.Combine(importBaseFolder, Path.GetFileNameWithoutExtension(file.FileName));
                if (Directory.Exists(extractFolder))
                    Directory.Delete(extractFolder, recursive: true);
                Directory.CreateDirectory(extractFolder);

                ZipFile.ExtractToDirectory(filePath, extractFolder);
                extractFolder = extractFolder.Replace("\\", "/");

                // Si la estructura extraída contiene una carpeta con el nombre de la BD, se ajusta la ruta
                var subFolder = Path.Combine(extractFolder, database);
                if (Directory.Exists(subFolder))
                {
                    extractFolder = subFolder.Replace("\\", "/");
                }

                await _mongoService.RestoreDatabaseAsync(database, extractFolder);
                return RedirectToAction("Backups");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error al importar backup: {ex.Message}");
            }
        }




    }
}
