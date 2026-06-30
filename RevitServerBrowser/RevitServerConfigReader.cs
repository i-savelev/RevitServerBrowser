using RevitServerBrowser;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using RevitLogger;

namespace RevitServerBrowser
{
    /// <summary>
    /// Читает конфигурацию Revit Server из файла RSN.INI.
    /// Поддерживает формат: просто список IP/хостов (по одному на строку).
    /// </summary>
    public static class RevitServerConfigReader
    {
        public static Dictionary<string, string> ReadServers(int revitYear)
        {
            Logger.Info($"[RSN] Чтение серверов для Revit {revitYear}");
            var servers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            var iniPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "Autodesk",
                $"Revit Server {revitYear}",
                "Config",
                "RSN.INI"
            );

            if (!File.Exists(iniPath))
            {
                Logger.Warning($"[RSN] ❌ Файл не найден: {iniPath}");
                return servers;
            }

            Logger.Info($"[RSN] Путь: {iniPath}");

            try
            {
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                // Читаем все строки
                var lines = File.ReadAllLines(iniPath, Encoding.GetEncoding(1251));
                Logger.Info($"[RSN] Строк в файле: {lines.Length}");

                foreach (var rawLine in lines)
                {
                    var line = rawLine.Trim();

                    // Пропускаем пустые строки и комментарии
                    if (string.IsNullOrEmpty(line) || line.StartsWith(";") || line.StartsWith("#"))
                        continue;

                    // Берём адрес до первого пробела/табуляции, сохраняя порт в формате host:port.
                    var host = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries)[0];

                    if (!string.IsNullOrEmpty(host))
                    {
                        // Уникальный ключ + значение (для ComboBox удобно, когда имя = IP)
                        if (!servers.ContainsKey(host))
                        {
                            servers[host] = host;
                            Logger.Info($"[RSN] ✅ Добавлен сервер: {host}");
                        }
                    }
                }

                Logger.Info($"[RSN] Готово. Найдено серверов: {servers.Count}");
            }
            catch (Exception ex)
            {
                Logger.Error($"[RSN] ❌ Исключение при чтении: {ex.Message}");
            }
            foreach (var kvp in servers)
            {
                Logger.Debug($"[CMD] Сервер: '{kvp.Key}' → '{kvp.Value}'");
            }
            return servers;
        }
    }
}
