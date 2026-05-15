using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RevitServerBrowser
{
    public class RevitServerClient : IDisposable
    {
        private readonly string _baseUrl;
        private readonly WebClient _webClient;

        public RevitServerClient(string host, int apiYear)
        {
            if (string.IsNullOrEmpty(host))
                throw new ArgumentException("Хост не может быть пустым.", "host");
            if (apiYear < 2012)
                throw new ArgumentException("Год версии должен быть 2012 или новее.", "apiYear");

            _baseUrl = string.Format("http://{0}/RevitServerAdminRESTService{1}/AdminRESTService.svc", host, apiYear);
            _webClient = new WebClient { Encoding = Encoding.UTF8 };
        }

        private Dictionary<string, string> BuildHeaders()
        {
            var headers = new Dictionary<string, string>();
            headers["User-Name"] = GetUser();
            headers["User-Machine-Name"] = Environment.MachineName;
            headers["Operation-GUID"] = Guid.NewGuid().ToString();
            return headers;
        }

        private static string GetUser()
        {
            var domain = Environment.GetEnvironmentVariable("USERDOMAIN") ?? "";
            var username = Environment.GetEnvironmentVariable("USERNAME") ?? "";
            return string.Format("{0}\\{1}", domain, username);
        }

        public List<RevitServerItem> GetContents(string path)
        {
            Logger.Debug($"[CLIENT] Запрос: path='{path}'");

            if (!path.StartsWith("|"))
                throw new ArgumentException("Путь должен начинать с '|'.", nameof(path));

            var url = $"{_baseUrl}/{path}/contents";
            Logger.Debug($"[CLIENT] URL: {url}");
            if (string.IsNullOrEmpty(path) || !path.StartsWith("|"))
                throw new ArgumentException("Путь должен начинать с '|'.", "path");

            foreach (var header in BuildHeaders())
                _webClient.Headers[header.Key] = header.Value;

            var json = _webClient.DownloadString(url);
            return ParseJson(json, path);
        }

        private static List<RevitServerItem> ParseJson(string json, string parentPath)
        {
            var items = new List<RevitServerItem>();
            items.AddRange(ExtractItems(json, "Folders", parentPath, "Folder"));
            items.AddRange(ExtractItems(json, "Models", parentPath, "Model"));
            return items;
        }

        private static List<RevitServerItem> ExtractItems(string json, string arrayName, string parentPath, string itemType)
        {
            var result = new List<RevitServerItem>();
            var searchArray = string.Format("\"{0}\":[", arrayName);
            var arrayStart = json.IndexOf(searchArray, StringComparison.OrdinalIgnoreCase);
            if (arrayStart < 0) return result;

            var bracketStart = json.IndexOf('[', arrayStart);
            if (bracketStart < 0) return result;

            var depth = 0;
            var bracketEnd = -1;
            for (var i = bracketStart; i < json.Length; i++)
            {
                if (json[i] == '[') depth++;
                if (json[i] == ']')
                {
                    depth--;
                    if (depth == 0) { bracketEnd = i; break; }
                }
            }
            if (bracketEnd < 0) return result;

            var content = json.Substring(bracketStart + 1, bracketEnd - bracketStart - 1);
            var prop = "\"Name\"";
            var idx = 0;

            while ((idx = content.IndexOf(prop, idx, StringComparison.OrdinalIgnoreCase)) >= 0)
            {
                var vs = content.IndexOf('"', idx + prop.Length + 1);
                if (vs < 0) break;
                var ve = content.IndexOf('"', vs + 1);
                if (ve < 0) break;

                var name = content.Substring(vs + 1, ve - vs - 1);
                if (!string.IsNullOrEmpty(name))
                {
                    var childPath = parentPath == "|" ? string.Format("|{0}", name) : string.Format("{0}|{1}", parentPath, name);
                    result.Add(new RevitServerItem(name, itemType, childPath));
                }
                idx = ve + 1;
            }
            return result;
        }

        public void Dispose()
        {
            if (_webClient != null)
                _webClient.Dispose();
        }
    }
}
