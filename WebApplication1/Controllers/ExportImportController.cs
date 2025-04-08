using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using WebApplication1.Services;

namespace WebApplication1.Controllers
{
    public class ExportImportController : Controller
    {
        private readonly MongoDBService _mongoService;

        public ExportImportController(MongoDBService mongoService)
        {
            _mongoService = mongoService;
        }

        [HttpPost]
        public async Task<IActionResult> Export(string database, string collection)
        {
            await _mongoService.ExportCollectionAsync(database, collection);
            return PhysicalFile($"./Exports/{collection}.json", "application/json", $"{collection}.json");
        }

        [HttpPost]
        public async Task<IActionResult> Import(string database, IFormFile file)
        {
            var filePath = Path.Combine("./Imports", file.FileName);
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "mongoimport",
                    Arguments = $"--db {database} --collection {Path.GetFileNameWithoutExtension(file.FileName)} --file {filePath}"
                }
            };
            process.Start();

            return RedirectToAction("Index", "Database");
        }
    }
}
