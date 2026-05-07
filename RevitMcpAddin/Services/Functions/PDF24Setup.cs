using System;
using Microsoft.Win32;

namespace RevitMcpAddin.Services
{
    /// <summary>
    /// Configures PDF24 via the Windows registry for fully silent auto-save so Revit
    /// can print PDFs without any interactive dialogs appearing.
    /// <para>
    /// All settings are written to <c>HKCU\Software\PDF24\Services\PDF</c> which PDF24
    /// reads each time it processes a print job.  The changes take effect immediately —
    /// no PDF24 service restart is required.
    /// </para>
    /// </summary>
    internal static class PDF24Setup
    {
        private const string KeyPath = @"Software\PDF24\Services\PDF";

        /// <summary>
        /// Configures PDF24 for silent auto-save.  Call this before submitting print jobs.
        /// </summary>
        /// <param name="outputDir">
        /// Directory where PDF24 will auto-save the PDF file.
        /// The directory must exist before submitting the print job.
        /// </param>
        /// <param name="fileName">
        /// File stem (without extension) for the saved PDF.
        /// Pass <see langword="null"/> to let PDF24 use the print-job title as the filename.
        /// </param>
        public static void SetAutoSave(string outputDir, string? fileName = null)
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(KeyPath);

                key.SetValue("AutoSaveDir",            outputDir,              RegistryValueKind.String);
                key.SetValue("AutoSaveFilename",       fileName ?? "$fileName", RegistryValueKind.String);
                key.SetValue("ShowSaveDialog",         0,                      RegistryValueKind.DWord);
                key.SetValue("AutoSaveEnabled",        1,                      RegistryValueKind.DWord);
                key.SetValue("LoadInCreatorIfOpen",    0,                      RegistryValueKind.DWord);
                key.SetValue("AutoSaveProfile",        "default/high",         RegistryValueKind.String);
                key.SetValue("AutoSaveOpenDir",        0,                      RegistryValueKind.DWord);
                key.SetValue("AutoSaveOverwriteFile",  1,                      RegistryValueKind.DWord);
                key.SetValue("AutoSaveShowProgress",   0,                      RegistryValueKind.DWord);
                key.SetValue("AutoSaveUseFileChooser", 0,                      RegistryValueKind.DWord);
                key.SetValue("AutoSaveUseFileCmd",     0,                      RegistryValueKind.DWord);
                key.SetValue("Handler",                "autoSave",             RegistryValueKind.String);
            }
            catch (Exception)
            {
                // Registry write failure is non-fatal.  Printing may still work via
                // pm.PrintToFileName, but PDF24 might show its save dialog.
            }
        }
    }
}
