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

        public IActionResult Index()
        {
            return View("~/Views/Database/Index.cshtml");
        }




        [HttpPost]
        public async Task<IActionResult> Export(string database)
        {
            try
            {
                // Definir carpeta de backup en /backup/<database>/<fecha>
                var backupFolder = Path.Combine("/backup", database, DateTime.Now.ToString("yyyy-MM-dd"));
                Directory.CreateDirectory(backupFolder);

                await _mongoService.CreateBackupAsync(database, backupFolder);

                // Comprimir la carpeta de backup
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

                // Definir carpeta base para la importación en /backup/imports
                var importBaseFolder = "/backup/imports";
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
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error al importar backup: {ex.Message}");
            }
        }




    }
}
