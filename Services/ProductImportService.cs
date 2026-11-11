using ECommerceCsvImport.Models;
using ECommerceCsvImport.Services;

namespace ECommerceCsvImport.Services 
{
    public class ProductImportService : IProductImportService
    {
        private readonly IProductRepository _repository;

        public ProductImportService(IProductRepository repository)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        public ImportResult ImportFromCsv(string filePath)
        {
            var result = new ImportResult();

            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                result.Errors.Add(new ImportError { RowNumber = 0, Message = "Файл не найден" });
                return result;
            }

            try
            {
                var lines = File.ReadAllLines(filePath);
                return ImportFromLines(lines);
            }
            catch (Exception ex)
            {
                result.Errors.Add(new ImportError { RowNumber = 0, Message = $"Ошибка чтения файла: {ex.Message}" });
                return result;
            }
        }

        public ImportResult ImportFromLines(IEnumerable<string> lines)
        {
            var result = new ImportResult();
            var lineList = lines.Where(l => !string.IsNullOrWhiteSpace(l)).ToList();

            if (lineList.Count == 0)
            {
                result.Errors.Add(new ImportError { RowNumber = 0, Message = "Данные для импорта отсутствуют" });
                return result;
            }

          
            var headerLine = lineList[0];
            var headers = CsvParser.Parse(headerLine);
            var columnMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < headers.Count; i++)
            {
                if (!columnMap.ContainsKey(headers[i]))
                    columnMap[headers[i]] = i;
            }

           
            string[] requiredColumns = { "sku", "name", "price", "stock", "is_active", "brand", "warranty_months" };
            if (requiredColumns.Any(c => !columnMap.ContainsKey(c)))
            {
                result.Errors.Add(new ImportError { RowNumber = 1, Message = "Неверные заголовки CSV" });
                return result;
            }

            var importedSkus = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 1; i < lineList.Count; i++)
            {
                var line = lineList[i];
                var physicalRowNumber = i + 1; // Номер строки в файле
                result.TotalRows++;

                var values = CsvParser.Parse(line);
                
                string GetValue(string colName)
                {
                    if (columnMap.TryGetValue(colName, out int idx) && idx < values.Count)
                        return values[idx];
                    return string.Empty;
                }

                var rowErrors = new List<ImportError>();

                string sku = GetValue("sku").Trim();
                if (string.IsNullOrWhiteSpace(sku))
                    rowErrors.Add(new ImportError { RowNumber = physicalRowNumber, Message = "Поле sku не может быть пустым" });

                string name = GetValue("name").Trim();
                if (string.IsNullOrWhiteSpace(name))
                    rowErrors.Add(new ImportError { RowNumber = physicalRowNumber, Message = "Поле name не может быть пустым" });

                string priceStr = GetValue("price").Trim();
                if (!decimal.TryParse(priceStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal price) || price <= 0)
                    rowErrors.Add(new ImportError { RowNumber = physicalRowNumber, Message = "Поле price должно быть числом больше 0" });

                string stockStr = GetValue("stock").Trim();
                if (!int.TryParse(stockStr, out int stock) || stock < 0)
                    rowErrors.Add(new ImportError { RowNumber = physicalRowNumber, Message = "Остаток на складе должен быть целым числом не меньше 0" });

                string isActiveStr = GetValue("is_active").Trim().ToLower();
                bool isActive = isActiveStr == "true" || isActiveStr == "1";
                if (isActiveStr != "true" && isActiveStr != "false" && isActiveStr != "1" && isActiveStr != "0")
                    rowErrors.Add(new ImportError { RowNumber = physicalRowNumber, Message = "Поле is_active должно иметь значение true, false, 1 или 0" });

                string brand = GetValue("brand").Trim();
                if (string.IsNullOrWhiteSpace(brand))
                    rowErrors.Add(new ImportError { RowNumber = physicalRowNumber, Message = "Поле brand обязательно для варианта «Электроника»" });

                string warrantyStr = GetValue("warranty_months").Trim();
                if (!int.TryParse(warrantyStr, out int warranty) || warranty < 0 || warranty > 60)
                    rowErrors.Add(new ImportError { RowNumber = physicalRowNumber, Message = "Гарантия должна быть целым числом от 0 до 60 месяцев" });

               
                if (rowErrors.Count > 0)
                {
                    result.Errors.AddRange(rowErrors);
                    continue;
                }

                
                if (_repository.GetBySku(sku) != null || importedSkus.Contains(sku))
                {
                    result.Errors.Add(new ImportError { RowNumber = physicalRowNumber, Message = "Артикул товара уже существует" });
                    continue;
                }

                
                var product = new Product
                {
                    Sku = sku,
                    Name = name,
                    Description = GetValue("description").Trim(),
                    Price = price,
                    Currency = string.IsNullOrWhiteSpace(GetValue("currency")) ? "RUB" : GetValue("currency").Trim().ToUpperInvariant(),
                    CategoryName = string.IsNullOrWhiteSpace(GetValue("category_name")) ? "Без категории" : GetValue("category_name").Trim(),
                    StockQuantity = stock,
                    ImageUrl = GetValue("image_url").Trim(),
                    IsActive = isActive,
                    Brand = brand,
                    WarrantyMonths = warranty,
                    ModelNumber = GetValue("model_number").Trim()
                };

                _repository.Add(product);
                importedSkus.Add(sku);
                result.ImportedCount++;
            }

            if (result.TotalRows == 0 && result.Errors.Count == 0)
            {
                result.Errors.Add(new ImportError { RowNumber = 0, Message = "Данные для импорта отсутствуют" });
            }

            return result;
        }
    }
}