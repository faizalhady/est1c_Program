using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClosedXML.Excel;
using eST1C_ProgramImporter.Models;
using eST1C_ProgramImporter.Data;
using Microsoft.EntityFrameworkCore;

namespace eST1C_ProgramImporter.Services
{
    public class ProgramDbImporterFromList
    {
        // Path to your .txt file containing file paths (one per line)
        private static readonly string ListFilePath  = @"D:\Console Program\est1c Program\eST1C_ProgramImporter\txt\programs_to_import.txt";

        // Path for error log (folder will be created if missing)
        private static readonly string ErrorLogPath  = @"D:\Console Program\est1c Program\eST1C_ProgramImporter\txt\problem_files_from_list.txt";

        private static readonly Dictionary<string, string> HeaderMap = new(StringComparer.OrdinalIgnoreCase)
        {
            { "Torque", "TargetTorque" },
            { "TC Target Torque/ AC Max Torque (Kgf_cm)", "TargetTorque" },
            { "TC Target Torque/ AC Max Torque (Lbf_in)", "TargetTorque" },
            { "Min Angle", "MinAngle" },
            { "Min Angle (°)", "MinAngle" },
            { "Max Angle", "MaxAngle" },
            { "Max Angle (°)", "MaxAngle" },
            { "Screw Count", "ScrewCount" },
            { "Speed", "SpeedRPM" },
            { "Speed (RPM)", "SpeedRPM" }
        };

        private static List<string> errorLogs = new();
        private static int successCount = 0;

        public static void ImportFromList()
        {
            errorLogs.Clear();
            successCount = 0;

            // Ensure list file exists
            if (!File.Exists(ListFilePath))
            {
                Console.WriteLine($"❌ List file not found: {ListFilePath}");
                return;
            }

            // Ensure error-log directory exists
            var errDir = Path.GetDirectoryName(ErrorLogPath);
            if (!string.IsNullOrWhiteSpace(errDir))
                Directory.CreateDirectory(errDir);

            // Read & normalize lines: trim, ignore blanks/comments, strip quotes, dedupe
            var allPaths = File.ReadAllLines(ListFilePath)
                               .Select(l => l?.Trim())
                               .Where(l => !string.IsNullOrWhiteSpace(l) && !l!.StartsWith("#"))
                               .Select(NormalizePath)
                               .Distinct(StringComparer.OrdinalIgnoreCase)
                               .ToList();

            // Keep only existing .xlsx and not "backup"
            var excelFiles = allPaths
                .Where(File.Exists)
                .Where(p => p.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
                .Where(p => !p.Contains("backup", StringComparison.OrdinalIgnoreCase))
                .ToList();

            Console.WriteLine($"📂 From list: {allPaths.Count} lines → valid .xlsx: {excelFiles.Count}");

            // Latest file per model (model = filename without extension)
            var latestFiles = excelFiles
                .Select(f => new
                {
                    FilePath = f,
                    ModelName = Path.GetFileNameWithoutExtension(f),
                    FileDate = File.GetLastWriteTime(f)
                })
                .GroupBy(x => x.ModelName, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.OrderByDescending(x => x.FileDate).First())
                .ToList();

            Console.WriteLine($"📦 Processing {latestFiles.Count} unique models...");

            using var db = new MainDbContext();

            foreach (var file in latestFiles)
            {
                try
                {
                    var existingProgram = db.Program.FirstOrDefault(p => p.Model == file.ModelName);

                    if (existingProgram != null)
                    {
                        db.ProgramDetails.RemoveRange(db.ProgramDetails.Where(d => d.ProgramId == existingProgram.ProgramId));
                        db.Program.Remove(existingProgram);
                        db.SaveChanges();
                    }

                    var programEntity = new ProgramHeaders
                    {
                        ProgramId = Guid.NewGuid(),
                        Model = file.ModelName,
                        WorkcellName = GuessWorkcellFromPath(file.FilePath),
                        FilePath = file.FilePath,
                        FileDate = file.FileDate,
                        DateExtracted = DateTime.Now,
                        ProgramDetails = ExtractProgramDetails(file.FilePath)
                    };

                    if (programEntity.ProgramDetails.Count == 0)
                    {
                        errorLogs.Add($"❌ No details found in file: {file.FilePath}");
                        continue;
                    }

                    db.Program.Add(programEntity);
                    db.SaveChanges();
                    successCount++;
                }
                catch (IOException ioEx) when (ioEx.Message.Contains("being used by another process"))
                {
                    errorLogs.Add($"⏭ Skipping locked file: {file.FilePath}");
                }
                catch (Exception ex)
                {
                    errorLogs.Add($"❌ Error in file: {file.FilePath}\n   Reason: {ex.Message}");
                }

                Console.Write($"\r📦 Processed {successCount} / {latestFiles.Count}");
            }

            Console.WriteLine("\n\n🚀 Import Completed!");
            Console.WriteLine($"✅ Total Successful Inserts: {successCount}");
            Console.WriteLine($"⚠️ Total Problem Files: {errorLogs.Count}");

            if (errorLogs.Count > 0)
            {
                File.WriteAllLines(ErrorLogPath, errorLogs);
                Console.WriteLine($"📝 Problem file details saved to: {ErrorLogPath}");
            }
        }

        private static List<ProgramDetails> ExtractProgramDetails(string filePath)
        {
            var detailsList = new List<ProgramDetails>();

            try
            {
                using var workbook = new XLWorkbook(filePath);

                foreach (var worksheet in workbook.Worksheets)
                {
                    var rows = worksheet.RowsUsed().ToList();
                    if (rows.Count < 2) continue;

                    var columnIndexes = GetColumnIndexes(rows[0]);
                    if (columnIndexes.Count == 0) continue;

                    foreach (var row in rows.Skip(1))
                    {
                        var nonEmptyCells = row.CellsUsed().Count(c => !string.IsNullOrWhiteSpace(c.GetString()) && c.GetString() != "0");
                        if (nonEmptyCells < 2) continue;

                        var detail = new ProgramDetails
                        {
                            DetailId = Guid.NewGuid(),
                            ExcelRowNum = row.RowNumber(),
                            TargetTorque = GetSafeDecimal(row, columnIndexes, "TargetTorque"),
                            MinAngle = GetSafeDecimal(row, columnIndexes, "MinAngle"),
                            MaxAngle = GetSafeDecimal(row, columnIndexes, "MaxAngle"),
                            ScrewCount = GetSafeInt(row, columnIndexes, "ScrewCount"),
                            SpeedRPM = GetSafeInt(row, columnIndexes, "SpeedRPM"),
                            TorqueUnit = columnIndexes.ContainsKey("TargetTorque")
                                ? ExtractTorqueUnit(rows[0].Cell(columnIndexes["TargetTorque"]).GetString())
                                : "Unknown",
                            AngleUnit = "Degree"
                        };

                        if (columnIndexes.ContainsKey("MinAngle") && IsTurn(rows[0].Cell(columnIndexes["MinAngle"]).GetString()))
                            detail.MinAngle *= 360;

                        if (columnIndexes.ContainsKey("MaxAngle") && IsTurn(rows[0].Cell(columnIndexes["MaxAngle"]).GetString()))
                            detail.MaxAngle *= 360;

                        detailsList.Add(detail);
                    }

                    if (detailsList.Count > 0) break;
                }
            }
            catch (Exception ex)
            {
                errorLogs.Add($"❌ Failed to read details in: {filePath}\n   Reason: {ex.Message}");
            }

            return detailsList;
        }

        private static decimal GetSafeDecimal(IXLRow row, Dictionary<string, int> indexes, string key)
        {
            try
            {
                if (indexes.ContainsKey(key))
                {
                    var cellValue = row.Cell(indexes[key]).GetFormattedString().Trim();
                    if (decimal.TryParse(cellValue, out decimal result))
                        return Math.Round(result, 2, MidpointRounding.AwayFromZero);
                }
            }
            catch { }
            return 0m;
        }

        private static int GetSafeInt(IXLRow row, Dictionary<string, int> indexes, string key)
        {
            try
            {
                if (indexes.ContainsKey(key))
                {
                    var cellValue = row.Cell(indexes[key]).GetValue<string>().Trim();
                    if (int.TryParse(cellValue, out int result))
                        return result;
                }
            }
            catch { }
            return 0;
        }

        private static Dictionary<string, int> GetColumnIndexes(IXLRow headerRow)
        {
            var indexes = new Dictionary<string, int>();
            foreach (var kvp in HeaderMap)
            {
                string predefinedHeader = kvp.Key.Trim().ToLowerInvariant();
                string dbField = kvp.Value;

                foreach (var cell in headerRow.CellsUsed())
                {
                    string cellText = cell.GetValue<string>().Trim().ToLowerInvariant();
                    if (cellText.Length > 500) continue;
                    if (cellText.Contains(predefinedHeader))
                    {
                        indexes[dbField] = cell.Address.ColumnNumber;
                        break;
                    }
                }
            }
            return indexes;
        }

        private static string ExtractTorqueUnit(string header) =>
            header.Contains("kgf") ? "kgf.cm" : header.Contains("lbf") ? "lbf-in" : "Unknown";

        private static bool IsTurn(string header) =>
            header.Contains("(Turn)", StringComparison.OrdinalIgnoreCase);

        private static string GuessWorkcellFromPath(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath)) return "Unknown";
            var dir = Path.GetDirectoryName(filePath) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(dir)) return "Unknown";
            var parts = dir.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return parts.Length > 0 ? parts[^1] : "Unknown";
        }

        private static string NormalizePath(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return string.Empty;
            s = s.Trim();
            // Strip surrounding quotes if present
            if (s.Length >= 2 && ((s[0] == '"' && s[^1] == '"') || (s[0] == '\'' && s[^1] == '\'')))
                s = s.Substring(1, s.Length - 2).Trim();
            return s;
        }
    }
}
