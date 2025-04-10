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

        // Vista para gestionar exportación e importación de datos
        public IActionResult Index() => View();

        [HttpPost]
        public async Task<IActionResult> ExportData(string database, string collection)
        {
            // Llama al método de exportación de colección (ya existente en el servicio)
            await _mongoService.ExportCollectionAsync(database, collection);
            // Nota: Aquí se debería retornar o descargar el archivo exportado según esté implementado.
            return RedirectToAction("Index");
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
