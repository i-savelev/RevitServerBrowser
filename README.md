# RevitServerBrowser

Базовый инструмент для работы с `Revit Server` внутри Revit-плагинов на `.NET Framework 4.8`.

Проект можно использовать как готовый браузер моделей на Revit Server и как основу для своих инструментов: пакетного экспорта, проверок моделей, публикации, аудита структуры сервера, выборки наборов моделей по папкам и любых других сценариев, где нужно дать пользователю удобный UI для выбора моделей с Revit Server.

## Что делает проект

- автоматически определяет версию Revit из `ExternalCommandData`;
- автоматически читает список серверов из `RSN.INI`;
- показывает WinForms-интерфейс с деревом структуры Revit Server;
- лениво загружает папки и модели по мере раскрытия дерева;
- позволяет выбрать одну или несколько моделей;
- возвращает выбранные модели в формате `RSN://...`, пригодном для дальнейшей работы через Revit API;
- дополнительно хранит "сырые" внутренние пути вида `|Folder|SubFolder|Model.rvt`.

Базовый результат работы инструмента: список путей выбранных моделей.

## Для чего подходит

`RevitServerBrowser` не привязан к одному сценарию. Это именно базовый слой, на котором удобно строить прикладные инструменты:

- экспорт моделей с Revit Server;
- открытие выбранных моделей в фоновом режиме;
- запуск пакетных проверок;
- сбор реестров моделей;
- массовая обработка выбранных файлов;
- построение собственных UI-обвязок поверх стандартного браузера.

## Две реализации доступа к Revit Server

В проекте реализовано **два способа** получения структуры Revit Server:

### 1. REST API (классический)

Использует публичный REST-эндпоинт Revit Server:

```
http://<host>/RevitServerAdminRESTService<year>/AdminRESTService.svc/<path>/contents
```

**Особенности:**
- работает через HTTP-запросы;
- требует указания года версии Revit в URL;
- использует `WebClient` с настраиваемым таймаутом;
- подходит для любых сред, где доступен REST API.

**Реализация:** `RevitServerClient`

**Форма:** `RevitServerBrowserForm`

### 2. Native WCF API (прямой вызов internal API Revit)

Использует внутренний WCF-клиент Revit, работающий через сборки:

- `RS.Enterprise.Common.ClientServer.Proxy.dll`
- `RS.Enterprise.Common.ClientServer.ServiceContract.Model.dll`
- `RS.Enterprise.Common.ClientServer.DataContract.dll`

**Особенности:**
- работает напрямую через internal WCF-прокси Revit;
- не требует указания года версии в URL;
- может быть быстрее при работе внутри процесса Revit;
- зависит от наличия internal сборок Revit;
- использует рефлексию для доступа к internal API.

**Реализация:** `RevitServerNativeClient`

**Форма:** `RevitServerBrowserNativeForm`

### Сравнение подходов

| Характеристика | REST API | Native WCF API |
|----------------|----------|----------------|
| Способ доступа | HTTP-запросы | WCF-прокси через рефлексию |
| Зависимости | Только `WebClient` | Internal сборки Revit |
| Скорость | Зависит от сети | Намного быстрее для серверов с большим количеством моделей |
| Авторизация | Требует наличия у пользователя доступа к RevitServerAdmin | Не требует дополнительных прав доступа |

**Рекомендация:** начинайте с REST-реализации, если она работает — используйте её. Native-версия предназначена для случаев, когда REST API недоступен или требуется более тесная интеграция с Revit.

## Как это работает

### 1. Автоматическое определение версии Revit

Версия берётся из:

```csharp
commandData.Application.Application.VersionNumber
```

Дальше эта версия используется для:

- чтения правильного `RSN.INI`;
- формирования URL к `RevitServerAdminRESTService{year}` (только для REST-клиента).

### 2. Автоматическое чтение серверов из `RSN.INI`

Список серверов читается из файла:

```text
C:\ProgramData\Autodesk\Revit Server <YEAR>\Config\RSN.INI
```

Например для Revit 2026:

```text
C:\ProgramData\Autodesk\Revit Server 2026\Config\RSN.INI
```

Поддерживается простой формат: один сервер на строку. Пустые строки и комментарии пропускаются.

Пример:

```ini
111.111.10.30
rs-main
# backup
rs-backup
```

### 3. Браузер структуры Revit Server

UI построен на `WinForms` и содержит:

- выпадающий список серверов;
- дерево структуры Revit Server;
- выбор моделей кликом;
- кнопку подтверждения выбора;
- кнопку сброса;
- строку статуса.

Дерево загружает содержимое не целиком, а по раскрытию узлов. Это удобно для больших серверных структур.

### 4. Возврат результатов

После выбора моделей форма возвращает:

- `SelectedModelPaths` - список путей в формате `RSN://server/path/model.rvt`;
- `SelectedModelPathsRaw` - список внутренних путей вида `|Folder|SubFolder|Model.rvt`.

## Форматы путей

### Внутренний путь браузера

```text
|ProjectA|AR|Building_01.rvt
```

Такой путь используется внутри дерева и при обращении к REST API Revit Server.

### Путь для Revit API

```text
RSN://rs-main/ProjectA/AR/Building_01.rvt
```

Именно этот формат удобно передавать дальше в свои инструменты для открытия модели, экспорта, анализа и т.д.

## Публичные точки расширения

Проект удобно использовать как библиотеку. Основные точки входа:

### REST-реализация

- `RevitServerConfigReader.ReadServers(int revitYear)` - получить список серверов;
- `RevitServerBrowserForm` - готовая форма выбора моделей через REST API;
- `RevitServerClient` - низкоуровневый клиент к REST API Revit Server.

### Native-реализация

- `RevitServerConfigReader.ReadServers(int revitYear)` - получить список серверов;
- `RevitServerBrowserNativeForm` - готовая форма выбора моделей через native WCF API;
- `RevitServerNativeClient` - клиент к native WCF API Revit Server (через рефлексию).

### Общие модели

- `RevitServerItem` - элемент структуры сервера: папка или модель.

## Быстрое подключение в свой плагин

### Вариант 1. Использовать REST-форму

Самый простой сценарий - показать форму и получить выбранные модели.

```csharp
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.Linq;
using System.Windows.Forms;

namespace MyTool
{
    [Transaction(TransactionMode.Manual)]
    public class MyCommand : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            // Форма сама определит версию Revit и прочитает RSN.INI
            using (var form = new RevitServerBrowser.RevitServerBrowserForm(commandData))
            {
                form.ConfirmButton.Click += (s, e) =>
                {
                    var selected = form.SelectedModelPaths;

                    if (!selected.Any())
                    {
                        MessageBox.Show(form, "Выберите хотя бы одну модель");
                        return;
                    }

                    foreach (var rsnPath in selected)
                    {
                        TaskDialog.Show("Выбранная модель", rsnPath);
                    }
                };

                form.ShowDialog();
            }

            return Result.Succeeded;
        }
    }
}
```

### Вариант 2. Использовать Native-форму

Альтернативная реализация через native WCF API:

```csharp
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.Linq;
using System.Windows.Forms;

namespace MyTool
{
    [Transaction(TransactionMode.Manual)]
    public class MyNativeCommand : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            // Native-форма использует внутренний WCF API Revit
            using (var form = new RevitServerBrowser.RevitServerBrowserNativeForm(commandData))
            {
                form.ConfirmButton.Click += (s, e) =>
                {
                    var selected = form.SelectedModelPaths;

                    if (!selected.Any())
                    {
                        MessageBox.Show(form, "Выберите хотя бы одну модель");
                        return;
                    }

                    foreach (var rsnPath in selected)
                    {
                        TaskDialog.Show("Выбранная модель (Native)", rsnPath);
                    }
                };

                form.ShowDialog();
            }

            return Result.Succeeded;
        }
    }
}
```

### Вариант 3. Использовать форму с обработкой выбора моделей

Пример с получением выбранных моделей для дальнейшей обработки:

```csharp
[Transaction(TransactionMode.Manual)]
public class LoadModelsCommand : IExternalCommand
{
    public Result Execute(
        ExternalCommandData commandData,
        ref string message,
        ElementSet elements)
    {
        var uiApp = commandData.Application;
        
        // Проверяем наличие активного документа
        if (uiApp.ActiveUIDocument == null || uiApp.ActiveUIDocument.Document == null)
        {
            MessageBox.Show("Для работы необходим открытый активный документ", 
                "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return Result.Cancelled;
        }

        // Создаём форму (REST или Native)
        using (var form = new RevitServerBrowser.RevitServerBrowserForm(commandData))
        {
            // Подписываемся на событие клика по кнопке "Подтвердить"
            form.ConfirmButton.Click += (s, e) =>
            {
                var selectedPaths = form.SelectedModelPaths;
                
                if (!selectedPaths.Any())
                {
                    MessageBox.Show(form, "Выберите хотя бы одну модель");
                    return;
                }

                // Обрабатываем выбранные модели
                foreach (var rsnPath in selectedPaths)
                {
                    // Ваша логика обработки модели
                    ProcessModel(uiApp, rsnPath);
                }
            };

            form.ShowDialog();
        }

        return Result.Succeeded;
    }

    private void ProcessModel(UIApplication uiApp, string rsnPath)
    {
        // Открытие модели, экспорт, проверка и т.д.
        var doc = uiApp.Application.OpenDocumentFile(rsnPath);
        // ... ваша бизнес-логика
    }
}
```

### Вариант 4. Использовать только REST-клиент

Если свой UI уже есть, можно использовать только клиент.

```csharp
using System;
using System.Collections.Generic;

namespace MyTool
{
    public class ServerReader
    {
        public List<RevitServerBrowser.RevitServerItem> ReadRoot(string host, int apiYear)
        {
            using (var client = new RevitServerBrowser.RevitServerClient(host, apiYear))
            {
                return client.GetContents("|");
            }
        }
    }
}
```

### Вариант 5. Использовать только Native-клиент

```csharp
using System;
using System.Collections.Generic;

namespace MyTool
{
    public class NativeServerReader
    {
        public List<RevitServerBrowser.RevitServerItem> ReadRoot(string host)
        {
            using (var client = new RevitServerBrowser.RevitServerNativeClient(host))
            {
                return client.GetContents("|");
            }
        }
    }
}
```

### Вариант 6. Полный пример с логированием

```csharp
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitLogger;
using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace MyTool
{
    [Transaction(TransactionMode.Manual)]
    public class FullExampleCommand : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            // Настройка логирования
            ConfigureLogging(commandData);
            
            try
            {
                Logger.Info("[FullExample] Старт команды");
                
                var uiApp = commandData.Application;
                
                // Проверяем наличие активного документа
                if (uiApp.ActiveUIDocument?.Document == null)
                {
                    var errMsg = "Для работы необходим открытый активный документ";
                    Logger.Error($"[FullExample] {errMsg}");
                    MessageBox.Show(errMsg, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return Result.Cancelled;
                }

                var hostDoc = uiApp.ActiveUIDocument.Document;
                Logger.Info($"[FullExample] Активный документ: {hostDoc.Title}");

                // Создаём форму (выберите нужную реализацию)
                using (var form = new RevitServerBrowser.RevitServerBrowserForm(commandData))
                {
                    form.ConfirmButton.Click += (s, e) =>
                    {
                        var selectedPaths = form.SelectedModelPaths;
                        Logger.Info($"[FullExample] Выбрано моделей: {selectedPaths.Count}");

                        if (!selectedPaths.Any())
                        {
                            MessageBox.Show(form, "Выберите хотя бы одну модель");
                            return;
                        }

                        foreach (var rsnPath in selectedPaths)
                        {
                            Logger.Debug($"[FullExample] Обработка: {rsnPath}");
                            // Ваша логика
                        }
                    };

                    form.ShowDialog();
                }

                Logger.Info("[FullExample] Команда завершена");
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "[FullExample] Ошибка выполнения");
                message = ex.Message;
                return Result.Failed;
            }
        }

        private void ConfigureLogging(ExternalCommandData commandData)
        {
            Logger.SetLogPath(
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Temp",
                    "MyTool",
                    "plugin.log"));

            Logger.SetLogLevel(Logger.LogLevel.Debug);
            Logger.Init(
                hostName: "Autodesk Revit",
                hostVersionNumber: commandData.Application.Application.VersionNumber,
                hostBuild: commandData.Application.Application.VersionBuild,
                hasActiveDocument: commandData.Application.ActiveUIDocument != null);
        }
    }
}
```

## Важное замечание

**Обе формы** (`RevitServerBrowserForm` и `RevitServerBrowserNativeForm`) **автоматически**:
- определяют версию Revit из `commandData`;
- читают список серверов из `RSN.INI`;
- настраивают логирование.

## Пример реальной интеграции: BatchIfcExporter

В проекте [`BatchIfcExporter`](https://github.com/i-savelev/BatchIfcExporter) этот инструмент используется как слой выбора моделей с Revit Server перед пакетным IFC-экспортом.

Сценарий там такой:

1. Команда получает `apiYear` из текущего Revit.
2. Читает список серверов через `RevitServerConfigReader`.
3. Показывает `RevitServerBrowserForm`.
4. Получает `SelectedModelPaths`.
5. Передаёт эти `RSN://` пути в собственный пайплайн открытия моделей и экспорта IFC.

Ниже упрощённый фрагмент по мотивам `ExportFromServerCommand`:

```csharp
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace BatchExportIfc
{
    [Transaction(TransactionMode.Manual)]
    public class ExportFromServerCommand : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            var form = new RevitServerBrowser.RevitServerBrowserForm(commandData);

            form.ConfirmButton.Click += (s, e) =>
            {
                var selectedPaths = form.SelectedModelPaths;

                if (!selectedPaths.Any())
                {
                    MessageBox.Show(form, "Выберите хотя бы одну модель для экспорта");
                    return;
                }

                string outputFolder = @"C:\IFC_Output";

                foreach (var rsnPath in selectedPaths)
                {
                    ExportModelToIfc(commandData.Application.Application, rsnPath, outputFolder);
                }
            };

            form.ShowDialog();
            return Result.Succeeded;
        }

        private void ExportModelToIfc(
            Autodesk.Revit.ApplicationServices.Application app,
            string rsnPath,
            string outputFolder)
        {
            var fileName = Path.GetFileName(rsnPath);

            // Дальше уже ваш прикладной пайплайн:
            // 1. открыть модель по RSN:// пути
            // 2. подготовить IFCExportOptions
            // 3. вызвать doc.Export(...)
        }
    }
}
```

### Что важно в этой интеграции

`RevitServerBrowser` ничего не знает про IFC, экспорт, настройки видов, JSON-конфиги и логику обработки документов. Он решает только одну задачу, но решает её полноценно:

- находит доступные Revit Server;
- показывает структуру;
- даёт пользователю выбрать модели;
- возвращает готовые пути.

А весь прикладной сценарий остаётся в вашем проекте. За счёт этого инструмент легко переиспользовать.

## Рекомендуемый паттерн расширения

Если вы строите свой инструмент поверх этого проекта, удобно разделять ответственность так:

- `RevitServerBrowser` отвечает за выбор моделей;
- ваш проект отвечает за дальнейшую бизнес-логику.

Например:

```csharp
var selectedModels = form.SelectedModelPaths;

foreach (var rsnPath in selectedModels)
{
    var doc = myDocumentOpener.Open(rsnPath);
    var report = myChecker.Run(doc);
    myExporter.Save(report);
}
```

Это позволяет:

- не смешивать UI выбора и прикладную обработку;
- переиспользовать браузер в нескольких командах;
- проще тестировать собственную бизнес-логику;
- не дублировать код работы с `RSN.INI` и REST API.

## Доступные команды Revit

В проекте реализованы две Revit-команды для запуска браузера:

### RS (REST-версия)

```csharp
public class RS : IExternalCommand
```

Запускает форму `RevitServerBrowserForm` с использованием REST API.

### RSNative (Native-версия)

```csharp
public class RSNative : IExternalCommand
```

Запускает форму `RevitServerBrowserNativeForm` с использованием native WCF API.

Обе команды зарегистрированы для `PluginsManager` и отображаются во вкладке `ISTools`.

## Технические детали

### Стек

- `.NET Framework 4.8`
- `Autodesk Revit API`
- `System.Windows.Forms`
- `WebClient` для вызова REST API Revit Server
- Рефлексия для доступа к internal WCF API Revit Server

### REST endpoint

Клиент формирует URL в виде:

```text
http://<host>/RevitServerAdminRESTService<year>/AdminRESTService.svc/<path>/contents
```

Пример:

```text
http://rs-main/RevitServerAdminRESTService2026/AdminRESTService.svc/|/contents
```

### Заголовки запроса (REST)

Для обращения к Revit Server используются служебные заголовки:

- `User-Name`
- `User-Machine-Name`
- `Operation-GUID`

### Native WCF API

Native-клиент использует internal сборки Revit:

- `RS.Enterprise.Common.ClientServer.Proxy.dll`
- `RS.Enterprise.Common.ClientServer.ServiceContract.Model.dll`
- `RS.Enterprise.Common.ClientServer.DataContract.dll`

Доступ к API осуществляется через рефлексию, что позволяет обойти ограничения публичного API.

### Модель данных

Элемент структуры сервера описывается типом `RevitServerItem`:

```csharp
public class RevitServerItem
{
    public string Name { get; }
    public string ItemType { get; }
    public string Path { get; }

    public bool IsFolder { get; }
    public bool IsModel { get; }
}
```

### Ленивая загрузка дерева

При старте форма не загружает всё дерево целиком. В корень добавляется placeholder, а реальная загрузка происходит в обработчике раскрытия узла. Это важно для больших серверов и длинных списков моделей.

### Настройка таймаута (REST)

Для REST-клиента можно настроить таймаут:

```csharp
var client = new RevitServerClient(host, apiYear);
client.Timeout = 600000; // 10 минут
```

## Структура проекта

- `RevitServerBrowser/Command.cs` - REST-команда для PluginsManager
- `RevitServerBrowser/CommandNative.cs` - Native-команда для PluginsManager
- `RevitServerBrowser/RevitServerBrowserForm.cs` - основная WinForms-форма (REST)
- `RevitServerBrowser/RevitServerBrowserNativeForm.cs` - Native WinForms-форма
- `RevitServerBrowser/RevitServerClient.cs` - REST-клиент Revit Server
- `RevitServerBrowser/RevitServerNativeClient.cs` - Native WCF-клиент Revit Server
- `RevitServerBrowser/RevitServerConfigReader.cs` - чтение `RSN.INI`
- `RevitServerBrowser/RevitServerItem.cs` - модель папки/файла
- `RevitServerBrowser/TimeoutWebClient.cs` - `WebClient` с настраиваемым таймаутом

## Сборка

Проект собирается как библиотека:

```text
RevitServerBrowser.dll
```

Для запуска вместе с библиотекой также требуется:

```text
RevitLogger.dll
```

Основные зависимости:

- `RevitAPI.dll`
- `RevitAPIUI.dll`
- `RevitLogger.dll`

В репозитории уже лежат локальные копии `Revit API` DLL, но `RevitLogger.dll` должен быть доступен по пути ссылки в проекте или лежать рядом с `RevitServerBrowser.dll` в выходной папке/папке плагина.

## Ограничения

- библиотека работает внутри процесса Revit;
- список серверов берётся из локального `RSN.INI`, поэтому он должен быть настроен на машине пользователя;
- доступность моделей зависит от сетевой доступности Revit Server и прав пользователя;
- инструмент не открывает и не обрабатывает модели сам по себе, он только возвращает выбранные пути;
- Native-реализация зависит от internal сборок Revit и может измениться в новых версиях;
- Native-реализация требует наличия файлов `RS.Enterprise.Common.ClientServer.*.dll` в папке Revit.

## Когда использовать этот проект

Используйте `RevitServerBrowser`, если вам нужен базовый, расширяемый слой выбора моделей с Revit Server без необходимости каждый раз заново писать:

- чтение `RSN.INI`;
- выбор сервера по версии Revit;
- REST-клиент к структуре Revit Server;
- Native WCF-клиент к структуре Revit Server;
- дерево папок и моделей;
- конвертацию внутренних путей в `RSN://`.

Если нужна прикладная логика поверх этого выбора, её лучше строить отдельным проектом, как это сделано в `BatchIfcExporter`.

## Выбор между REST и Native

| Сценарий | Рекомендация |
|----------|--------------|
| Стандартная работа с Revit Server | REST |
| Проблемы с доступом к REST API | Native |
| Требуется максимальная производительность | Native |
| Стабильность важнее скорости | REST |
| Работа в среде без доступа к internal API Revit | REST |
| Отладка проблем с сервером | REST (легче логировать HTTP) |
| Решение проблем с авторизацией | Native (использует текущую сессию Revit) |
```