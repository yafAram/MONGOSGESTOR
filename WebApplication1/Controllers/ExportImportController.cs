using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using System.IO;
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
        public async Task<IActionResult> Export(string database, string collection)
        {
            Directory.CreateDirectory($"/backup/{database}");

            var outputPath = $"/backup/{database}/{collection}.json";
            await _mongoService.ExportCollectionAsync(database, collection, outputPath);

            return PhysicalFile(outputPath, "application/json", $"{collection}.json");
        }

        [HttpPost]
        public async Task<IActionResult> Import(string database, string collection, IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("No se ha seleccionado archivo");

            Directory.CreateDirectory("/backup/imports");
            var filePath = Path.Combine("/backup/imports", file.FileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            await _mongoService.ImportCollectionAsync(database, collection, filePath);
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
