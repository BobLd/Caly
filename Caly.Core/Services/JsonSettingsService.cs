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
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Caly.Core.Models;
using Caly.Core.Services.Interfaces;
using Caly.Core.Views;
using static Caly.Core.Models.CalySettings;

namespace Caly.Core.Services
{
    [JsonSerializable(typeof(CalySettings), GenerationMode = JsonSourceGenerationMode.Metadata)]
    internal partial class SourceGenerationContext : JsonSerializerContext
    {
    }
    
    internal sealed class JsonSettingsService : ISettingsService
    {
        private const string _settingsFile = "caly_settings";
        
        private readonly Visual _target;

        private CalySettings? _current;

        public JsonSettingsService(Visual target)
        {
            _target = target;
            if (_target is Window w)
            {
                w.Opened += _window_Opened;
                w.Closing += _window_Closing;
            }
        }

        private void _window_Closing(object? sender, WindowClosingEventArgs e)
        {
            if (_target is Window w)
            {
                w.Closing -= _window_Closing;

                if (_current is not null)
                {
                    _current.Width = (int)w.Width;
                    _current.Height = (int)w.Height;
                }
            }

            Save();
        }

        private void _window_Opened(object? sender, EventArgs e)
        {
            if (_target is Window w)
            {
                w.Opened -= _window_Opened;
            }

            if (sender is MainWindow mw)
            {
                if (_current is not null)
                {
                    mw.Width = _current.Width;
                    mw.Height = _current.Height;
                }
            }
            else
            {
                throw new InvalidOperationException($"Expecting '{typeof(MainWindow)}' but got '{sender?.GetType()}'.");
            }
        }

        public void SetProperty(CalySettingsProperty property, object value)
        {
            try
            {
                if (_current is null)
                {
                    return;
                }

                switch (property)
                {
                    case CalySettingsProperty.PaneSize:
                        _current.PaneSize = (int)(double)value;
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteExceptionToFile(ex);
            }
        }

        public CalySettings GetSettings()
        {
            if (_current is null)
            {
                Load();
            }

            return _current!;
        }

        public async ValueTask<CalySettings> GetSettingsAsync()
        {
            if (_current is null)
            {
                await LoadAsync();
            }

            return _current!;
        }

        private void HandleCorruptedFile()
        {
            if (File.Exists(_settingsFile))
            {
                File.Delete(_settingsFile);
            }

            SetDefaultSettings();
        }

        private static void ValidateSetting(CalySettings? settings)
        {
            if (settings is null)
            {
                return;
            }

            if (settings.PaneSize <= 0)
            {
                settings.PaneSize = CalySettings.Default.PaneSize;
            }

            if (settings.Width <= 0)
            {
                settings.Width = CalySettings.Default.Width;
            }

            if (settings.Height <= 0)
            {
                settings.Height = CalySettings.Default.Height;
            }
        }

        private void SetDefaultSettings()
        {
            _current ??= CalySettings.Default;
        }

        public void Load()
        {
            try
            {
                if (!File.Exists(_settingsFile))
                {
                    SetDefaultSettings();

                    using (FileStream createStream = File.Create(_settingsFile))
                    {
                        JsonSerializer.Serialize(createStream, _current, SourceGenerationContext.Default.CalySettings);
                    }

                    return;
                }

                using (FileStream createStream = File.OpenRead(_settingsFile))
                {
                    _current = JsonSerializer.Deserialize(createStream, SourceGenerationContext.Default.CalySettings);
                    ValidateSetting(_current);
                }
            }
            catch (JsonException jsonEx)
            {
                HandleCorruptedFile();
                Debug.WriteExceptionToFile(jsonEx);
            }
            catch (Exception ex)
            {
                Debug.WriteExceptionToFile(ex);
            }
        }

        public async Task LoadAsync()
        {
            Debug.ThrowOnUiThread();

            try
            {
                if (!File.Exists(_settingsFile))
                {
                    SetDefaultSettings();

                    await using (FileStream createStream = File.Create(_settingsFile))
                    {
                        await JsonSerializer.SerializeAsync(createStream, _current, SourceGenerationContext.Default.CalySettings);
                    }
                    return;
                }

                await using (FileStream createStream = File.OpenRead(_settingsFile))
                {
                    _current = await JsonSerializer.DeserializeAsync(createStream, SourceGenerationContext.Default.CalySettings);
                    ValidateSetting(_current);
                }
            }
            catch (JsonException jsonEx)
            {
                HandleCorruptedFile();
                Debug.WriteExceptionToFile(jsonEx);
            }
            catch (Exception ex)
            {
                Debug.WriteExceptionToFile(ex);
            }
        }

        public void Save()
        {
            if (_current is not null)
            {
                try
                {
                    using (FileStream createStream = File.Create(_settingsFile))
                    {
                        JsonSerializer.Serialize(createStream, _current, SourceGenerationContext.Default.CalySettings);
                    }
                }
                catch (JsonException jsonEx)
                {
                    HandleCorruptedFile();
                    Debug.WriteExceptionToFile(jsonEx);
                }
                catch (Exception ex)
                {
                    Debug.WriteExceptionToFile(ex);
                }
            }
        }

        public async Task SaveAsync()
        {
            Debug.ThrowOnUiThread();

            if (_current is not null)
            {
                try
                {
                    await using (FileStream createStream = File.Create(_settingsFile))
                    {
                        await JsonSerializer.SerializeAsync(createStream, _current, SourceGenerationContext.Default.CalySettings);
                    }
                }
                catch (JsonException jsonEx)
                {
                    HandleCorruptedFile();
                    Debug.WriteExceptionToFile(jsonEx);
                }
                catch (Exception ex)
                {
                    Debug.WriteExceptionToFile(ex);
                }
            }
        }
    }
}
