using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using RevitLogger;


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
                var form = new RevitServerBrowserForm(commandData);
                Logger.Info("[CMD] Создаю форму");
                form.ConfirmButton.Click += (s, e) =>
                {
                    Logger.Info($"[FORM] Клик по Подтвердить. Выбрано моделей: {form.SelectedModelPaths.Count}");
                    foreach (var model in form.SelectedModelPaths)
                    {
                        Logger.Debug($"[FORM] Модель: {model}");
                        DebugWindow.AddRow(model);
                    }
                    DebugWindow.Show();
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
