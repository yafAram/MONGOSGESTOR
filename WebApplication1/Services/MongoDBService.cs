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
                Directory.CreateDirectory(backupFolder);

                var processInfo = new ProcessStartInfo
                {
                    FileName = "mongodump",
                    Arguments = $"--host=mongodb --db={databaseName} " +  //  Host sin puerto explícito
                              $"--username=admin --password=AdminPassword123 --authenticationDatabase=admin " +  // ✅ Parámetros en formato largo
                              $"--out={backupFolder} --gzip",  //  Compresión habilitada
                    RedirectStandardError = true,  //  Solo necesitamos capturar errores
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = new Process { StartInfo = processInfo };
                process.Start();

                var error = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();

                if (process.ExitCode != 0)
                    throw new Exception($"mongodump error: {error}");

                _logger.LogInformation("Backup de {Database} creado en: {Path}", databaseName, backupFolder);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en CreateBackupAsync");
                throw;
            }
        }

        // Sobrecarga mejorada
        public async Task CreateBackupAsync(string databaseName)
        {
            var backupFolder = $"/app/backups/{databaseName}/{DateTime.Now:yyyyMMddHHmmss}";  //  Ruta dentro del volumen
            await CreateBackupAsync(databaseName, backupFolder);
        }





        public async Task<List<string>> ListDatabasesAsync()
        {
            var databases = new List<string>();
            using (var cursor = await _client.ListDatabaseNamesAsync())
            {
                while (await cursor.MoveNextAsync())
                {
                    foreach (var db in cursor.Current)
                    {
                        databases.Add(db); // db es el nombre (string) de la BD
                    }
                }
            }
            return databases;
        }

        // Método para restaurar un backup completo (desde una carpeta extraída)
        public List<string> GetAvailableBackups(string databaseName)
        {
            var backupRoot = $"/app/backups/{databaseName}";
            return Directory.GetDirectories(backupRoot)
                           .Select(Path.GetFileName)
                           .ToList();
        }

        public async Task RestoreDatabaseAsync(string databaseName, string backupFolder)
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "mongorestore",
                    Arguments = $"--host=mongodb -u admin -p AdminPassword123 " +
                                $"--authenticationDatabase admin --db={databaseName} " +
                                $"--dir={backupFolder} --gzip --drop",
                    RedirectStandardError = true
                }
            };

            process.Start();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                throw new Exception(await process.StandardError.ReadToEndAsync());
            }
        }


        // Método para exportar datos de una colección (mongoexport)
       

        public async Task<string> ExportCollectionAsync(string databaseName, string collectionName)
        {
            var backupDir = $"/app/backups/{databaseName}/{DateTime.Now:yyyyMMddHHmmss}";
            Directory.CreateDirectory(backupDir);

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "mongodump",
                    Arguments = $"--host=mongodb --username=admin --password=AdminPassword123 " +
                                $"--db={databaseName} --collection={collectionName} " +
                                $"--out={backupDir} --gzip --authenticationDatabase=admin",
                    RedirectStandardError = true
                }
            };

            process.Start();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
                throw new Exception(await process.StandardError.ReadToEndAsync());

            // Comprimir resultado
            var zipPath = $"{backupDir}.zip";
            ZipFile.CreateFromDirectory(backupDir, zipPath);
            Directory.Delete(backupDir, recursive: true);

            return zipPath;
        }

        public async Task ImportCollectionAsync(string databaseName, string collectionName, IFormFile file)
        {
            var tempDir = Path.GetTempFileName();
            File.Delete(tempDir);
            Directory.CreateDirectory(tempDir);

            // Guardar y extraer ZIP
            var zipPath = Path.Combine(tempDir, file.FileName);
            using (var stream = new FileStream(zipPath, FileMode.Create))
                await file.CopyToAsync(stream);
            ZipFile.ExtractToDirectory(zipPath, tempDir);

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "mongorestore",
                    Arguments = $"--host=mongodb --username=admin --password=AdminPassword123 " +
                                $"--db={databaseName} --collection={collectionName} " +
                                $"--dir={tempDir}/{databaseName}/{collectionName} --gzip --drop " +
                                "--authenticationDatabase=admin",
                    RedirectStandardError = true
                }
            };

            process.Start();
            await process.WaitForExitAsync();

            Directory.Delete(tempDir, recursive: true);

            if (process.ExitCode != 0)
                throw new Exception(await process.StandardError.ReadToEndAsync());
        }




    }
}
