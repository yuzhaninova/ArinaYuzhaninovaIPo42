using ECommerceCsvImport.Models;
using ECommerceCsvImport.Services;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceCsvImport.Controllers
{
    public class ProductsController : Controller
    {
        private readonly IProductRepository _repository;
        private readonly IProductImportService _importService;

        public ProductsController(IProductRepository repository, IProductImportService importService)
        {
            _repository = repository;
            _importService = importService;
        }

        public IActionResult Index()
        {
            var viewModel = new ProductsIndexViewModel { Products = _repository.GetAll() };
            return View(viewModel);
        }

        [HttpGet]
        public IActionResult Import()
        {
            return View(new ImportViewModel());
        }

        [HttpPost]
        public IActionResult Import(string filePath)
        {
            var model = new ImportViewModel { FilePath = filePath };
            
            if (string.IsNullOrWhiteSpace(filePath))
            {
                ViewBag.Error = "Укажите путь к CSV-файлу";
                return View(model);
            }

            
            string resolvedPath = filePath;
            if (filePath.StartsWith("/") || filePath.StartsWith("\\"))
            {
                string webRootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
                resolvedPath = Path.Combine(webRootPath, filePath.TrimStart('/', '\\'));
            }

            model.Result = _importService.ImportFromCsv(resolvedPath);
            return View(model);
        }
                [HttpPost]
        public async Task<IActionResult> ImportUpload(IFormFile file)
        {
            var model = new ImportViewModel();
            
            if (file == null || file.Length == 0)
            {
                ViewBag.Error = "Файл не выбран или он пустой. Попробуйте еще раз.";
                return View("Import", model);
            }

    
            var tempFilePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".csv");
            
            using (var stream = new FileStream(tempFilePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            model.Result = _importService.ImportFromCsv(tempFilePath);
         
            if (System.IO.File.Exists(tempFilePath))
            {
                System.IO.File.Delete(tempFilePath);
            }

            return View("Import", model);
        }
    }
}