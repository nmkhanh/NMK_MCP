using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
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
#if R24
    private const string RevitBuildVersion = "2024";
#else
    private const string RevitBuildVersion = "2026";
#endif

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

        // ── 3. Resolve target sheet by element ID ─────────────────
        // SheetId is REQUIRED.
        if (string.IsNullOrWhiteSpace(request.SheetId))
          return new
          {
            success = false,
            message = "'sheetId' is required. Provide the Revit element ID of the sheet to print. " +
                      "Use get_elements with category 'Sheets' to discover sheet IDs."
          };

        var allSheets = new FilteredElementCollector(doc)
                  .OfClass(typeof(ViewSheet))
                  .Cast<ViewSheet>()
                  .Where(s => !s.IsTemplate)
                  .ToDictionary(s => s.Id.ToString(), StringComparer.OrdinalIgnoreCase);

        if (!allSheets.TryGetValue(request.SheetId.Trim(), out var targetSheet))
          return new
          {
            success = false,
            requestedId = request.SheetId,
            message = "The specified sheet ID was not found in the active document. " +
                          "Use get_elements with category 'Sheets' to get valid element IDs."
          };

        // ── 3a. Pre-create custom Windows paper form for the sheet size ──────────
        //        Must run BEFORE pm.SelectNewPrintDriver so the driver loads them.
        {
          var (sheetW, sheetH) = GetSheetDimensionsMm(doc, targetSheet);
          var w = (int)Math.Round(sheetW);
          var h = (int)Math.Round(sheetH);
          var formName = $"RMCP_{w}x{h}";
          try
          {
            PaperFormManager.CreatePaperFormMM(formName, w, h);
            Trace.WriteLine($"[RevitMCP][PRINT] Registered paper form: {formName} ({w}×{h} mm)");
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

        // ── 5. Print the single sheet ─────────────────────────────
        var (wMm, hMm) = GetSheetDimensionsMm(doc, targetSheet);
        var safeName = SanitizeFileName(targetSheet.SheetNumber);
        var outputFile = Path.Combine(outputFolder, safeName + ".pdf");
        if (File.Exists(outputFile)) File.Delete(outputFile);
        PDF24Setup.SetAutoSave(outputFolder, safeName);

        var viewSet = new ViewSet();
        viewSet.Insert(targetSheet);

        // Basic PrintManager properties — set before the transaction group
        pm.PrintRange = PrintRange.Select;
        pm.PrintToFile = true;
        pm.CombinedFile = true;   // must be true when PrintRange = Select
        pm.PrintToFileName = outputFile;

        var tempName = $"RMCP_{safeName}";

        try
        {
          using (var g = new TransactionGroup(doc, $"RevitMCP: Print - {targetSheet.SheetNumber}"))
          {
            g.Start();

            // ── Configure and submit print ────────────────────
            using (var tx = new Transaction(doc, "Configure Print"))
            {
              tx.Start();

              pm.SelectNewPrintDriver(printerName);
              pm.Apply();
              doc.Regenerate();

              InSessionPrintSetting printSetting = pm.PrintSetup.InSession;
              pm.Apply();
              doc.Regenerate();

              var pp = printSetting.PrintParameters;

              pp.ColorDepth = ParseColorDepth(request.ColorMode);
              pp.RasterQuality = ParseRasterQuality(request.RasterQuality);
              pp.ZoomType = ZoomType.Zoom;
              pp.Zoom = 100;

              pp.PaperPlacement = PaperPlacementType.Margins;
              pp.MarginType = MarginType.NoMargin;

              pp.HideScopeBoxes = true;
              pp.HideUnreferencedViewTags = true;
              pp.HideReforWorkPlanes = true;
              pp.HideCropBoundaries = true;

              var paper = FindBestPaperSize(pm, wMm, hMm);

              if (paper != null)
                pp.PaperSize = paper;

              pp.PageOrientation =
                  wMm > hMm
                  ? PageOrientationType.Landscape
                  : PageOrientationType.Portrait;

              pm.PrintSetup.SaveAs(tempName);
              pm.Apply();
              doc.Regenerate();

              //pm.PrintSetup.CurrentPrintSetting = printSetting;
              pm.PrintSetup.CurrentPrintSetting = doc.GetPrintSettingIds().Select(x => doc.GetElement(x) as Autodesk.Revit.DB.PrintSetting).First(x => x.Name == tempName);
              pm.Apply();
              doc.Regenerate();

              //doc.Print(viewSet, true);
              pm.SubmitPrint(targetSheet);
              tx.Commit();
            }

            // ── Delete the temporary named print setting ──────
            using (var tx = new Transaction(doc, "Cleanup Print Setting"))
            {
              tx.Start();
              var toDelete = doc.GetPrintSettingIds()
                        .Select(id => doc.GetElement(id) as PrintSetting)
                        .Where(x => x?.Name == tempName)
                        .ToList();
              if (toDelete.Count > 0)
                doc.Delete(toDelete[0]!.Id);
              tx.Commit();
            }

            g.Assimilate();
          }
        }
        catch (Exception ex)
        {
          File.WriteAllText($@"C:\ProgramData\Autodesk\Revit\Addins\{RevitBuildVersion}\RevitMcpAddin\{ex.Message}.txt", $"{ex.Message}");
        }

        Trace.WriteLine($"[RevitMCP][PRINT] Sheet {targetSheet.SheetNumber} → {outputFile}");

        return (object)new
        {
          success = true,
          sheet = new
          {
            number = targetSheet.SheetNumber,
            name = targetSheet.Name,
            widthMm = Math.Round(wMm, 1),
            heightMm = Math.Round(hMm, 1),
            paperSize = AutoDetectPaperLabel(wMm, hMm),
            file = outputFile
          },
          printer = printerName,
          outputFolder,
          outputFile,
          message = $"Printed sheet '{targetSheet.SheetNumber}' to '{printerName}'."
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

    // ── Win32 DeviceCapabilities ─────────────────────────────────────

    [DllImport("winspool.drv", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int DeviceCapabilities(
        string device, string? port, short capability, IntPtr output, IntPtr devMode);

    /// <summary>
    /// Queries the printer driver for the name of the paper whose physical dimensions
    /// best match the given sheet size, using Win32 <c>DeviceCapabilities</c>.
    /// DC_PAPERSIZE returns dimensions in 1/10 mm units (POINT = cx, cy).
    /// Both short and long sides are compared orientation-independently.
    /// Returns <see langword="null"/> if the query fails or no match within 15 mm total error.
    /// </summary>
    private static string? FindPaperNameByDimension(string printerName, double sheetWidthMm, double sheetHeightMm)
    {
      const short DC_PAPERSIZE = 3;
      const short DC_PAPERNAMES = 16;
      const int NameCharLen = 64; // fixed 64-WCHAR field per Windows docs

      int count = DeviceCapabilities(printerName, null, DC_PAPERSIZE, IntPtr.Zero, IntPtr.Zero);
      if (count <= 0) return null;

      var sizeBuf = Marshal.AllocHGlobal(count * 8);              // 2 × int32 per POINT
      var nameBuf = Marshal.AllocHGlobal(count * NameCharLen * 2); // 64 WCHARs per entry
      try
      {
        if (DeviceCapabilities(printerName, null, DC_PAPERSIZE, sizeBuf, IntPtr.Zero) != count) return null;
        if (DeviceCapabilities(printerName, null, DC_PAPERNAMES, nameBuf, IntPtr.Zero) != count) return null;

        double shortSheet = Math.Min(sheetWidthMm, sheetHeightMm);
        double longSheet = Math.Max(sheetWidthMm, sheetHeightMm);

        int bestIdx = -1;
        double bestDiff = double.MaxValue;
        for (int i = 0; i < count; i++)
        {
          int cx = Marshal.ReadInt32(sizeBuf, i * 8);
          int cy = Marshal.ReadInt32(sizeBuf, i * 8 + 4);
          double shortPaper = Math.Min(cx, cy) / 10.0;
          double longPaper = Math.Max(cx, cy) / 10.0;
          double diff = Math.Abs(shortPaper - shortSheet) + Math.Abs(longPaper - longSheet);
          if (diff < bestDiff) { bestDiff = diff; bestIdx = i; }
        }

        if (bestIdx < 0 || bestDiff > 15.0) return null;

        var namePtr = IntPtr.Add(nameBuf, bestIdx * NameCharLen * 2);
        var name = Marshal.PtrToStringUni(namePtr, NameCharLen)?.TrimEnd('\0').Trim();
        Trace.WriteLine(
            $"[RevitMCP][PRINT] DevCap matched '{name}' (diff={bestDiff:F1} mm) " +
            $"for sheet {sheetWidthMm:F0}×{sheetHeightMm:F0} mm");
        return name;
      }
      finally
      {
        Marshal.FreeHGlobal(sizeBuf);
        Marshal.FreeHGlobal(nameBuf);
      }
    }

    /// <summary>
    /// Finds the best matching <see cref="PaperSize"/> from the printer's available list.
    /// Match priority:
    /// 1. Win32 DeviceCapabilities — dimension match against the printer's own size table
    /// 2. Exact name match ("A3" == "A3")
    /// 3. Custom RMCP paper form
    /// 4. Partial name match
    /// 5. Nearest standard size by long-side dimension
    /// 6. First available (absolute last resort)
    /// </summary>
    private static PaperSize FindBestPaperSize(PrintManager pm, double widthMm, double heightMm)
    {
      var label = AutoDetectPaperLabel(widthMm, heightMm);
      var sizes = pm.PaperSizes.Cast<PaperSize>().ToList();

      if (sizes.Count == 0)
        throw new InvalidOperationException(
            $"The PDF printer '{pm.PrinterName}' reported no paper sizes. " +
            "Please check its configuration.");

      // 1. Query actual physical dimensions from the driver — most reliable approach.
      //    DeviceCapabilities returns the printer's own size table in 1/10 mm units,
      //    so this works regardless of paper name language or format.
      try
      {
        var paperName = FindPaperNameByDimension(pm.PrinterName, widthMm, heightMm);
        if (paperName != null)
        {
          var hit = sizes.FirstOrDefault(ps =>
              string.Equals(ps.Name.Trim(), paperName, StringComparison.OrdinalIgnoreCase));
          hit ??= sizes.FirstOrDefault(ps =>
              ps.Name.Trim().StartsWith(paperName, StringComparison.OrdinalIgnoreCase));
          if (hit != null) return hit;
        }
      }
      catch (Exception ex)
      {
        Trace.WriteLine($"[RevitMCP][PRINT] DeviceCapabilities lookup failed: {ex.Message}");
      }

      // 2. Exact name match (case-insensitive)
      var fallback = sizes.FirstOrDefault(ps =>
          string.Equals(ps.Name.Trim(), label, StringComparison.OrdinalIgnoreCase));
      if (fallback != null) return fallback;

      // 3. Custom RMCP paper form — actual SHEET_WIDTH × SHEET_HEIGHT
      var wInt = (int)Math.Round(widthMm);
      var hInt = (int)Math.Round(heightMm);
      fallback = sizes.FirstOrDefault(ps =>
          string.Equals(ps.Name.Trim(), $"RMCP_{wInt}x{hInt}", StringComparison.OrdinalIgnoreCase));
      if (fallback != null) return fallback;

      // 4. Partial name match (e.g. "ISO A3", "A3 Transverse")
      fallback = sizes.FirstOrDefault(ps =>
          ps.Name.IndexOf(label, StringComparison.OrdinalIgnoreCase) >= 0);
      if (fallback != null) return fallback;

      // 5. Nearest standard size by long-side dimension
      var longSide = Math.Max(widthMm, heightMm);
      string[] fallbackOrder =
          longSide <= 297 ? ["A4", "A3", "Letter", "A2", "A1", "A0", "Tabloid"] :
          longSide <= 420 ? ["A3", "A2", "A4", "A1", "A0", "Tabloid", "Letter"] :
          longSide <= 594 ? ["A2", "A1", "A3", "A0", "Tabloid", "A4", "Letter"] :
          longSide <= 841 ? ["A1", "A0", "A2", "Tabloid", "A3", "A4", "Letter"] :
                            ["A0", "A1", "A2", "A3", "Tabloid", "A4", "Letter"];
      foreach (var fb in fallbackOrder)
      {
        fallback = sizes.FirstOrDefault(ps =>
            ps.Name.IndexOf(fb, StringComparison.OrdinalIgnoreCase) >= 0);
        if (fallback != null)
        {
          Trace.WriteLine(
              $"[RevitMCP][PRINT] Paper '{label}' ({Math.Round(widthMm)}×{Math.Round(heightMm)} mm) " +
              $"not found via DevCap/name. Using nearest '{fallback.Name}'.");
          return fallback;
        }
      }

      // 6. Absolute fallback
      Trace.WriteLine(
          $"[RevitMCP][PRINT] No paper size matched for '{label}'. " +
          $"Using first available: '{sizes[0].Name}'.");
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
    /// Returns the paper size label matching the given SHEET_WIDTH × SHEET_HEIGHT dimensions (mm).
    /// Checks exact orientation first, then swapped, with 10 mm tolerance.
    /// Returns an RMCP custom form name if no standard size matches.
    /// </summary>
    private static string AutoDetectPaperLabel(double widthMm, double heightMm)
    {
      if (IsSize(widthMm, heightMm, 1189, 841) || IsSize(widthMm, heightMm, 841, 1189)) return "A0";
      if (IsSize(widthMm, heightMm, 841, 594) || IsSize(widthMm, heightMm, 594, 841)) return "A1";
      if (IsSize(widthMm, heightMm, 594, 420) || IsSize(widthMm, heightMm, 420, 594)) return "A2";
      if (IsSize(widthMm, heightMm, 420, 297) || IsSize(widthMm, heightMm, 297, 420)) return "A3";
      if (IsSize(widthMm, heightMm, 297, 210) || IsSize(widthMm, heightMm, 210, 297)) return "A4";
      if (IsSize(widthMm, heightMm, 210, 148) || IsSize(widthMm, heightMm, 148, 210)) return "A5";
      if (IsSize(widthMm, heightMm, 279, 216) || IsSize(widthMm, heightMm, 216, 279)) return "Letter";
      if (IsSize(widthMm, heightMm, 356, 216) || IsSize(widthMm, heightMm, 216, 356)) return "Legal";
      if (IsSize(widthMm, heightMm, 432, 279) || IsSize(widthMm, heightMm, 279, 432)) return "Tabloid";
      return $"RMCP_{(int)Math.Round(widthMm)}x{(int)Math.Round(heightMm)}";
    }

    private static bool IsSize(double w, double h, double tw, double th, double tol = 10.0)
        => Math.Abs(w - tw) <= tol && Math.Abs(h - th) <= tol;

    private static ColorDepthType ParseColorDepth(string? mode) =>
        mode?.Trim().ToUpperInvariant() switch
        {
          "BLACKANDWHITE" or "BW" or "BLACKWHITE" or "BLACK" or "BLACKLINE" => ColorDepthType.BlackLine,
          "GRAYSCALE" or "GREYSCALE" or "GRAY" or "GREY" => ColorDepthType.GrayScale,
          _ => ColorDepthType.Color
        };

    private static RasterQualityType ParseRasterQuality(string? q) =>
        q?.Trim().ToUpperInvariant() switch
        {
          "LOW" => RasterQualityType.Low,
          "MEDIUM" => RasterQualityType.Medium,
          "PRESENTATION" => RasterQualityType.Presentation,
          _ => RasterQualityType.High
        };

    private static string SanitizeFileName(string name)
    {
      var invalid = Path.GetInvalidFileNameChars();
      return string.Concat(name.Select(c => invalid.Contains(c) ? '_' : c));
    }
  }
}
