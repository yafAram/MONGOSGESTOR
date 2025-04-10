using MongoDB.Driver;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace WebApplication1.Services
{
    public class MongoDBService
    {
        private readonly IMongoClient _client;
        private readonly ILogger<MongoDBService> _logger;

        public MongoDBService(IMongoClient client, ILogger<MongoDBService> logger)
        {
            _client = client;
            _logger = logger;
        }

        public async Task CreateDatabaseAsync(string databaseName)
        {
            try
            {
                await _client.GetDatabase(databaseName).CreateCollectionAsync("DefaultCollection");
                _logger.LogInformation("Base de datos {Database} creada", databaseName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear base de datos");
                throw;
            }
        }

        // Método para crear respaldo (backup) completo usando mongodump
        // Sobrecarga que permite llamar con los dos parámetros: databaseName y backupFolder
        public async Task CreateBackupAsync(string databaseName, string backupFolder)
        {
            try
            {
                // Asegurarse de que exista la carpeta de backup
                Directory.CreateDirectory(backupFolder);

                var processInfo = new ProcessStartInfo
                {
                    FileName = "mongodump",
                    Arguments = $"--host mongodb --port 27017 --db {databaseName} --authenticationDatabase admin -u admin -p AdminPassword123 --out {backupFolder}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = new Process { StartInfo = processInfo };
                process.Start();
                var output = await process.StandardOutput.ReadToEndAsync();
                var error = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();

                if (process.ExitCode != 0)
                {
                    _logger.LogError("Error en backup: {Error}", error);
                    throw new Exception($"Error en backup: {error}");
                }

                _logger.LogInformation("Backup de {Database} creado en: {Path}", databaseName, backupFolder);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en CreateBackupAsync (sobrecarga con backupFolder)");
                throw;
            }
        }


        // Sobrecarga para cuando no se especifique la carpeta, se genera una automáticamente
        public async Task CreateBackupAsync(string databaseName)
        {
            var backupFolder = $"/backup/{databaseName}/{DateTime.Now.ToString("yyyyMMddHHmmss")}";
            await CreateBackupAsync(databaseName, backupFolder);
        }


        public async Task<List<string>> ListDatabasesAsync()
        {
            try
            {
                var databases = await _client.ListDatabaseNamesAsync();
                return databases.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al listar bases de datos");
                throw;
            }
        }

        // Método para restaurar un backup completo (desde una carpeta extraída)
        public async Task RestoreDatabaseAsync(string databaseName, string backupFolder)
        {
            backupFolder = backupFolder.Replace("\\", "/");
            try
            {
                var processInfo = new ProcessStartInfo
                {
                    FileName = "mongorestore",
                    Arguments = $"--host mongodb --port 27017 --authenticationDatabase admin " +
                                $"--authenticationMechanism SCRAM-SHA-1 -u admin -p AdminPassword123 " +
                                $"--drop --nsInclude={databaseName}.* {backupFolder}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = new Process { StartInfo = processInfo };
                process.Start();
                var output = await process.StandardOutput.ReadToEndAsync();
                var error = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();

                if (process.ExitCode != 0)
                {
                    _logger.LogError("Error en restauración: {Error}", error);
                    throw new Exception($"Error en restauración: {error}");
                }

                _logger.LogInformation("Backup de {Database} restaurado desde: {Folder}", databaseName, backupFolder);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al restaurar backup");
                throw;
            }
        }


        // Método para exportar datos de una colección (mongoexport)
        public async Task ExportCollectionAsync(string databaseName, string collectionName)
        {
            try
            {
                var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
                var exportPath = $"/data/exports/{timestamp}_{collectionName}.json";

                var processInfo = new ProcessStartInfo
                {
                    FileName = "docker",
                    Arguments = $"exec mongodb mongoexport --db {databaseName} " +
                                $"--collection {collectionName} --authenticationDatabase admin " +
                                $"-u admin -p adminpassword --out {exportPath} --jsonArray",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = new Process { StartInfo = processInfo };
                process.Start();
                var output = await process.StandardOutput.ReadToEndAsync();
                var error = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();
                if (process.ExitCode != 0)
                {
                    _logger.LogError("Error en exportación de colección: {Error}", error);
                    throw new Exception($"Error en exportación de colección: {error}");
                }
                _logger.LogInformation("Colección {Collection} exportada a: {Path}", collectionName, exportPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en ExportCollectionAsync");
                throw;
            }
        }

        // Método para importar datos a una colección (mongoimport)
        public async Task ImportCollectionAsync(string databaseName, string collectionName, IFormFile file)
        {
            try
            {
                // Carpeta temporal para almacenar el archivo
                var tempFolder = Path.Combine(Path.GetTempPath(), "mongo_import");
                Directory.CreateDirectory(tempFolder);
                var filePath = Path.Combine(tempFolder, file.FileName);
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                var processInfo = new ProcessStartInfo
                {
                    FileName = "docker",
                    Arguments = $"exec mongodb mongoimport --db {databaseName} " +
                                $"--collection {collectionName} --authenticationDatabase admin " +
                                $"-u admin -p AdminPassword123 --file {filePath} --jsonArray --drop",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var process = new Process { StartInfo = processInfo };
                process.Start();
                var output = await process.StandardOutput.ReadToEndAsync();
                var error = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();
                if (process.ExitCode != 0)
                {
                    _logger.LogError("Error en importación de colección: {Error}", error);
                    throw new Exception($"Error en importación de colección: {error}");
                }
                _logger.LogInformation("Colección {Collection} importada correctamente", collectionName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en ImportCollectionAsync");
                throw;
            }
        }
    }
}
