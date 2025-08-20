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
    public class ProgramDbImporter
    {
        // private readonly string _rootFolder = @"C:\Users\4033375\Desktop\est1c Testing\programs sample";
        private readonly string _rootFolder = @"\\mypenm0opsapp01\SmartTorque$\Program Directory";

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

        private readonly List<string> errorLogs = new();
        private int successCount = 0;

        public void ImportAll()
        {
            var excelFiles = Directory.EnumerateFiles(_rootFolder, "*.xlsx", SearchOption.AllDirectories)
                                      .Where(f => !f.Contains("backup", StringComparison.OrdinalIgnoreCase))
                                      .ToList();

            Console.WriteLine($"📂 Found {excelFiles.Count} Excel files. Filtering to latest per model...");

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
                        WorkcellName = GetWorkcellName(file.FilePath),
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

                // 🔄 Progress counter in same line
                Console.Write($"\r📦 Processed {successCount} / {latestFiles.Count}");
            }

            Console.WriteLine("\n\n🚀 Import Completed!");
            Console.WriteLine($"✅ Total Successful Inserts: {successCount}");
            Console.WriteLine($"⚠️ Total Problem Files: {errorLogs.Count}");

            // 📝 Write error logs to file
            if (errorLogs.Count > 0)
            {
                string logPath = @"D:\Console Program\est1c Program\problem_files.txt";
                File.WriteAllLines(logPath, errorLogs);
                Console.WriteLine($"📝 Problem file details saved to: {logPath}");
            }
        }

        private List<ProgramDetails> ExtractProgramDetails(string filePath)
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
                        var rowNum = row.RowNumber();
                        var nonEmptyCells = row.CellsUsed().Count(c => !string.IsNullOrWhiteSpace(c.GetString()) && c.GetString() != "0");
                        if (nonEmptyCells < 2) continue;

                        var detail = new ProgramDetails
                        {
                            DetailId = Guid.NewGuid(),
                            ExcelRowNum = row.RowNumber(),
                            TargetTorque = GetSafeDecimal(row, columnIndexes, "TargetTorque", filePath, rowNum),
                            MinAngle = GetSafeDecimal(row, columnIndexes, "MinAngle", filePath, rowNum),
                            MaxAngle = GetSafeDecimal(row, columnIndexes, "MaxAngle", filePath, rowNum),
                            ScrewCount = GetSafeInt(row, columnIndexes, "ScrewCount", filePath, rowNum),
                            SpeedRPM = GetSafeInt(row, columnIndexes, "SpeedRPM", filePath, rowNum),
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

        private decimal GetSafeDecimal(IXLRow row, Dictionary<string, int> indexes, string key, string filePath, int rowNum)
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

        private int GetSafeInt(IXLRow row, Dictionary<string, int> indexes, string key, string filePath, int rowNum)
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

        private Dictionary<string, int> GetColumnIndexes(IXLRow headerRow)
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

        private string ExtractTorqueUnit(string header) =>
            header.Contains("kgf") ? "kgf.cm" : header.Contains("lbf") ? "lbf-in" : "Unknown";

        private bool IsTurn(string header) =>
            header.Contains("(Turn)", StringComparison.OrdinalIgnoreCase);

        private string GetWorkcellName(string filePath)
        {
            // Get relative path after root
            var relativePath = Path.GetRelativePath(_rootFolder, filePath);

            // Split relative path and take the first folder
            var firstFolder = relativePath.Split(Path.DirectorySeparatorChar)[0];

            return string.IsNullOrWhiteSpace(firstFolder) ? "Unknown" : firstFolder;
        }

    }
}
