using Autodesk.Revit.ApplicationServices;
using RevitLogger;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;

namespace RevitServerBrowser
{
    /// <summary>
    /// Получает содержимое Revit Server через внутренний WCF API Revit.
    /// </summary>
    public class RevitServerNativeClient : IDisposable
    {
        private readonly string _host;
        private readonly Type _modelServiceType;
        private readonly MethodInfo _listMethod;
        private readonly object _modelService;
        private readonly object _sessionToken;

        /// <summary>
        /// Инициализирует native-клиент Revit Server.
        /// </summary>
        /// <param name="host">Имя или IP хоста Revit Server.</param>
        public RevitServerNativeClient(string host)
        {
            if (string.IsNullOrWhiteSpace(host))
                throw new ArgumentException("Хост не может быть пустым.", nameof(host));

            _host = host;

            Logger.Info($"[NativeClient] Инициализация клиента | Host={_host}");

            var revitFolder = Path.GetDirectoryName(typeof(Application).Assembly.Location);
            Logger.Debug($"[NativeClient] Папка Revit: {revitFolder}");

            var proxyAssemblyPath = Path.Combine(revitFolder, "RS.Enterprise.Common.ClientServer.Proxy.dll");
            var contractAssemblyPath = Path.Combine(revitFolder, "RS.Enterprise.Common.ClientServer.ServiceContract.Model.dll");
            var dataContractAssemblyPath = Path.Combine(revitFolder, "RS.Enterprise.Common.ClientServer.DataContract.dll");

            Logger.Info($"[NativeClient] Загрузка сборки Proxy | Path={proxyAssemblyPath} | Exists={File.Exists(proxyAssemblyPath)}");
            Logger.Info($"[NativeClient] Загрузка сборки Contract | Path={contractAssemblyPath} | Exists={File.Exists(contractAssemblyPath)}");
            Logger.Info($"[NativeClient] Загрузка сборки DataContract | Path={dataContractAssemblyPath} | Exists={File.Exists(dataContractAssemblyPath)}");

            var proxyAssembly = Assembly.LoadFrom(proxyAssemblyPath);
            var contractAssembly = Assembly.LoadFrom(contractAssemblyPath);
            var dataContractAssembly = Assembly.LoadFrom(dataContractAssemblyPath);

            Logger.Info($"[NativeClient] Сборка Proxy загружена | FullName={proxyAssembly.FullName}");
            Logger.Info($"[NativeClient] Сборка Contract загружена | FullName={contractAssembly.FullName}");
            Logger.Info($"[NativeClient] Сборка DataContract загружена | FullName={dataContractAssembly.FullName}");

            _modelServiceType = contractAssembly.GetType(
                "Autodesk.RevitServer.Enterprise.Common.ClientServer.ServiceContract.Model.IModelService");
            var sessionTokenType = dataContractAssembly.GetType(
                "Autodesk.RevitServer.Enterprise.Common.ClientServer.DataContract.SessionToken.ServiceSessionToken");
            var proxyProviderType = proxyAssembly.GetType(
                "Autodesk.RevitServer.Enterprise.Common.ClientServer.Proxy.ProxyProvider");

            Logger.Info($"[NativeClient] Тип IModelService найден = {_modelServiceType != null}");
            Logger.Info($"[NativeClient] Тип ServiceSessionToken найден = {sessionTokenType != null}");
            Logger.Info($"[NativeClient] Тип ProxyProvider найден = {proxyProviderType != null}");

            if (_modelServiceType == null || sessionTokenType == null || proxyProviderType == null)
                throw new InvalidOperationException("Не удалось найти один или несколько типов internal Revit Server API.");

            Logger.Debug($"[NativeClient] IModelService = {_modelServiceType.FullName}");
            Logger.Debug($"[NativeClient] ServiceSessionToken = {sessionTokenType.FullName}");
            Logger.Debug($"[NativeClient] ProxyProvider = {proxyProviderType.FullName}");

            var userName = $"{Environment.UserDomainName}\\{Environment.UserName}";
            Logger.Info($"[NativeClient] Создание session token | User={userName} | Machine={Environment.MachineName}");

            _sessionToken = Activator.CreateInstance(sessionTokenType, new object[]
            {
                userName,
                string.Empty,
                Environment.MachineName,
                Guid.NewGuid().ToString()
            });

            Logger.Info($"[NativeClient] Session token создан | Type={_sessionToken?.GetType().FullName}");

            var instanceProperty = proxyProviderType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
            Logger.Info($"[NativeClient] Свойство ProxyProvider.Instance найдено = {instanceProperty != null}");

            var proxyProvider = instanceProperty?.GetValue(null, null);
            Logger.Info($"[NativeClient] Экземпляр ProxyProvider получен = {proxyProvider != null}");

            var getProxyMethod = proxyProviderType
                .GetMethod("GetBufferedProxy", new[] { typeof(string) })
                .MakeGenericMethod(_modelServiceType);

            Logger.Info($"[NativeClient] Метод GetBufferedProxy найден = {getProxyMethod != null}");
            Logger.Debug($"[NativeClient] Generic GetBufferedProxy = {getProxyMethod}");

            LogHostResolution(_host);

            object clientProxy;
            try
            {
                Logger.Info($"[NativeClient] Создание ClientProxy | Host={_host}");
                clientProxy = getProxyMethod.Invoke(proxyProvider, new object[] { _host });
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, $"[NativeClient] Ошибка создания ClientProxy | Host={_host}");
                throw;
            }

            Logger.Info($"[NativeClient] ClientProxy создан | Type={clientProxy?.GetType().FullName}");

            var proxyProperty = clientProxy?.GetType().GetProperty("Proxy");
            Logger.Info($"[NativeClient] Свойство clientProxy.Proxy найдено = {proxyProperty != null}");

            _modelService = proxyProperty?.GetValue(clientProxy, null);
            _listMethod = _modelServiceType.GetMethod("ListSubFoldersAndModels");

            Logger.Info($"[NativeClient] WCF proxy получен = {_modelService != null}");
            Logger.Info($"[NativeClient] Метод ListSubFoldersAndModels найден = {_listMethod != null}");
            Logger.Debug($"[NativeClient] WCF proxy type = {_modelService?.GetType().FullName}");
            Logger.Debug($"[NativeClient] ListSubFoldersAndModels = {_listMethod}");

            if (_modelService == null || _listMethod == null)
                throw new InvalidOperationException("Не удалось получить WCF-прокси Revit Server.");

            Logger.Info($"[NativeClient] Клиент инициализирован | Host={_host}");
        }

        /// <summary>
        /// Возвращает дочерние папки и модели для внутреннего пути браузера.
        /// </summary>
        /// <param name="browserPath">Путь вида |Folder|SubFolder или | для корня.</param>
        public List<RevitServerItem> GetContents(string browserPath)
        {
            Logger.Info($"[NativeClient] Запрос содержимого | BrowserPath={browserPath}");

            if (string.IsNullOrWhiteSpace(browserPath) || !browserPath.StartsWith("|"))
                throw new ArgumentException("Путь должен начинаться с '|'.", nameof(browserPath));

            var relativePath = ToNativeRelativePath(browserPath);
            Logger.Debug($"[NativeClient] Преобразование пути | BrowserPath={browserPath} | RelativePath={relativePath}");

            var folders = new ArrayList();
            var models = new ArrayList();
            var args = new object[] { _sessionToken, relativePath, folders, models };

            Logger.Debug($"[NativeClient] Вызов ListSubFoldersAndModels | Host={_host} | RelativePath={relativePath}");
            var success = (bool)_listMethod.Invoke(_modelService, args);
            folders = (ArrayList)args[2];
            models = (ArrayList)args[3];

            Logger.Info($"[NativeClient] Ответ сервиса | Success={success} | Folders={folders.Count} | Models={models.Count}");
            LogRawItemsPreview(folders, "Folder");
            LogRawItemsPreview(models, "Model");

            if (!success)
                return new List<RevitServerItem>();

            var items = new List<RevitServerItem>();
            items.AddRange(ConvertItems(folders, browserPath, "Folder"));
            items.AddRange(ConvertItems(models, browserPath, "Model"));
            return items;
        }

        /// <summary>
        /// Освобождает ресурсы клиента.
        /// </summary>
        public void Dispose()
        {
            if (_modelService is IDisposable disposable)
                disposable.Dispose();
        }

        /// <summary>
        /// Преобразует внутренний путь браузера в формат native WCF API.
        /// </summary>
        private static string ToNativeRelativePath(string browserPath)
        {
            return browserPath == "|"
                ? string.Empty
                : browserPath.TrimStart('|').Replace('|', '/');
        }

        /// <summary>
        /// Преобразует массив native-элементов в элементы браузера.
        /// </summary>
        private static List<RevitServerItem> ConvertItems(ArrayList source, string parentBrowserPath, string itemType)
        {
            var items = new List<RevitServerItem>();

            foreach (var sourceItem in source)
            {
                var rawValue = sourceItem?.ToString() ?? string.Empty;
                var name = ExtractDisplayName(rawValue);

                if (string.IsNullOrWhiteSpace(name))
                    continue;

                var childPath = parentBrowserPath == "|"
                    ? $"|{name}"
                    : $"{parentBrowserPath}|{name}";

                Logger.Debug($"[NativeClient] ConvertItem | Type={itemType} | Raw={rawValue} | Name={name} | ChildPath={childPath}");
                items.Add(new RevitServerItem(name, itemType, childPath));
            }

            return items;
        }

        /// <summary>
        /// Возвращает отображаемое имя без технического GUID-хвоста.
        /// </summary>
        private static string ExtractDisplayName(string rawValue)
        {
            var separatorIndex = rawValue.IndexOf('|');
            return separatorIndex >= 0
                ? rawValue.Substring(0, separatorIndex)
                : rawValue;
        }

        /// <summary>
        /// Пишет в лог несколько первых элементов сырого ответа сервиса.
        /// </summary>
        private static void LogRawItemsPreview(ArrayList source, string itemType)
        {
            if (source == null || source.Count == 0)
            {
                Logger.Debug($"[NativeClient] Raw preview | Type={itemType} | Count=0");
                return;
            }

            for (var index = 0; index < Math.Min(5, source.Count); index++)
            {
                Logger.Debug($"[NativeClient] Raw preview | Type={itemType} | Index={index} | Value={source[index]}");
            }
        }

        /// <summary>
        /// Пишет в лог результат DNS-резолва имени сервера.
        /// </summary>
        private static void LogHostResolution(string host)
        {
            try
            {
                var dnsHost = GetHostWithoutPort(host);
                var addresses = Dns.GetHostAddresses(dnsHost);
                Logger.Info($"[NativeClient] DNS resolve | Host={host} | DnsHost={dnsHost} | AddressCount={addresses.Length}");

                for (var index = 0; index < addresses.Length; index++)
                {
                    Logger.Debug($"[NativeClient] DNS resolve | Host={host} | DnsHost={dnsHost} | Index={index} | Address={addresses[index]}");
                }
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, $"[NativeClient] Ошибка DNS resolve | Host={host}");
            }
        }

        /// <summary>
        /// Возвращает имя хоста без порта для DNS-диагностики.
        /// </summary>
        private static string GetHostWithoutPort(string host)
        {
            if (string.IsNullOrWhiteSpace(host))
                return host;

            if (Uri.TryCreate($"net.tcp://{host}", UriKind.Absolute, out var uri))
                return uri.Host;

            var portSeparatorIndex = host.LastIndexOf(':');
            return portSeparatorIndex > 0
                ? host.Substring(0, portSeparatorIndex)
                : host;
        }
    }
}
