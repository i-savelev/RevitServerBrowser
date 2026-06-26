using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitLogger;
using System;
using System.IO;

namespace RevitServerBrowser
{
    /// <summary>
    /// Команда запуска native-браузера Revit Server.
    /// </summary>
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public class RSNative : IExternalCommand
    {
        public static string IS_TAB_NAME => "ISTools";
        public static string IS_NAME => "Ревит сервер native";
        public static string IS_IMAGE => "RevitServerBrowser.Resources.RS.png";
        public static string IS_DESCRIPTION => "Автор: https://github.com/i-savelev\r\nБраузер ревит сервера через native WCF API";

        /// <summary>
        /// Точка входа команды Revit.
        /// </summary>
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            ConfigureLogging(commandData);

            try
            {
                Logger.Info("[RSNative] Старт команды");

                using (var form = new RevitServerBrowserNativeForm(commandData))
                {
                    Logger.Info("[RSNative] Создана native-форма");

                    form.ConfirmButton.Click += (s, e) =>
                    {
                        Logger.Info($"[RSNative] Подтверждение выбора | Count={form.SelectedModelPaths.Count}");

                        foreach (var modelPath in form.SelectedModelPaths)
                        {
                            Logger.Debug($"[RSNative] SelectedModel={modelPath}");
                            DebugWindow.AddRow(modelPath);
                        }

                        DebugWindow.Show();
                    };

                    form.ShowDialog();
                }

                Logger.Info("[RSNative] Команда завершена успешно");
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "[RSNative] Ошибка выполнения команды");
                message = ex.Message;
                return Result.Failed;
            }
        }

        /// <summary>
        /// Настраивает файловый лог и снимок окружения.
        /// </summary>
        private static void ConfigureLogging(ExternalCommandData commandData)
        {
            Logger.SetLogPath(
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Temp",
                    "RevitServerBrowser",
                    "native-command.log"));

            Logger.SetLogLevel(Logger.LogLevel.Debug);
            Logger.Init(
                hostName: "Autodesk Revit",
                hostVersionNumber: commandData.Application.Application.VersionNumber,
                hostBuild: commandData.Application.Application.VersionBuild,
                hasActiveDocument: commandData.Application.ActiveUIDocument != null);
        }
    }
}
