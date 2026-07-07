# RevitServerBrowser

Базовый инструмент для работы с Revit Server внутри Revit-плагинов на `.NET Framework 4.8`.

Проект можно использовать как готовый браузер моделей и как основу для своих инструментов: пакетного экспорта, проверок, аудита структуры, массовой обработки.

## Что делает

- автоматически определяет версию Revit из `ExternalCommandData`;
- читает список серверов из `RSN.INI`;
- показывает WinForms-дерево структуры Revit Server;
- лениво загружает папки и модели по мере раскрытия;
- позволяет выбрать одну или несколько моделей;
- возвращает пути в формате `RSN://...` и в "сыром" виде `|Folder|...`.

## Две реализации доступа к Revit Server

| | REST API | Native WCF API |
|---|---|---|
| Способ | HTTP-запросы к публичному эндпоинту | Прямой вызов internal WCF-прокси Revit |
| Зависимости | `WebClient` | Internal сборки Revit (`RS.Enterprise.*`) |
| Скорость | Зависит от сети | Быстрее для больших структур |
| Авторизация | Требует прав на `RevitServerAdmin` | Использует текущую сессию Revit |
| Реализация | `RevitServerClient` | `RevitServerNativeClient` |
| Форма | `RevitServerBrowserForm` | `RevitServerBrowserNativeForm` |

**Рекомендация:** начинайте с REST. Если он недоступен или медленный — используйте Native.

## Как работает

- Версия Revit определяется автоматически, для REST формируется URL с годом.
- Серверы читаются из `C:\ProgramData\Autodesk\Revit Server <YEAR>\Config\RSN.INI`.
- Дерево подгружается лениво, при раскрытии узлов.
- Выбранные модели доступны через свойства `SelectedModelPaths` (RSN://) и `SelectedModelPathsRaw` (внутренние пути).

## Форматы путей

- Внутренний: `|ProjectA|AR|Building.rvt`
- Для Revit API: `RSN://rs-main/ProjectA/AR/Building.rvt`

## Публичные точки расширения

- `RevitServerConfigReader.ReadServers(revitYear)` – получить список серверов.
- `RevitServerBrowserForm` / `RevitServerBrowserNativeForm` – готовые формы.
- `RevitServerClient` / `RevitServerNativeClient` – низкоуровневые клиенты.
- `RevitServerItem` – модель папки/модели.

## Быстрое подключение в плагин

Обе формы автоматически определяют версию Revit и читают `RSN.INI`, достаточно передать `commandData`.

```csharp
// REST-форма
using (var form = new RevitServerBrowser.RevitServerBrowserForm(commandData))
{
    form.ConfirmButton.Click += (s, e) =>
    {
        foreach (var path in form.SelectedModelPaths)
            TaskDialog.Show("Модель", path);
    };
    form.ShowDialog();
}

// Native-форма – аналогично
using (var form = new RevitServerBrowser.RevitServerBrowserNativeForm(commandData))
{
    // ...
}
```

Если нужна только клиентская часть без UI:

```csharp
using (var client = new RevitServerClient(host, apiYear))
{
    var items = client.GetContents("|");
}
```

## Пример реальной интеграции (BatchIfcExporter)

В проекте [BatchIfcExporter](https://github.com/i-savelev/BatchIfcExporter) браузер используется для выбора моделей перед экспортом IFC. После подтверждения выбора получаются `SelectedModelPaths`, которые передаются в пайплайн открытия и экспорта.

## Рекомендуемый паттерн

Разделяйте ответственность:
- `RevitServerBrowser` отвечает за выбор моделей.
- Ваш код – за бизнес-логику (открытие, экспорт, проверки).

## Доступные команды Revit

- `RS` – запускает REST-форму.
- `RSNative` – запускает Native-форму.

Обе зарегистрированы во вкладке `ISTools` через `PluginsManager`.

## Технические детали

**Стек:** .NET Framework 4.8, Revit API, WinForms, WebClient, рефлексия.

**REST endpoint:** `http://<host>/RevitServerAdminRESTService<year>/AdminRESTService.svc/<path>/contents`

**Заголовки:** `User-Name`, `User-Machine-Name`, `Operation-GUID`.

**Native WCF:** использует сборки `RS.Enterprise.Common.ClientServer.*` и рефлексию для вызова `ListSubFoldersAndModels`.

**Модель данных:** `RevitServerItem` с полями `Name`, `ItemType`, `Path`, методами `IsFolder`/`IsModel`.

**Таймаут REST:** настраивается через `client.Timeout`.

## Структура проекта

- `Command.cs` / `CommandNative.cs` – команды для Revit.
- `RevitServerBrowserForm.cs` / `RevitServerBrowserNativeForm.cs` – формы.
- `RevitServerClient.cs` / `RevitServerNativeClient.cs` – клиенты.
- `RevitServerConfigReader.cs` – чтение RSN.INI.
- `RevitServerItem.cs` – модель.
- `TimeoutWebClient.cs` – WebClient с таймаутом.

## Сборка

Собирается как `RevitServerBrowser.dll`. Требует `RevitLogger.dll` рядом. Зависит от `RevitAPI.dll`, `RevitAPIUI.dll`.

## Ограничения

- Работает только внутри процесса Revit.
- Список серверов зависит от локального `RSN.INI`.
- Не открывает модели, только возвращает пути.
- Native-реализация зависит от internal сборок Revit и может измениться в новых версиях.

## Когда использовать

Используйте, если нужен готовый слой выбора моделей с Revit Server без написания:
- чтения `RSN.INI`;
- REST/Native клиентов;
- дерева папок и конвертации путей.

Прикладную логику стройте отдельно.

## Выбор между REST и Native

| Сценарий | Рекомендация |
|----------|--------------|
| Стандартная работа | REST |
| Проблемы с доступом к REST API | Native |
| Максимальная производительность | Native |
| Стабильность важнее скорости | REST |
| Отладка проблем | REST (проще логировать) |
| Проблемы с авторизацией | Native (использует сессию Revit) |