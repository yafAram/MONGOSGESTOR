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
            var databases = await _mongoService.ListDatabasesAsync();
            return View(databases);
        }



        [HttpPost]
        public async Task<IActionResult> ExportData(string database, string collection)
        {
            // Llama al método de exportación de una colección
            await _mongoService.ExportCollectionAsync(database, collection);
            // Aquí debería implementarse la lógica para descargar el archivo exportado,
            // o redirigir a una vista que permita visualizar el resultado.
            return RedirectToAction("ExportacionImportacion");
        }


        [HttpPost]
        public async Task<IActionResult> ImportData(string database, string collection, IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("No se ha seleccionado archivo");

            await _mongoService.ImportCollectionAsync(database, collection, file);
            return RedirectToAction("Index");
        }
    }
}
