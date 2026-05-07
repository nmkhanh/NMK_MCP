using System.Diagnostics;
using System.IO;
using Autodesk.Revit.DB;
using Microsoft.Win32;
using RevitMcpAddin.Models;

namespace RevitMcpAddin.Services
{
    // ═══════════════════════════════════════════════════════════════════════
    //  F_Print — Sheet to PDF via Revit PrintManager
    //  Part of the RevitService partial class.
    //
    //  Uses Revit's built-in PrintManager API to send print jobs to a
    //  virtual PDF printer (PDF24 is preferred; falls back to any printer
    //  whose name contains "PDF", then to other known virtual printers).
    //
    //  Paper size is auto-detected per sheet from the title block dimensions
    //  and matched to the closest available paper size in the printer's list.
    // ═══════════════════════════════════════════════════════════════════════

    public sealed partial class RevitService
    {
        private const int PrintTimeoutMs = 180_000; // 3 minutes

        // Standard ISO / ANSI paper sizes (portrait short × long, mm)
        private static readonly (double ShortMm, double LongMm, string Label)[] _stdSizes =
        {
            (210,  297,  "A4"),
            (297,  420,  "A3"),
            (420,  594,  "A2"),
            (594,  841,  "A1"),
            (841,  1189, "A0"),
            (216,  279,  "Letter"),
            (279,  432,  "Tabloid"),
        };

        // ── Public method ─────────────────────────────────────────────────

        /// <summary>
        /// Prints one or more ViewSheets to PDF via a virtual PDF printer.
        /// Paper size is auto-detected from each sheet's title block.
        /// </summary>
        public Task<object> PrintSheetToPdfAsync(PrintSheetRequest request, CancellationToken ct = default)
        {
            return _queue.EnqueueAsync(async uiApp =>
            {
                var doc = uiApp.ActiveUIDocument?.Document
                    ?? throw new InvalidOperationException("No active document.");

                // ── 1. Resolve output folder ──────────────────────────────
                var outputFolder = string.IsNullOrWhiteSpace(request.OutputFolder)
                    ? (!string.IsNullOrEmpty(doc.PathName)
                        ? Path.GetDirectoryName(doc.PathName)!
                        : Environment.GetFolderPath(Environment.SpecialFolder.Desktop))
                    : request.OutputFolder;
                Directory.CreateDirectory(outputFolder);

                // ── 2. Find PDF24 printer (required) ─────────────────────
                var printerName = FindPdf24PrinterName()
                    ?? throw new InvalidOperationException(
                        "PDF24 is not installed or was not detected as a printer.\n" +
                        "Please install PDF24 from https://www.pdf24.org, restart Revit, and try again.\n" +
                        "PDF24 is required — other virtual printers (including 'Microsoft Print to PDF') " +
                        "always show interactive Save-As dialogs that block Revit indefinitely.");

                Trace.WriteLine($"[RevitMCP][PRINT] Using printer: {printerName}");

                // ── 2a. Configure PDF24 for silent auto-save (suppress dialogs) ──
                PDF24Setup.SetAutoSave(outputFolder);

                // ── 3. Collect target sheets ──────────────────────────────
                var allSheets = new FilteredElementCollector(doc)
                    .OfClass(typeof(ViewSheet))
                    .Cast<ViewSheet>()
                    .Where(s => !s.IsTemplate)
                    .ToList();

                List<ViewSheet> targetSheets;
                if (request.SheetNumbers == null || request.SheetNumbers.Count == 0)
                {
                    targetSheets = allSheets.OrderBy(s => s.SheetNumber).ToList();
                }
                else
                {
                    var requested = new HashSet<string>(request.SheetNumbers, StringComparer.OrdinalIgnoreCase);

                    var missing = request.SheetNumbers
                        .Where(n => !allSheets.Any(s =>
                            string.Equals(s.SheetNumber, n, StringComparison.OrdinalIgnoreCase)))
                        .ToList();
                    if (missing.Count > 0)
                        Trace.WriteLine($"[RevitMCP][PRINT] Sheets not found: {string.Join(", ", missing)}");

                    targetSheets = allSheets
                        .Where(s => requested.Contains(s.SheetNumber))
                        .OrderBy(s => s.SheetNumber)
                        .ToList();
                }

                if (targetSheets.Count == 0)
                    throw new InvalidOperationException(
                        "No sheets found matching the specified criteria. " +
                        "Omit 'sheetNumbers' to print all sheets.");

                // ── 3a. Pre-create custom Windows paper forms for every unique sheet size ─
                //        Must run BEFORE pm.SelectNewPrintDriver so the driver loads them.
                var distinctDims = targetSheets
                    .Select(s => GetSheetDimensionsMm(doc, s))
                    .Select(d => (W: (int)Math.Round(d.WidthMm), H: (int)Math.Round(d.HeightMm)))
                    .Distinct()
                    .ToList();
                foreach (var (w, h) in distinctDims)
                {
                    var formName = $"RMCP_{w}x{h}";
                    try
                    {
                        PaperFormManager.CreatePaperFormMM(formName, w, h);
                        Trace.WriteLine($"[RevitMCP][PRINT] Registered paper form: {formName} ({w}\u00d7{h} mm)");
                    }
                    catch (InvalidOperationException ex) when (ex.Message == "Form already exists")
                    {
                        // Already registered from a previous session — that is fine.
                    }
                    catch (Exception ex)
                    {
                        Trace.WriteLine(
                            $"[RevitMCP][PRINT] Warning: could not register paper form '{formName}': {ex.Message}");
                    }
                }

                // ── 4. Configure PrintManager ─────────────────────────────
                // All PrintManager write operations (SelectNewPrintDriver, property sets, Apply)
                // modify the Revit document model and MUST run inside a Transaction.
                // SubmitPrint() does NOT modify the model and MUST run OUTSIDE a Transaction.
                var pm = doc.PrintManager;

                var docTitle = !string.IsNullOrEmpty(doc.PathName)
                    ? Path.GetFileNameWithoutExtension(doc.PathName)
                    : doc.Title;
                var baseFileName = !string.IsNullOrWhiteSpace(request.OutputFileName)
                    ? SanitizeFileName(request.OutputFileName)
                    : SanitizeFileName(docTitle);

                var sheetResults = new List<object>();

                // ── 5a. Combined: all sheets in one print job ─────────────
                if (request.Combine)
                {
                    var (w0, h0) = GetSheetDimensionsMm(doc, targetSheets[0]);
                    var outputFile = Path.Combine(outputFolder, baseFileName + ".pdf");
                    if (File.Exists(outputFile)) File.Delete(outputFile);
                    PDF24Setup.SetAutoSave(outputFolder, baseFileName);

                    // All PrintManager writes must be inside a transaction
                    using (var tx = new Transaction(doc, "RevitMCP: Configure Print"))
                    {
                        tx.Start();
                        pm.SelectNewPrintDriver(printerName);
                        pm.PrintToFile     = true;
                        pm.PrintToFileName = outputFile;
                        pm.CombinedFile    = true;
                        pm.PrintSetup.CurrentPrintSetting = pm.PrintSetup.InSession;
                        var pp = pm.PrintSetup.InSession.PrintParameters;
                        pp.ColorDepth               = ParseColorDepth(request.ColorMode);
                        pp.RasterQuality            = ParseRasterQuality(request.RasterQuality);
                        pp.ZoomType                 = ZoomType.FitToPage;
                        pp.PaperPlacement           = PaperPlacementType.Center;
                        // NOTE: MarginType must NOT be set when PaperPlacement = Center.
                        pp.HideScopeBoxes           = true;
                        pp.HideUnreferencedViewTags = true;
                        pp.HideReforWorkPlanes      = true;
                        pp.HideCropBoundaries       = true;
                        pp.PaperSize                = FindBestPaperSize(pm, w0, h0);
                        pp.PageOrientation          = w0 > h0
                            ? PageOrientationType.Landscape
                            : PageOrientationType.Portrait;
                        pm.PrintRange = PrintRange.Select;
                        // NOTE: InSession.Views is read-only — assign a new ViewSet object.
                        var newViewSet = new ViewSet();
                        foreach (var s in targetSheets)
                            newViewSet.Insert(s);
                        pm.ViewSheetSetting.InSession.Views = newViewSet;
                        pm.Apply();
                        tx.Commit();
                    }

                    // SubmitPrint must run OUTSIDE the transaction
                    pm.SubmitPrint();

                    Trace.WriteLine(
                        $"[RevitMCP][PRINT] Combined job submitted: {targetSheets.Count} sheet(s) → {outputFile}");

                    foreach (var s in targetSheets)
                    {
                        var (wMm, hMm) = GetSheetDimensionsMm(doc, s);
                        sheetResults.Add(new
                        {
                            number    = s.SheetNumber,
                            name      = s.Name,
                            widthMm   = Math.Round(wMm,  1),
                            heightMm  = Math.Round(hMm,  1),
                            paperSize = AutoDetectPaperLabel(wMm, hMm)
                        });
                    }

                    return (object)new
                    {
                        success      = true,
                        sheetCount   = targetSheets.Count,
                        sheets       = sheetResults,
                        printer      = printerName,
                        outputFolder,
                        outputFile,
                        message = $"Submitted {targetSheets.Count} sheet(s) to '{printerName}'. " +
                                  $"PDF will be saved to '{outputFile}'."
                    };
                }

                // ── 5b. Individual: one PDF per sheet ─────────────────────
                var outputFiles = new List<string>();

                foreach (var sheet in targetSheets)
                {
                    var (wMm, hMm) = GetSheetDimensionsMm(doc, sheet);
                    var safeName   = SanitizeFileName($"{sheet.SheetNumber} - {sheet.Name}");
                    var outputFile = Path.Combine(outputFolder, safeName + ".pdf");
                    if (File.Exists(outputFile)) File.Delete(outputFile);
                    PDF24Setup.SetAutoSave(outputFolder, safeName);

                    // All PrintManager writes must be inside a transaction (per sheet)
                    using (var tx = new Transaction(doc, $"RevitMCP: Configure Print - {sheet.SheetNumber}"))
                    {
                        tx.Start();
                        pm.SelectNewPrintDriver(printerName);
                        pm.PrintToFile     = true;
                        pm.PrintToFileName = outputFile;
                        pm.CombinedFile    = false;
                        pm.PrintSetup.CurrentPrintSetting = pm.PrintSetup.InSession;
                        var pp = pm.PrintSetup.InSession.PrintParameters;
                        pp.ColorDepth               = ParseColorDepth(request.ColorMode);
                        pp.RasterQuality            = ParseRasterQuality(request.RasterQuality);
                        pp.ZoomType                 = ZoomType.FitToPage;
                        pp.PaperPlacement           = PaperPlacementType.Center;
                        // NOTE: MarginType must NOT be set when PaperPlacement = Center.
                        pp.HideScopeBoxes           = true;
                        pp.HideUnreferencedViewTags = true;
                        pp.HideReforWorkPlanes      = true;
                        pp.HideCropBoundaries       = true;
                        pp.PaperSize                = FindBestPaperSize(pm, wMm, hMm);
                        pp.PageOrientation          = wMm > hMm
                            ? PageOrientationType.Landscape
                            : PageOrientationType.Portrait;
                        pm.Apply();
                        tx.Commit();
                    }

                    // SubmitPrint must run OUTSIDE the transaction
                    pm.SubmitPrint(sheet);

                    outputFiles.Add(outputFile);
                    Trace.WriteLine($"[RevitMCP][PRINT] Sheet {sheet.SheetNumber} → {outputFile}");

                    sheetResults.Add(new
                    {
                        number    = sheet.SheetNumber,
                        name      = sheet.Name,
                        widthMm   = Math.Round(wMm,  1),
                        heightMm  = Math.Round(hMm,  1),
                        paperSize = AutoDetectPaperLabel(wMm, hMm),
                        file      = outputFile
                    });
                }

                return (object)new
                {
                    success      = true,
                    sheetCount   = targetSheets.Count,
                    sheets       = sheetResults,
                    printer      = printerName,
                    outputFolder,
                    outputFiles,
                    message = $"Submitted {targetSheets.Count} sheet(s) to '{printerName}'."
                };

            }, ct, timeoutMs: PrintTimeoutMs);
        }

        // ── Private Helpers ──────────────────────────────────────────────

        /// <summary>
        /// Searches installed printers for PDF24.  Returns <see langword="null"/> if PDF24
        /// is not installed.
        /// <para>
        /// PDF24 is the only supported virtual PDF printer because it supports registry-based
        /// silent auto-save.  Other printers ("Microsoft Print to PDF", etc.) always show
        /// an interactive Save-As dialog that would block the Revit main thread indefinitely.
        /// </para>
        /// </summary>
        private static string? FindPdf24PrinterName()
        {
            var printers = new List<string>();

            // System-wide printers (HKLM)
            using (var key = Registry.LocalMachine.OpenSubKey(
                       @"SYSTEM\CurrentControlSet\Control\Print\Printers"))
            {
                if (key != null)
                    printers.AddRange(key.GetSubKeyNames());
            }

            // Per-user connection printers (HKCU)
            using (var key = Registry.CurrentUser.OpenSubKey(
                       @"Software\Microsoft\Windows NT\CurrentVersion\Devices"))
            {
                if (key != null)
                    printers.AddRange(key.GetValueNames());
            }

            return printers
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault(p => p.IndexOf("PDF24", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        /// <summary>
        /// Finds the best matching <see cref="PaperSize"/> from the printer's available list.
        /// Match priority:
        /// 1. Exact name match      ("A3" == "A3")
        /// 2. Partial name match    ("A3" ⊆ "ISO A3" or "A3 (297×420 mm)")
        /// 3. Custom paper form     ("RMCP_420x297" — registered via <see cref="PaperFormManager"/>)
        /// 4. Nearest standard size (by long-side dimension: A4 → A3 → A2 → A1 → A0 → …)
        /// 5. First available size  (absolute last resort)
        /// </summary>
        private static PaperSize FindBestPaperSize(PrintManager pm, double widthMm, double heightMm)
        {
            var label = AutoDetectPaperLabel(widthMm, heightMm); // e.g. "A3"
            var sizes = pm.PaperSizes.Cast<PaperSize>().ToList();

            if (sizes.Count == 0)
                throw new InvalidOperationException(
                    $"The PDF printer '{pm.PrinterName}' reported no paper sizes. " +
                    "Please check its configuration.");

            // 1. Exact name match (case-insensitive)
            var hit = sizes.FirstOrDefault(ps =>
                string.Equals(ps.Name.Trim(), label, StringComparison.OrdinalIgnoreCase));
            if (hit != null) return hit;

            // 2. Partial name match (e.g. "ISO A3", "A3 (297 × 420 mm)", "A3 Transverse")
            hit = sizes.FirstOrDefault(ps =>
                ps.Name.IndexOf(label, StringComparison.OrdinalIgnoreCase) >= 0);
            if (hit != null) return hit;

            // 3. Custom paper form registered by PaperFormManager before SelectNewPrintDriver
            var customFormName = $"RMCP_{(int)Math.Round(widthMm)}x{(int)Math.Round(heightMm)}";
            hit = sizes.FirstOrDefault(ps =>
                string.Equals(ps.Name.Trim(), customFormName, StringComparison.OrdinalIgnoreCase));
            if (hit != null)
            {
                Trace.WriteLine(
                    $"[RevitMCP][PRINT] Using custom paper form '{customFormName}' " +
                    $"({Math.Round(widthMm)}×{Math.Round(heightMm)} mm).");
                return hit;
            }

            // 4. No standard or custom form matched.
            //    The Revit 2026 API does not expose PaperSize dimensions and PaperFormManager
            //    may have failed (e.g. access denied).  Fall back through the nearest standard
            //    sizes by long-side dimension so we don't clip content.
            var longSide = Math.Max(widthMm, heightMm);

            // Build a priority list that starts with the physically closest standard size.
            string[] fallbackOrder;
            if      (longSide <= 297)  fallbackOrder = ["A4","A3","Letter","A2","A1","A0","Tabloid"];
            else if (longSide <= 420)  fallbackOrder = ["A3","A2","A4","A1","A0","Tabloid","Letter"];
            else if (longSide <= 594)  fallbackOrder = ["A2","A1","A3","A0","Tabloid","A4","Letter"];
            else if (longSide <= 841)  fallbackOrder = ["A1","A0","A2","Tabloid","A3","A4","Letter"];
            else                       fallbackOrder = ["A0","A1","A2","A3","Tabloid","A4","Letter"];

            foreach (var fb in fallbackOrder)
            {
                hit = sizes.FirstOrDefault(ps =>
                    ps.Name.IndexOf(fb, StringComparison.OrdinalIgnoreCase) >= 0);
                if (hit != null)
                {
                    Trace.WriteLine(
                        $"[RevitMCP][PRINT] Paper '{label}' ({Math.Round(widthMm)}×{Math.Round(heightMm)} mm) " +
                        $"not found in '{pm.PrinterName}'. Using nearest standard '{hit.Name}'. " +
                        $"Available: {string.Join(", ", sizes.Take(15).Select(s => s.Name))}");
                    return hit;
                }
            }

            // 5. Absolute fallback — first entry in the printer's list.
            Trace.WriteLine(
                $"[RevitMCP][PRINT] No standard paper size matched for '{label}'. " +
                $"Using first available: '{sizes[0].Name}'. " +
                $"All sizes: {string.Join(", ", sizes.Take(15).Select(s => s.Name))}");
            return sizes[0];
        }

        /// <summary>
        /// Reads the sheet's paper dimensions (mm) from SHEET_WIDTH / SHEET_HEIGHT
        /// built-in parameters, or from the title block family instance if those are absent.
        /// Falls back to A3 landscape (420×297 mm) on any error.
        /// </summary>
        private static (double WidthMm, double HeightMm) GetSheetDimensionsMm(Document doc, ViewSheet sheet)
        {
            try
            {
                var wParam = sheet.get_Parameter(BuiltInParameter.SHEET_WIDTH);
                var hParam = sheet.get_Parameter(BuiltInParameter.SHEET_HEIGHT);

                // Fallback: try the title block family instance on this sheet
                if (wParam == null || hParam == null)
                {
                    var tb = new FilteredElementCollector(doc, sheet.Id)
                        .OfCategory(BuiltInCategory.OST_TitleBlocks)
                        .OfClass(typeof(FamilyInstance))
                        .Cast<FamilyInstance>()
                        .FirstOrDefault();

                    if (tb != null)
                    {
                        wParam = tb.get_Parameter(BuiltInParameter.SHEET_WIDTH);
                        hParam = tb.get_Parameter(BuiltInParameter.SHEET_HEIGHT);
                    }
                }

                if (wParam == null || hParam == null) return (420, 297);

                var wMm = UnitUtils.ConvertFromInternalUnits(wParam.AsDouble(), UnitTypeId.Millimeters);
                var hMm = UnitUtils.ConvertFromInternalUnits(hParam.AsDouble(), UnitTypeId.Millimeters);

                return wMm >= 10 && hMm >= 10 ? (wMm, hMm) : (420, 297);
            }
            catch
            {
                return (420, 297); // A3 landscape fallback
            }
        }

        /// <summary>
        /// Returns the closest standard paper size label ("A3", "A4", etc.)
        /// or a custom dimension string if no standard size matches within 30 mm.
        /// Comparison is orientation-independent (uses max/min sides).
        /// </summary>
        private static string AutoDetectPaperLabel(double widthMm, double heightMm)
        {
            var longSide  = Math.Max(widthMm, heightMm);
            var shortSide = Math.Min(widthMm, heightMm);
            double bestDiff  = double.MaxValue;
            string bestLabel = $"Custom ({Math.Round(widthMm)}×{Math.Round(heightMm)} mm)";

            foreach (var (s, l, label) in _stdSizes)
            {
                var diff = Math.Abs(longSide - l) + Math.Abs(shortSide - s);
                if (diff < bestDiff)
                {
                    bestDiff  = diff;
                    bestLabel = diff < 30.0
                        ? label
                        : $"Custom ({Math.Round(widthMm)}×{Math.Round(heightMm)} mm)";
                }
            }

            return bestLabel;
        }

        private static ColorDepthType ParseColorDepth(string? mode) =>
            mode?.Trim().ToUpperInvariant() switch
            {
                "BLACKANDWHITE" or "BW" or "BLACKWHITE" or "BLACK" or "BLACKLINE" => ColorDepthType.BlackLine,
                "GRAYSCALE"     or "GREYSCALE" or "GRAY" or "GREY"                => ColorDepthType.GrayScale,
                _                                                                   => ColorDepthType.Color
            };

        private static RasterQualityType ParseRasterQuality(string? q) =>
            q?.Trim().ToUpperInvariant() switch
            {
                "LOW"          => RasterQualityType.Low,
                "MEDIUM"       => RasterQualityType.Medium,
                "PRESENTATION" => RasterQualityType.Presentation,
                _              => RasterQualityType.High
            };

        private static string SanitizeFileName(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            return string.Concat(name.Select(c => invalid.Contains(c) ? '_' : c));
        }
    }
}
