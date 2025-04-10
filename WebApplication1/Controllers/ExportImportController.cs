﻿using Microsoft.AspNetCore.Http;
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
                // Se define la carpeta de backup en /backup/<database>/<fecha>
                var backupFolder = Path.Combine("/backup", database, DateTime.Now.ToString("yyyy-MM-dd"));
                Directory.CreateDirectory(backupFolder);

                await _mongoService.ExportDatabaseAsync(database, backupFolder);

                // Crear archivo ZIP a partir del backup (en /backup)
                var zipPath = Path.Combine("/backup", $"{database}-backup-{DateTime.Now:yyyyMMddHHmmss}.zip");
                if (System.IO.File.Exists(zipPath))
                    System.IO.File.Delete(zipPath);

                ZipFile.CreateFromDirectory(backupFolder, zipPath);

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

                // Definir carpeta base para la importación en /backup/imports (usando el mismo volumen)
                var importBaseFolder = "/backup/imports";
                Directory.CreateDirectory(importBaseFolder);

                var filePath = Path.Combine(importBaseFolder, file.FileName);
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                // Carpeta de extracción (por ejemplo, /backup/imports/{nombreDelZipSinExt})
                var extractFolder = Path.Combine(importBaseFolder, Path.GetFileNameWithoutExtension(file.FileName));
                if (Directory.Exists(extractFolder))
                    Directory.Delete(extractFolder, recursive: true);
                Directory.CreateDirectory(extractFolder);

                ZipFile.ExtractToDirectory(filePath, extractFolder);

                extractFolder = extractFolder.Replace("\\", "/");

                await _mongoService.RestoreDatabaseAsync(database, extractFolder);
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
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
