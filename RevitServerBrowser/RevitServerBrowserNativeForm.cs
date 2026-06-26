using Autodesk.Revit.UI;
using RevitLogger;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using ComboBox = System.Windows.Forms.ComboBox;

namespace RevitServerBrowser
{
    /// <summary>
    /// Браузер моделей Revit Server через native WCF-доступ.
    /// </summary>
    public class RevitServerBrowserNativeForm : Form
    {
        private RevitServerNativeClient _client;
        private readonly Dictionary<string, bool> _selectedModels = new Dictionary<string, bool>();
        private TreeView _tree;
        private Label _statusLabel;
        private Button _btnConfirm;
        private ComboBox _serverCombo;
        private bool _isInitializing = true;

        /// <summary>
        /// Выбранные пути к моделям в формате RSN://.
        /// </summary>
        public IReadOnlyList<string> SelectedModelPaths =>
            _selectedModels
                .Where(kv => kv.Value)
                .Select(kv => ToRsnPath(kv.Key))
                .Where(path => path != null)
                .ToList()
                .AsReadOnly();

        /// <summary>
        /// Выбранные внутренние пути вида |Folder|Model.rvt.
        /// </summary>
        public IReadOnlyList<string> SelectedModelPathsRaw =>
            _selectedModels.Where(kv => kv.Value).Select(kv => kv.Key).ToList().AsReadOnly();

        /// <summary>
        /// Прямой доступ к кнопке подтверждения.
        /// </summary>
        public Button ConfirmButton => _btnConfirm;

        /// <summary>
        /// Текущий выбранный сервер.
        /// </summary>
        public string SelectedServerHost => _serverCombo?.SelectedItem is KeyValuePair<string, string> kvp
            ? kvp.Value
            : null;

        /// <summary>
        /// Создаёт native-форму выбора моделей Revit Server.
        /// </summary>
        public RevitServerBrowserNativeForm(ExternalCommandData commandData, string defaultHost = null)
        {
            var uiApp = commandData.Application;
            var revitApp = uiApp?.Application;

            Logger.SetLogPath(
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Temp",
                    "i-savelev",
                    "RevitServerBrowserNative.log"));

            Logger.SetLogLevel(Logger.LogLevel.Debug);
            Logger.Init(
                hostName: revitApp?.VersionName,
                hostVersionNumber: revitApp?.VersionNumber,
                hostBuild: revitApp?.VersionBuild,
                hasActiveDocument: uiApp?.ActiveUIDocument != null);

            var revitVersion = revitApp?.VersionNumber ?? "unknown";
            if (!int.TryParse(revitVersion, out var apiYear))
                apiYear = 2026;

            var servers = RevitServerConfigReader.ReadServers(apiYear);
            Logger.Info($"[NativeForm] Версия Revit: {revitVersion}");
            Logger.Info($"[NativeForm] Получено серверов из конфига: {servers.Count}");

            SetupForm();
            SetupControls(servers, defaultHost);
            _isInitializing = false;
            LoadRootNode();
            _statusLabel.Text = $"Готово к подключению: {SelectedServerHost ?? "сервер не выбран"}";
        }

        /// <summary>
        /// Настраивает параметры окна.
        /// </summary>
        private void SetupForm()
        {
            Text = "Revit Server Browser Native";
            Size = new Size(600, 500);
            MinimumSize = new Size(450, 400);
            StartPosition = FormStartPosition.CenterParent;
            Font = new Font("Segoe UI", 9F);
            ShowIcon = false;
        }

        /// <summary>
        /// Создаёт и размещает контролы формы.
        /// </summary>
        private void SetupControls(Dictionary<string, string> servers, string defaultHost)
        {
            _tree = new TreeView
            {
                Dock = DockStyle.Fill,
                ShowLines = true,
                ShowPlusMinus = true,
                ShowRootLines = true,
                Indent = 20,
                HideSelection = false
            };

            var topPanel = new Panel { Dock = DockStyle.Top, Height = 35, BackColor = SystemColors.ControlLight };
            var serverLabel = new Label { Text = "Сервер:", Location = new Point(10, 8), AutoSize = true };
            _serverCombo = new ComboBox
            {
                Location = new Point(70, 5),
                Width = 300,
                DropDownStyle = servers.Any() ? ComboBoxStyle.DropDownList : ComboBoxStyle.DropDown,
                FormattingEnabled = true,
                DisplayMember = "Key",
                ValueMember = "Value"
            };

            var bottomPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 45,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                Padding = new Padding(5),
                BackColor = SystemColors.ControlLight
            };

            _btnConfirm = new Button { Text = "✅ Подтвердить", Width = 110, FlatStyle = FlatStyle.Flat };
            var resetButton = new Button { Text = "⟲ Сброс", Width = 80, FlatStyle = FlatStyle.Flat };
            var closeButton = new Button { Text = "✖ Закрыть", Width = 80, DialogResult = DialogResult.Cancel };

            _statusLabel = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 22,
                BackColor = Color.LightYellow,
                ForeColor = Color.DarkBlue,
                Padding = new Padding(5, 3, 0, 0),
                Text = "Готов"
            };

            topPanel.Controls.Add(serverLabel);
            topPanel.Controls.Add(_serverCombo);
            bottomPanel.Controls.Add(closeButton);
            bottomPanel.Controls.Add(resetButton);
            bottomPanel.Controls.Add(_btnConfirm);

            Controls.Add(_tree);
            Controls.Add(_statusLabel);
            Controls.Add(bottomPanel);
            Controls.Add(topPanel);

            _serverCombo.SelectedIndexChanged += (s, e) => OnServerChanged();
            resetButton.Click += (s, e) => ResetSelection();
            closeButton.Click += (s, e) => Close();
            _tree.BeforeExpand += Tree_BeforeExpand;
            _tree.NodeMouseClick += Tree_NodeMouseClick;

            if (servers.Any())
            {
                foreach (var server in servers)
                    _serverCombo.Items.Add(server);

                if (!string.IsNullOrEmpty(defaultHost))
                {
                    var matchedServer = servers.FirstOrDefault(kv => kv.Value.Equals(defaultHost, StringComparison.OrdinalIgnoreCase));
                    _serverCombo.SelectedItem = matchedServer.Key != null ? matchedServer : servers.First();
                }
                else
                {
                    _serverCombo.SelectedIndex = 0;
                }
            }
            else
            {
                _serverCombo.Text = defaultHost ?? "127.0.0.1";
            }
        }

        /// <summary>
        /// Переподключает форму к новому серверу.
        /// </summary>
        private void OnServerChanged()
        {
            if (_isInitializing)
                return;

            ConnectToSelectedServer();
            ReloadTree();
        }

        /// <summary>
        /// Создаёт native-клиент для текущего сервера.
        /// </summary>
        private void ConnectToSelectedServer()
        {
            var host = SelectedServerHost;
            if (string.IsNullOrWhiteSpace(host))
                return;

            Logger.Info($"[NativeForm] Подключение к серверу: {host}");

            try
            {
                if (_client != null)
                {
                    Logger.Debug($"[NativeForm] Освобождаю предыдущий клиент для сервера: {host}");
                    _client.Dispose();
                }

                _client = new RevitServerNativeClient(host);
                Text = $"Revit Server Browser Native [{host}]";
                _statusLabel.Text = $"Подключено: {host}";
                Logger.Info($"[NativeForm] Подключение успешно: {host}");
            }
            catch (Exception ex)
            {
                _client = null;
                _statusLabel.Text = $"Нет подключения: {host}";
                Text = $"Revit Server Browser Native [{host}]";
                Logger.Exception(ex, $"[NativeForm] Ошибка подключения к серверу: {host}");
            }
        }

        /// <summary>
        /// Полностью перезагружает дерево.
        /// </summary>
        private void ReloadTree()
        {
            if (_tree == null)
                return;

            _tree.Nodes.Clear();
            _selectedModels.Clear();
            LoadRootNode();
        }

        /// <summary>
        /// Добавляет корневой узел дерева.
        /// </summary>
        private void LoadRootNode()
        {
            var rootNode = new TreeNode("📁 Root") { Name = "|", Tag = "|" };
            rootNode.Nodes.Add(new TreeNode("…") { ForeColor = Color.Gray });
            _tree.Nodes.Add(rootNode);
        }

        /// <summary>
        /// Лениво загружает содержимое раскрываемого узла.
        /// </summary>
        private void Tree_BeforeExpand(object sender, TreeViewCancelEventArgs e)
        {
            var node = e.Node;
            var path = node.Tag as string;

            Logger.Info($"[NativeForm] Раскрытие узла | Path={path} | Text={node.Text}");

            if (string.IsNullOrWhiteSpace(path))
            {
                Logger.Warning("[NativeForm] Пропуск раскрытия: пустой путь");
                return;
            }

            if (_client == null)
            {
                Logger.Warning("[NativeForm] Клиент не инициализирован, пробую переподключиться");
                ConnectToSelectedServer();

                if (_client == null)
                {
                    Logger.Error("[NativeForm] Переподключение не удалось");
                    _statusLabel.Text = $"Нет подключения: {SelectedServerHost ?? "сервер не выбран"}";
                    MessageBox.Show(
                        this,
                        $"Не удалось подключиться к серверу '{SelectedServerHost ?? "unknown"}'.\nПроверьте VPN, сеть и доступность сервера.",
                        "Revit Server Native",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }
            }

            if (node.Nodes.Count == 1 && node.Nodes[0].Text == "…")
                node.Nodes.Clear();

            if (node.Nodes.Count > 0)
                return;

            try
            {
                Cursor = Cursors.WaitCursor;
                _tree.Enabled = false;
                _statusLabel.Text = $"Загрузка {path}...";

                var items = _client.GetContents(path);
                Logger.Info($"[NativeForm] Загружено элементов: {items.Count}");

                foreach (var item in items)
                {
                    var childNode = new TreeNode
                    {
                        Name = item.Path,
                        Tag = item.Path,
                        Text = item.IsModel ? $"[ ] {item.Name}" : $"📁 {item.Name}"
                    };

                    if (item.IsFolder)
                        childNode.Nodes.Add(new TreeNode("…") { ForeColor = Color.Gray });
                    else if (item.IsModel)
                        _selectedModels[item.Path] = false;

                    node.Nodes.Add(childNode);
                }

                _statusLabel.Text = $"Загружено: {items.Count} элементов";
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, $"[NativeForm] Ошибка загрузки узла: {path}");
                _statusLabel.Text = "Ошибка загрузки";
                MessageBox.Show(
                    this,
                    $"Не удалось загрузить '{path}':\n{ex.Message}",
                    "Revit Server Native",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                _tree.Enabled = true;
                Cursor = Cursors.Default;
            }
        }

        /// <summary>
        /// Переключает состояние выбора модели по клику.
        /// </summary>
        private void Tree_NodeMouseClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            var node = e.Node;
            var path = node.Tag as string;

            if (e.Button != MouseButtons.Left || string.IsNullOrWhiteSpace(path) || !_selectedModels.ContainsKey(path))
                return;

            _selectedModels[path] = !_selectedModels[path];
            node.Text = _selectedModels[path]
                ? $"[✓] {ExtractModelName(node.Text)}"
                : $"[ ] {ExtractModelName(node.Text)}";

            _statusLabel.Text = $"Выбрано: {_selectedModels.Count(kv => kv.Value)} моделей";
        }

        /// <summary>
        /// Убирает технический префикс выбора из текста модели.
        /// </summary>
        private static string ExtractModelName(string displayText)
        {
            if (displayText.StartsWith("[✓] "))
                return displayText.Substring(4);

            if (displayText.StartsWith("[ ] "))
                return displayText.Substring(4);

            return displayText;
        }

        /// <summary>
        /// Сбрасывает текущий выбор моделей.
        /// </summary>
        public void ResetSelection()
        {
            foreach (var path in _selectedModels.Keys.ToList())
            {
                _selectedModels[path] = false;
                var node = FindNode(path);
                if (node != null)
                    node.Text = $"[ ] {ExtractModelName(node.Text)}";
            }

            _statusLabel.Text = "Выбор сброшен";
        }

        /// <summary>
        /// Находит узел по внутреннему пути.
        /// </summary>
        private TreeNode FindNode(string path)
        {
            return FindNodeRecursive(_tree.Nodes, path);
        }

        /// <summary>
        /// Рекурсивно ищет узел по пути.
        /// </summary>
        private static TreeNode FindNodeRecursive(TreeNodeCollection nodes, string path)
        {
            foreach (TreeNode node in nodes)
            {
                if (path.Equals(node.Tag as string))
                    return node;

                var foundNode = FindNodeRecursive(node.Nodes, path);
                if (foundNode != null)
                    return foundNode;
            }

            return null;
        }

        /// <summary>
        /// Освобождает ресурсы формы при закрытии.
        /// </summary>
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _client?.Dispose();
            base.OnFormClosing(e);
        }

        /// <summary>
        /// Преобразует внутренний путь браузера в RSN:// путь.
        /// </summary>
        private string ToRsnPath(string browserPath)
        {
            if (string.IsNullOrWhiteSpace(browserPath))
            {
                Logger.Warning("[NativeForm] Пустой путь модели, пропускаю");
                return null;
            }

            var host = SelectedServerHost;
            if (string.IsNullOrWhiteSpace(host))
            {
                Logger.Error("[NativeForm] Не выбран хост сервера");
                return null;
            }

            if (!browserPath.StartsWith("|") || !browserPath.EndsWith(".rvt", StringComparison.OrdinalIgnoreCase))
            {
                Logger.Warning($"[NativeForm] Некорректный формат пути: {browserPath}");
                return null;
            }

            var relativePath = browserPath.TrimStart('|').Replace('|', '/');
            var rsnPath = $"RSN://{host}/{relativePath}";
            Logger.Debug($"[NativeForm] Конвертация пути | Browser={browserPath} | Rsn={rsnPath}");
            return rsnPath;
        }
    }
}
