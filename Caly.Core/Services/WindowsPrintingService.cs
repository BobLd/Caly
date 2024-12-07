// Copyright (C) 2024 BobLd
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY - without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <https://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Caly.Core.Services.Interfaces;
using Vanara.InteropServices;
using Vanara.PInvoke;
using static SkiaSharp.HarfBuzz.SKShaper;
using static Vanara.PInvoke.WinSpool;

namespace Caly.Core.Services
{
    // https://github.com/dahall/Vanara/blob/master/UnitTests/PInvoke/Printing/PrintingTests.cs#L25
    public sealed record CalyPrinter(string Name, string DriverName, string Port)
    {
    }

    internal sealed class WindowsPrintingService : IPrintingService
    {
        public bool IsSupported => true;

        public void Print(string text)
        {
            if (!WinSpool.OpenPrinter(GetDefaultPrinterName(), out WinSpool.SafeHPRINTER hprnt))
            {
                throw new Exception();
            }

            using (hprnt)
            {
                var job = StartDocPrinter(hprnt, 1, new DOC_INFO_1
                {
                    pDatatype = "RAW",
                    pDocName = "My Document"
                });

                try
                {
                    if (!StartPagePrinter(hprnt))
                    {
                        throw new Exception();
                    }

                    try
                    {
                        //SafeByteArray q = new SafeByteArray()
                        using var s = new SafeCoTaskMemString("Testing this printer.", CharSet.Unicode);
                        if (!WritePrinter(hprnt, s, s.Size, out var written))
                        {
                            throw new Exception();
                        }

                        System.Diagnostics.Debug.Assert(written == s.Size);
                    }
                    finally
                    {
                        EndPagePrinter(hprnt);
                    }
                }
                finally
                {
                    EndDocPrinter(hprnt);
                }
 

                if (!WinSpool.AddJob(hprnt, out string? path, out uint id))
                {
                    throw new Exception();
                }

                try
                {
                    string doc = @"C:\Users\Bob\Documents\Pdf\Difficult.pdf";
                    var fs = File.ReadAllBytes(doc);
                    File.WriteAllBytes(path!, fs);
                    //System.IO.File.WriteAllText(path!, text);


                    JOB_INFO_2 ji2 = GetJob<JOB_INFO_2>(hprnt, id);
                    System.Diagnostics.Debug.Assert(ji2.JobId == id);

                    var jobInfo = new JOB_INFO_1
                    {
                        JobId = id,
                        Priority = JOB_PRIORITY.MAX_PRIORITY,
                        Status = ji2.Status,
                        pDatatype = ji2.pDatatype!,
                        pDocument = $"Caly Pdf: {doc}"
                    };

                    if (!SetJob(hprnt, id, jobInfo))
                    {
                        throw new Exception();
                    }

                    if (!ScheduleJob(hprnt, id))
                    {
                        throw new Exception();
                    }
                }
                finally
                {
                    SetJob(hprnt, id, JOB_CONTROL.JOB_CONTROL_DELETE);
                }
            }
        }

        public string GetDefaultPrinterName()
        {
            var sb = new StringBuilder();
            int buffer = sb.Capacity;
            if (WinSpool.GetDefaultPrinter(sb, ref buffer))
            {
                return sb.ToString();
            }

            // Try again with correct buffer size.
            if (WinSpool.GetDefaultPrinter(sb, ref buffer))
            {
                return sb.ToString();
            }

            return string.Empty;
        }

        public IEnumerable<CalyPrinter> EnumeratePrinters()
        {
            foreach (var printer in WinSpool.EnumPrinters<WinSpool.PRINTER_INFO_2>())
            {
                yield return new CalyPrinter(printer.pPrinterName, printer.pDriverName, printer.pPortName);
            }
        }
    }
}
