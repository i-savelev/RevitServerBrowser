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

## Как это работает

### 1. Автоматическое определение версии Revit

Версия берётся из:

```csharp
commandData.Application.Application.VersionNumber
```

Дальше эта версия используется для:

- чтения правильного `RSN.INI`;
- формирования URL к `RevitServerAdminRESTService{year}`.

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
192.168.10.20
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

- `RevitServerConfigReader.ReadServers(int revitYear)` - получить список серверов;
- `RevitServerBrowserForm` - готовая форма выбора моделей;
- `RevitServerClient` - низкоуровневый клиент к REST API Revit Server;
- `RevitServerItem` - элемент структуры сервера: папка или модель.

## Быстрое подключение в свой плагин

### Вариант 1. Использовать готовую форму

Самый удобный сценарий - просто показать форму и получить выбранные модели.

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
            var apiYear = int.Parse(commandData.Application.Application.VersionNumber);
            var servers = RevitServerBrowser.RevitServerConfigReader.ReadServers(apiYear);

            using (var form = new RevitServerBrowser.RevitServerBrowserForm(servers, apiYear))
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

### Вариант 2. Использовать только REST-клиент

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
            var apiYear = int.Parse(commandData.Application.Application.VersionNumber);
            var servers = RevitServerBrowser.RevitServerConfigReader.ReadServers(apiYear);
            var form = new RevitServerBrowser.RevitServerBrowserForm(servers, apiYear);

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

## Технические детали

### Стек

- `.NET Framework 4.8`
- `Autodesk Revit API`
- `System.Windows.Forms`
- `WebClient` для вызова REST API Revit Server

### REST endpoint

Клиент формирует URL в виде:

```text
http://<host>/RevitServerAdminRESTService<year>/AdminRESTService.svc/<path>/contents
```

Пример:

```text
http://rs-main/RevitServerAdminRESTService2026/AdminRESTService.svc/|/contents
```

### Заголовки запроса

Для обращения к Revit Server используются служебные заголовки:

- `User-Name`
- `User-Machine-Name`
- `Operation-GUID`

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

## Структура проекта

- `RevitServerBrowser/Command.cs` - пример готовой Revit-команды, запускающей браузер
- `RevitServerBrowser/RevitServerBrowserForm.cs` - основная WinForms-форма
- `RevitServerBrowser/RevitServerClient.cs` - REST-клиент Revit Server
- `RevitServerBrowser/RevitServerConfigReader.cs` - чтение `RSN.INI`
- `RevitServerBrowser/RevitServerItem.cs` - модель папки/файла
- `RevitServerBrowser/TimeoutWebClient.cs` - `WebClient` с настраиваемым таймаутом

## Сборка

Проект собирается как библиотека:

```text
RevitServerBrowser.dll
```

Основные зависимости:

- `RevitAPI.dll`
- `RevitAPIUI.dll`

В репозитории уже лежат локальные копии `Revit API` DLL, поэтому проект можно открыть и собрать в Visual Studio без дополнительной ручной раскладки ссылок, если структура репозитория не менялась.

## Ограничения

- библиотека работает внутри процесса Revit;
- список серверов берётся из локального `RSN.INI`, поэтому он должен быть настроен на машине пользователя;
- доступность моделей зависит от сетевой доступности Revit Server и прав пользователя;
- инструмент не открывает и не обрабатывает модели сам по себе, он только возвращает выбранные пути.

## Когда использовать этот проект

Используйте `RevitServerBrowser`, если вам нужен базовый, расширяемый слой выбора моделей с Revit Server без необходимости каждый раз заново писать:

- чтение `RSN.INI`;
- выбор сервера по версии Revit;
- REST-клиент к структуре Revit Server;
- дерево папок и моделей;
- конвертацию внутренних путей в `RSN://`.

Если нужна прикладная логика поверх этого выбора, её лучше строить отдельным проектом, как это сделано в `BatchIfcExporter`.
