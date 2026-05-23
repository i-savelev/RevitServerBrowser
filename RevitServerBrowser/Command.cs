using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitServerBrowser;
using System;


namespace RevitServerBrowser
{
    [Autodesk.Revit.Attributes.TransactionAttribute(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public class RS : IExternalCommand
    {
        //---PluginsManager---//
        public static string IS_TAB_NAME => "ISTools";
        public static string IS_NAME => "Ревит сервер";
        public static string IS_IMAGE => "RevitServerBrowser.Resources.RS.png";
        public static string IS_DESCRIPTION => "Автор: https://github.com/i-savelev\r\nБраузер ревит сервера";
        //---PluginsManager---//
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                Logger.Clear();
                Logger.Info("=== Запуск команды RevitServerBrowser ===");
                string revitVersion = commandData.Application.Application.VersionNumber;
                if (!int.TryParse(revitVersion, out int apiYear))
                    apiYear = 2026; // fallback
                var servers = RevitServerConfigReader.ReadServers(apiYear);
                Logger.Info($"[CMD] Версия Revit: {revitVersion}");
                Logger.Info($"[CMD] Получено серверов из конфига: {servers.Count}");
                var form = new RevitServerBrowserForm(servers, apiYear);
                foreach (var kvp in servers)
                {
                    Logger.Debug($"[CMD] Сервер: '{kvp.Key}' → '{kvp.Value}'");
                }

                Logger.Info("[CMD] Создаю форму");
                form.ConfirmButton.Click += (s, e) =>
                {
                    Logger.Info($"[FORM] Клик по Подтвердить. Выбрано моделей: {form.SelectedModelPaths.Count}");
                    foreach (var model in form.SelectedModelPaths)
                    {
                        Logger.Debug($"[FORM] Модель: {model}");
                        IsDebugWindow.AddRow(model);
                    }
                    IsDebugWindow.Show();
                };
                form.ShowDialog();
                Logger.Info("[CMD] Форма закрыта, завершаю команду");
                return Result.Succeeded;
            }

            catch (Exception ex)
            {
                message = ex.Message;
                Logger.Critical($"[CMD] Необработанное исключение: {ex}");
                return Result.Failed;
            }
        }
    }
}
