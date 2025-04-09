using MongoDB.Driver;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Mvc;
using System.IO.Compression;

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
                await _client.GetDatabase(databaseName)
                    .CreateCollectionAsync("DefaultCollection");
                _logger.LogInformation("Base de datos {Database} creada", databaseName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear base de datos");
                throw;
            }
        }

        public async Task CreateBackupAsync(string databaseName)
        {
            try
            {
                var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
                var backupPath = $"/data/backups/{timestamp}";

                var processInfo = new ProcessStartInfo
                {
                    FileName = "docker",
                    Arguments = $"exec mongodb mongodump " +
                                $"--db {databaseName} " +
                                $"--authenticationDatabase admin " +
                                $"-u admin " +
                                $"-p adminpassword " +
                                $"--out {backupPath}",
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

                _logger.LogInformation("Backup de {Database} creado en: {Path}", databaseName, backupPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en CreateBackupAsync");
                throw;
            }
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

        public async Task RestoreBackupAsync(string backupName)
        {
            try
            {
                var processInfo = new ProcessStartInfo
                {
                    FileName = "docker",
                    Arguments = $"exec mongodb mongorestore " +
                                $"--authenticationDatabase admin " +
                                $"-u admin " +
                                $"-p adminpassword " +
                                $"--dir /data/backups/{backupName} " +
                                $"--drop",
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

                _logger.LogInformation("Backup {BackupName} restaurado exitosamente", backupName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en RestoreBackupAsync");
                throw;
            }
        }

        public async Task ExportCollectionAsync(string databaseName, string collectionName)
        {
            try
            {
                var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
                var exportPath = $"/data/exports/{timestamp}_{collectionName}.json";

                var processInfo = new ProcessStartInfo
                {
                    FileName = "docker",
                    Arguments = $"exec mongodb mongoexport " +
                                $"--db {databaseName} " +
                                $"--collection {collectionName} " +
                                $"--authenticationDatabase admin " +
                                $"-u admin " +
                                $"-p adminpassword " +
                                $"--out {exportPath} " +
                                $"--jsonArray",
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
                    _logger.LogError("Error en exportación: {Error}", error);
                    throw new Exception($"Error en exportación: {error}");
                }

                _logger.LogInformation("Colección {Collection} exportada a: {Path}",
                    collectionName, exportPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en ExportCollectionAsync");
                throw;
            }
        }

        public List<string> GetAvailableBackups()
        {
            try
            {
                const string backupsPath = @"C:\mongo-backups";
                return Directory.GetDirectories(backupsPath)
                    .Select(Path.GetFileName)
                    .OrderByDescending(d => d)
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al listar backups");
                throw;
            }
        }


        // En MongoDBService.cs
        public async Task ExportDatabaseAsync(string databaseName, string backupFolder)
        {
            try
            {
                var processInfo = new ProcessStartInfo
                {
                    FileName = "mongodump",
                    Arguments = $"--uri=mongodb://admin:AdminPassword123@mongodb:27017/ " +
                                $"--authenticationDatabase=admin " +
                                $"--db={databaseName} " +
                                $"--out={backupFolder}",  // Ahora usa directamente la ruta final
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
                    _logger.LogError("Error en exportación: {Error}", error);
                    throw new Exception($"Error en exportación: {error}");
                }

                _logger.LogInformation("Backup de {Database} creado en: {Folder}", databaseName, backupFolder);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al exportar base de datos");
                throw;  // Mantenemos el throw para manejar en el controlador
            }
        }

       

        public async Task RestoreDatabaseAsync(string databaseName, string backupFolder)
        {
            // Aseguramos que la ruta use barras '/' (Linux)
            backupFolder = backupFolder.Replace("\\", "/");
            try
            {
                var processInfo = new ProcessStartInfo
                {
                    FileName = "docker",
                    Arguments = $"exec mongodb mongorestore " +
                                $"--authenticationDatabase admin " +
                                $"--authenticationMechanism SCRAM-SHA-1 " +
                                $"-u admin " +
                                $"-p AdminPassword123 " +
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




    }
}