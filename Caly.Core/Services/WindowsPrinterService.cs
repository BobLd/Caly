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
using System.Management;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Caly.Core.Models;
using Caly.Core.Services.Interfaces;

namespace Caly.Core.Services
{
    public sealed class WindowsPrinterService : IPrinterService
    {
        public WindowsPrinterService()
        {
            if (!OperatingSystem.IsWindows())
            {
                throw new Exception();
            }
        }

        [SupportedOSPlatform("windows")]
        public Task<IReadOnlyList<CalyPrinter>> GetAllPrinters()
        {
            if (!OperatingSystem.IsWindows())
            {
                throw new Exception();
            }

            return Task.Run<IReadOnlyList<CalyPrinter>>(() =>
            {
                List<CalyPrinter> printers = new List<CalyPrinter>();
                using (var printerQuery = new ManagementObjectSearcher("SELECT * from Win32_Printer"))
                {
                    foreach (var printer in printerQuery.Get())
                    {
                        if (printer is not ManagementObject mo)
                        {
                            continue;
                        }

                        var name = mo.GetPropertyValue("Name");
                        var status = mo.GetPropertyValue("Status");
                        var isDefault = mo.GetPropertyValue("Default");
                        var isNetworkPrinter = mo.GetPropertyValue("Network");

                        printers.Add(new CalyPrinter()
                        {
                            IsDefault = false,
                            IsNetwork = false,
                            Name = name.ToString(),
                            Path = mo.Path.Path,
                            Status = status.ToString()
                        });

                        System.Diagnostics.Debug.WriteLine("{0} (Status: {1}, Default: {2}, Network: {3}",
                            name, status, isDefault, isNetworkPrinter);
                    }
                }

                return printers;
            });
        }
    }
}
