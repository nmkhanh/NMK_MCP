using System;
using System.Runtime.InteropServices;

namespace RevitMcpAddin.Services
{
    /// <summary>
    /// Creates custom paper forms in the Windows print spooler so they appear in
    /// Revit's <c>PrintManager.PaperSizes</c> list after the printer is re-initialised
    /// with <c>pm.SelectNewPrintDriver()</c>.
    /// Uses P/Invoke against <c>winspool.drv</c> — no additional NuGet packages required.
    /// </summary>
    internal static class PaperFormManager
    {
        // ── Win32 structures ─────────────────────────────────────────────

        [StructLayout(LayoutKind.Sequential)]
        private struct SIZEL
        {
            public int cx;   // width  in 1/1000 mm (thousandths of a millimetre)
            public int cy;   // height in 1/1000 mm
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECTL
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct FORM_INFO_1
        {
            public uint Flags;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string pName;
            public SIZEL  Size;
            public RECTL  ImageableArea;
        }

        // ── P/Invoke declarations ────────────────────────────────────────

        [DllImport("winspool.drv", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool OpenPrinter(string? pPrinterName, out IntPtr phPrinter, IntPtr pDefault);

        [DllImport("winspool.drv", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool AddForm(IntPtr hPrinter, uint Level, ref FORM_INFO_1 pForm);

        [DllImport("winspool.drv", SetLastError = true)]
        private static extern bool ClosePrinter(IntPtr hPrinter);

        // ── Public API ───────────────────────────────────────────────────

        /// <summary>
        /// Adds a named paper form to the local Windows print spooler.
        /// If the form already exists (Win32 error 183), the call is silently ignored.
        /// </summary>
        /// <param name="formName">
        /// Unique form name (max ~64 chars). Must not be empty.
        /// Use a deterministic name, e.g. <c>"RMCP_420x297"</c>.
        /// </param>
        /// <param name="widthMm">Paper width in millimetres (must be &gt; 0).</param>
        /// <param name="heightMm">Paper height in millimetres (must be &gt; 0).</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown for Win32 errors other than ERROR_ALREADY_EXISTS (183).
        /// Access-denied (error 5) typically means Revit needs to run as Administrator.
        /// </exception>
        public static void CreatePaperFormMM(string formName, double widthMm, double heightMm)
        {
            if (string.IsNullOrWhiteSpace(formName))
                throw new ArgumentException("Form name must not be empty.", nameof(formName));
            if (widthMm <= 0 || heightMm <= 0)
                throw new ArgumentException($"Invalid paper size: {widthMm}×{heightMm} mm.");

            var form = new FORM_INFO_1
            {
                Flags = 0,
                pName = formName,
                Size  = new SIZEL
                {
                    cx = (int)(widthMm  * 1000.0),
                    cy = (int)(heightMm * 1000.0)
                },
                ImageableArea = new RECTL
                {
                    left   = 0,
                    top    = 0,
                    right  = (int)(widthMm  * 1000.0),
                    bottom = (int)(heightMm * 1000.0)
                }
            };

            // Pass null to open the local print server (allows AddForm without locking one printer)
            if (!OpenPrinter(null, out IntPtr hPrinter, IntPtr.Zero))
            {
                int err = Marshal.GetLastWin32Error();
                throw new InvalidOperationException(
                    $"OpenPrinter failed (Win32 error {err}). Cannot register custom paper form.");
            }

            try
            {
                if (!AddForm(hPrinter, 1, ref form))
                {
                    int err = Marshal.GetLastWin32Error();
                    string msg = err switch
                    {
                        5   => "Access denied (Win32 error 5). Run Revit as Administrator to create custom paper forms.",
                        6   => "Invalid printer handle (Win32 error 6).",
                        183 => "Form already exists",
                        _   => $"AddForm failed (Win32 error {err})."
                    };
                    throw new InvalidOperationException(msg);
                }
            }
            finally
            {
                ClosePrinter(hPrinter);
            }
        }
    }
}
