using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace RevitServerBrowser
{
    /// <summary>
    /// Браузер моделей с выбором сервера из списка.
    /// </summary>
    public class RevitServerBrowserForm : Form
    {
        private RevitServerClient _client; // ❗ не readonly — сервер может меняться
        private readonly Dictionary<string, bool> _selectedModels = new Dictionary<string, bool>();
        private TreeView _tree;
        private Label _statusLabel;
        private Button _btnConfirm;
        private ComboBox _serverCombo; // 🔑 Новый элемент
        private readonly int _apiYear; // 🔑 Запоминаем год
        private bool _isInitializing = true;

        /// <summary>
        /// Выбранные пути к моделям.
        /// </summary>
        public IReadOnlyList<string> SelectedModelPaths =>
            _selectedModels.Where(kv => kv.Value).Select(kv => kv.Key).ToList().AsReadOnly();

        /// <summary>
        /// Прямой доступ к кнопке "Подтвердить".
        /// </summary>
        public Button ConfirmButton => _btnConfirm;

        /// <summary>
        /// Выбранный хост сервера (только для чтения).
        /// </summary>
        public string SelectedServerHost => _serverCombo?.SelectedItem is KeyValuePair<string, string> kvp
            ? kvp.Value
            : null;

        /// <summary>
        /// Инициализация формы.
        /// </summary>
        /// <param name="servers">Словарь: Название → Хост. Если пустой — поле ввода станет редактируемым.</param>
        /// <param name="apiYear">Год API Revit Server.</param>
        /// <param name="defaultHost">Хост по умолчанию (если есть).</param>
        public RevitServerBrowserForm(Dictionary<string, string> servers, int apiYear, string defaultHost = null)
        {
            _apiYear = apiYear;
            SetupForm();
            SetupControls(servers, defaultHost);
            ConnectToSelectedServer(); // Подключаемся к серверу по умолчанию
            _isInitializing = false;
            LoadRootNode();
        }

        private void SetupForm()
        {
            Text = "Revit Server Browser";
            Size = new Size(600, 500);
            MinimumSize = new Size(450, 400);
            StartPosition = FormStartPosition.CenterParent;
            Font = new Font("Segoe UI", 9F);
        }

        private void SetupControls(Dictionary<string, string> servers, string defaultHost)
        {
            // 🔑 1. Создаём ВСЕ контролы ДО навешивания событий и установки значений
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
            var lblServer = new Label { Text = "Сервер:", Location = new Point(10, 8), AutoSize = true };
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
            var btnReset = new Button { Text = "🔄 Сброс", Width = 80, FlatStyle = FlatStyle.Flat };
            var btnClose = new Button { Text = "✖ Закрыть", Width = 80, DialogResult = DialogResult.Cancel };

            _statusLabel = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 22,
                BackColor = Color.LightYellow,
                ForeColor = Color.DarkBlue,
                Padding = new Padding(5, 3, 0, 0),
                Text = "Готов"
            };

            // 🔑 2. Собираем иерархию
            topPanel.Controls.Add(lblServer);
            topPanel.Controls.Add(_serverCombo);
            bottomPanel.Controls.Add(btnClose);
            bottomPanel.Controls.Add(btnReset);
            bottomPanel.Controls.Add(_btnConfirm);
            Controls.Add(_tree);
            Controls.Add(_statusLabel);
            Controls.Add(bottomPanel);
            Controls.Add(topPanel);

            // 🔑 3. Навешиваем события ТОЛЬКО ТЕПЕРЬ
            _serverCombo.SelectedIndexChanged += (s, e) => OnServerChanged();
            btnReset.Click += (s, e) => ResetSelection();
            btnClose.Click += (s, e) => Close();
            _tree.BeforeExpand += Tree_BeforeExpand;
            _tree.NodeMouseClick += Tree_NodeMouseClick;

            // 🔑 4. Заполняем комбобокс и выставляем индекс (событие сработает безопасно)
            if (servers.Any())
            {
                foreach (var kvp in servers) _serverCombo.Items.Add(kvp);

                if (!string.IsNullOrEmpty(defaultHost))
                {
                    var match = servers.FirstOrDefault(kvp => kvp.Value.Equals(defaultHost, StringComparison.OrdinalIgnoreCase));
                    _serverCombo.SelectedItem = match.Key != null ? match : servers.First();
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
        /// Переподключает клиент к новому серверу при смене выбора.
        /// </summary>
        private void OnServerChanged()
        {
            if (_isInitializing) return;
            ConnectToSelectedServer();
            ReloadTree();
        }

        private void ConnectToSelectedServer()
        {
            var host = SelectedServerHost;
            if (!string.IsNullOrEmpty(host))
            {
                _client?.Dispose();
                _client = new RevitServerClient(host, _apiYear);
                Text = $"Revit Server Browser [{host}]";
            }
        }

        private void ReloadTree()
        {
            // 🔑 Защита: если дерево ещё не создано (вызов при инициализации), просто выходим
            if (_tree == null) return;

            _tree.Nodes.Clear();
            _selectedModels.Clear();
            LoadRootNode();
        }

        private void LoadRootNode()
        {
            var root = new TreeNode("📁 Root") { Name = "|", Tag = "|" };
            root.Nodes.Add(new TreeNode("…") { ForeColor = Color.Gray });
            _tree.Nodes.Add(root);
        }

        private void Tree_BeforeExpand(object sender, TreeViewCancelEventArgs e)
        {
            var node = e.Node;
            var path = node.Tag as string;

            Logger.Info($"[TREE] Раскрытие узла: path='{path}', text='{node.Text}'");

            if (string.IsNullOrEmpty(path))
            {
                Logger.Warning("[TREE] ❌ Пустой path, выходим");
                return;
            }

            // Проверка клиента
            if (_client == null)
            {
                Logger.Error("[TREE] ❌ _client == null!");
                _statusLabel.Text = "Ошибка: клиент не инициализирован";
                return;
            }

            if (node.Nodes.Count == 1 && node.Nodes[0].Text == "…")
            {
                Logger.Debug($"[TREE] Удаляем placeholder для '{path}'");
                node.Nodes.Clear();
            }

            if (node.Nodes.Count > 0)
            {
                Logger.Debug($"[TREE] Узел уже загружен, выходим");
                return;
            }

            try
            {
                Logger.Info($"[TREE] Запрос к серверу {_client} для пути '{path}'");
                Cursor = Cursors.WaitCursor;
                _tree.Enabled = false;
                _statusLabel.Text = $"Загрузка {path}...";

                var items = _client.GetContents(path);

                Logger.Info($"[TREE] Ответ сервера: {items?.Count ?? 0} элементов");

                // 🔥 Дамп первых 5 элементов для отладки
                if (items != null && items.Any())
                {
                    for (int i = 0; i < Math.Min(5, items.Count); i++)
                    {
                        var it = items[i];
                        Logger.Debug($"[TREE-DUMP] #{i + 1}: Name='{it.Name}', Type='{it.ItemType}', Path='{it.Path}'");
                    }
                }
                else
                {
                    Logger.Warning($"[TREE] ⚠️ Сервер вернул пустой список для '{path}'");
                }

                foreach (var item in items)
                {
                    var child = new TreeNode
                    {
                        Name = item.Path,
                        Tag = item.Path,
                        Text = item.IsModel ? $"[ ] {item.Name}" : $"📁 {item.Name}"
                    };

                    if (item.IsFolder)
                        child.Nodes.Add(new TreeNode("…") { ForeColor = Color.Gray });
                    else if (item.IsModel)
                        _selectedModels[item.Path] = false;

                    node.Nodes.Add(child);
                }

                _statusLabel.Text = $"Загружено: {items.Count} элементов";
                Logger.Info($"[TREE] ✅ Узел '{path}' загружен успешно");
            }
            catch (Exception ex)
            {
                Logger.Error($"[TREE] ❌ Исключение при загрузке '{path}': {ex.Message}");
                Logger.Error($"[TREE] Stack: {ex.StackTrace}");
                if (ex.InnerException != null)
                    Logger.Error($"[TREE] Inner: {ex.InnerException.Message}");

                _statusLabel.Text = "Ошибка загрузки";
                MessageBox.Show(this, $"Не удалось загрузить '{path}':\n{ex.Message}",
                    "Revit Server", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _tree.Enabled = true;
                Cursor = Cursors.Default;
            }
        }

        private void Tree_NodeMouseClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            var node = e.Node;
            var path = node.Tag as string;

            if (e.Button != MouseButtons.Left || string.IsNullOrEmpty(path) || !_selectedModels.ContainsKey(path))
                return;

            _selectedModels[path] = !_selectedModels[path];
            node.Text = _selectedModels[path]
                ? $"[✓] {ExtractModelName(node.Text)}"
                : $"[ ] {ExtractModelName(node.Text)}";

            _statusLabel.Text = $"Выбрано: {_selectedModels.Count(kv => kv.Value)} моделей";
        }

        private static string ExtractModelName(string displayText)
        {
            if (displayText.StartsWith("[✓] ")) return displayText.Substring(4);
            if (displayText.StartsWith("[ ] ")) return displayText.Substring(4);
            return displayText;
        }

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

        private TreeNode FindNode(string path) => FindNodeRecursive(_tree.Nodes, path);

        private TreeNode FindNodeRecursive(TreeNodeCollection nodes, string path)
        {
            foreach (TreeNode node in nodes)
            {
                if (path.Equals(node.Tag as string)) return node;
                var found = FindNodeRecursive(node.Nodes, path);
                if (found != null) return found;
            }
            return null;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _client?.Dispose();
            base.OnFormClosing(e);
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            // 
            // RevitServerBrowserForm
            // 
            this.ClientSize = new System.Drawing.Size(284, 261);
            this.Name = "RevitServerBrowserForm";
            this.ResumeLayout(false);

        }
    }
}
