using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace MyOmniNative
{
    internal sealed class ChatMessage
    {
        public string role { get; set; }
        public string content { get; set; }
    }

    internal sealed class PaletteCommand
    {
        public string Name { get; set; }
        public Action Execute { get; set; }

        public override string ToString()
        {
            return Name;
        }
    }

    internal sealed class RecentProject
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public string LastOpened { get; set; }
    }

    internal sealed class OpenDocument
    {
        public string Path { get; set; }
        public string Name { get; set; }
        public RichTextBox Editor { get; set; }
        public Panel Gutter { get; set; }
        public bool Dirty { get; set; }

        public override string ToString()
        {
            return (Dirty ? "* " : String.Empty) + Name;
        }
    }

    internal sealed class MainForm : Form
    {
        private const string Endpoint = "http://127.0.0.1:20128/v1";
        private const string OmniRouteVersion = "3.8.53";
        private const string ApplicationVersion = "0.1.0";
        private const int MaxFileBytes = 512 * 1024;
        private const int MaxAgentFileCharacters = 20000;
        private const int MaxWorkspaceFiles = 1000;
        private const int MaxWorkspaceBytes = 40 * 1024 * 1024;

        private readonly string _dataRoot;
        private readonly string _runtimeRoot;
        private readonly string _gatewayRoot;
        private readonly string _keyPath;
        private readonly string _settingsPath;
        private readonly JavaScriptSerializer _serializer = new JavaScriptSerializer();
        private readonly List<OpenDocument> _documents = new List<OpenDocument>();
        private readonly List<RecentProject> _recentProjects = new List<RecentProject>();
        private readonly List<ChatMessage> _history = new List<ChatMessage>();
        private readonly TreeView _tree = new TreeView();
        private readonly ImageList _treeImages = new ImageList();
        private readonly ListBox _openEditorsList = new ListBox();
        private readonly TabControl _tabs = new TabControl();
        private readonly RichTextBox _chatLog = new RichTextBox();
        private readonly TextBox _chatInput = new TextBox();
        private readonly TextBox _apiKeyInput = new TextBox();
        private readonly ComboBox _modelInput = new ComboBox();
        private readonly ComboBox _chatModelSelector = new ComboBox();
        private readonly Button _sendButton = new Button();
        private readonly Button _testConnectionButton = new Button();
        private Button _retryButton;
        private Button _copyButton;
        private readonly Button _installButton = new Button();
        private readonly Button _connectButton = new Button();
        private readonly Label _connectionLabel = new Label();
        private readonly Label _chatConnectionStatus = new Label();
        private readonly Label _apiKeyStatus = new Label();
        private readonly Label _statusLabel = new Label();
        private readonly ToolStripStatusLabel _workspaceStatus = new ToolStripStatusLabel();
        private readonly ToolStripStatusLabel _fileStatus = new ToolStripStatusLabel();
        private readonly ToolStripStatusLabel _gatewayStatus = new ToolStripStatusLabel();
        private readonly ToolStripStatusLabel _branchStatus = new ToolStripStatusLabel();
        private readonly ToolStripStatusLabel _encodingStatus = new ToolStripStatusLabel();
        private readonly ToolStripStatusLabel _languageStatus = new ToolStripStatusLabel();
        private readonly ToolStripStatusLabel _positionStatus = new ToolStripStatusLabel();
        private readonly ToolStripStatusLabel _notificationStatus = new ToolStripStatusLabel();
        private readonly Panel _welcomePanel = new Panel();
        private readonly Panel _sidebarContent = new Panel();
        private readonly Label _sidebarTitle = new Label();
        private readonly FlowLayoutPanel _welcomeRecentPanel = new FlowLayoutPanel();
        private readonly Label _homeModelLabel = new Label();
        private readonly ToolTip _navigationToolTip = new ToolTip();
        private readonly ToolStripMenuItem _aiSidebarMenu = new ToolStripMenuItem("AI Chat Sidebar") { Checked = true, CheckOnClick = true };
        private readonly System.Windows.Forms.Timer _highlightTimer = new System.Windows.Forms.Timer();
    private readonly System.Windows.Forms.Timer _sidebarAnimationTimer = new System.Windows.Forms.Timer();
    private readonly System.Windows.Forms.Timer _chatAnimationTimer = new System.Windows.Forms.Timer();
    private int _sidebarAnimationTarget;
    private int _chatAnimationTarget;
        private Form _profileWindow;
        private TabControl _settingsTabs;
        private CheckBox _autoStartGatewayCheckBox;
        private CheckBox _editorWordWrapCheckBox;
        private CheckBox _showHiddenFilesCheckBox;
        private CheckBox _autoIncludeActiveEditorCheckBox;
        private NumericUpDown _editorFontSizeSetting;
        private NumericUpDown _maximumAgentCharactersSetting;
        private NumericUpDown _maximumWorkspaceFilesSetting;
        private Label _accentPreviewLabel;
        private SplitContainer _workspaceSplit;
        private SplitContainer _editorSplit;
        private readonly HttpClient _http = new HttpClient();
        private Process _gatewayProcess;
        private CancellationTokenSource _chatCancellation;
        private string _workspaceRoot = String.Empty;
        private string _model = "auto";
        private string _lastPrompt = String.Empty;
        private OpenDocument _pendingHighlight;
        private bool _connected;
        private bool _busy;
        private bool _apiKeySaved;
        private bool _hasAssistantResponse;
        private bool _isClosing;
        private bool _loadingSettings;
        private bool _autoStartGateway = true;
        private bool _autoIncludeActiveEditor = true;
        private bool _showHiddenFiles;
        private bool _editorWordWrap;
        private int _editorFontSize = 10;
        private int _maximumAgentCharacters = MaxAgentFileCharacters;
        private int _maximumWorkspaceFiles = MaxWorkspaceFiles;
        private Color _accentColor = Color.FromArgb(125, 211, 252);
        private long _workspaceBytes;
        private int _workspaceFileCount;
        private int _untitledCount;

        public MainForm()
        {
            Text = "Open Editor - My Omni";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(980, 680);
            Size = new Size(1520, 940);
            BackColor = Color.FromArgb(11, 17, 32);
            ForeColor = Color.FromArgb(237, 245, 255);
            Font = new Font("Segoe UI", 9F);
            KeyPreview = true;
            _http.Timeout = TimeSpan.FromSeconds(60);
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            _dataRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "My Omni");
            _runtimeRoot = Path.Combine(_dataRoot, "runtime");
            _gatewayRoot = Path.Combine(_dataRoot, "omniroute");
            _keyPath = Path.Combine(_dataRoot, "api-key.dpapi");
            _settingsPath = Path.Combine(_dataRoot, "settings.json");

            _loadingSettings = true;
            BuildUi();
            LoadSettings();
            _loadingSettings = false;
            _highlightTimer.Interval = 300;
            _highlightTimer.Tick += delegate
            {
                _highlightTimer.Stop();
                OpenDocument document = _pendingHighlight;
                _pendingHighlight = null;
                if (document != null) HighlightDocument(document);
            };
            _sidebarAnimationTimer.Interval = 16;
            _sidebarAnimationTimer.Tick += AnimateWorkspaceSidebar;
            _chatAnimationTimer.Interval = 16;
            _chatAnimationTimer.Tick += AnimateAssistantPanel;
            FormClosing += OnFormClosing;
            KeyDown += OnFormKeyDown;
            Shown += async delegate
            {
                ConfigureSplitContainer(_workspaceSplit, 160, 500, 250);
                if (_editorSplit != null)
                    ConfigureSplitContainer(_editorSplit, 350, 280, _editorSplit.ClientSize.Width - _editorSplit.SplitterWidth - 360);
                if (_autoStartGateway) await AutoStartGatewayAsync();
                else
                {
                    _gatewayStatus.Text = "Auto-start disabled";
                    _connectionLabel.Text = "OmniRoute auto-start is disabled in Settings > General.";
                    UpdateConnectionStatus();
                }
            };
        }

        private void ConfigureSplitContainer(SplitContainer split, int minimumPanel1, int minimumPanel2, int preferredDistance)
        {
            if (split == null) return;
            int available = split.ClientSize.Width - split.SplitterWidth;
            if (available < 2) return;
            if (minimumPanel1 + minimumPanel2 > available)
            {
                int combinedMinimum = minimumPanel1 + minimumPanel2;
                minimumPanel1 = (int)((long)available * minimumPanel1 / combinedMinimum);
                minimumPanel2 = available - minimumPanel1;
            }

            split.Panel1MinSize = 0;
            split.Panel2MinSize = 0;
            int distance = Math.Max(0, Math.Min(available, preferredDistance));
            split.SplitterDistance = distance;
            distance = Math.Max(minimumPanel1, Math.Min(available - minimumPanel2, distance));
            split.Panel1MinSize = minimumPanel1;
            split.Panel2MinSize = minimumPanel2;
            split.SplitterDistance = distance;
        }

        private void BuildUi()
        {
            TableLayoutPanel shell = new TableLayoutPanel();
            shell.Dock = DockStyle.Fill;
            shell.Margin = Padding.Empty;
            shell.Padding = Padding.Empty;
            shell.ColumnCount = 1;
            shell.RowCount = 3;
            shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
            Controls.Add(shell);

            MenuStrip menu = new MenuStrip();
            menu.Dock = DockStyle.Fill;
            menu.BackColor = Color.FromArgb(17, 24, 39);
            menu.ForeColor = ForeColor;
            ToolStripMenuItem fileMenu = new ToolStripMenuItem("File");
            ToolStripMenuItem newFileMenu = new ToolStripMenuItem("New File", null, delegate { NewFile(); });
            newFileMenu.ShortcutKeys = Keys.Control | Keys.N;
            fileMenu.DropDownItems.Add(newFileMenu);
            ToolStripMenuItem openFolderMenu = new ToolStripMenuItem("Open Folder...", null, delegate { OpenWorkspace(); });
            openFolderMenu.ShortcutKeys = Keys.Control | Keys.Shift | Keys.O;
            fileMenu.DropDownItems.Add(openFolderMenu);
            ToolStripMenuItem openFileMenu = new ToolStripMenuItem("Open File...", null, delegate { OpenFileDialog(); });
            openFileMenu.ShortcutKeys = Keys.Control | Keys.O;
            fileMenu.DropDownItems.Add(openFileMenu);
            fileMenu.DropDownItems.Add(new ToolStripSeparator());
            ToolStripMenuItem saveMenu = new ToolStripMenuItem("Save", null, delegate { SaveActiveDocument(); });
            saveMenu.ShortcutKeys = Keys.Control | Keys.S;
            fileMenu.DropDownItems.Add(saveMenu);
            ToolStripMenuItem saveAsMenu = new ToolStripMenuItem("Save As...", null, delegate { SaveAsActiveDocument(); });
            saveAsMenu.ShortcutKeys = Keys.Control | Keys.Shift | Keys.S;
            fileMenu.DropDownItems.Add(saveAsMenu);
            fileMenu.DropDownItems.Add("Save All", null, delegate { SaveAllDocuments(); });
            fileMenu.DropDownItems.Add(new ToolStripSeparator());
            fileMenu.DropDownItems.Add("Exit", null, delegate { Close(); });
            menu.Items.Add(fileMenu);

            ToolStripMenuItem editMenu = new ToolStripMenuItem("Edit");
            editMenu.DropDownItems.Add("Undo", null, delegate { RunEditorCommand(delegate(RichTextBox editor) { if (editor.CanUndo) editor.Undo(); }); });
            editMenu.DropDownItems.Add("Redo", null, delegate { RunEditorCommand(delegate(RichTextBox editor) { if (editor.CanRedo) editor.Redo(); }); });
            editMenu.DropDownItems.Add(new ToolStripSeparator());
            editMenu.DropDownItems.Add("Cut", null, delegate { RunEditorCommand(delegate(RichTextBox editor) { editor.Cut(); }); });
            editMenu.DropDownItems.Add("Copy", null, delegate { RunEditorCommand(delegate(RichTextBox editor) { editor.Copy(); }); });
            editMenu.DropDownItems.Add("Paste", null, delegate { RunEditorCommand(delegate(RichTextBox editor) { editor.Paste(); }); });
            editMenu.DropDownItems.Add("Select All", null, delegate { RunEditorCommand(delegate(RichTextBox editor) { editor.SelectAll(); }); });
            menu.Items.Add(editMenu);

            ToolStripMenuItem selectionMenu = new ToolStripMenuItem("Selection");
            selectionMenu.DropDownItems.Add("Select All", null, delegate { RunEditorCommand(delegate(RichTextBox editor) { editor.SelectAll(); }); });
            menu.Items.Add(selectionMenu);

            ToolStripMenuItem viewMenu = new ToolStripMenuItem("View");
            _aiSidebarMenu.CheckedChanged += delegate { SetAssistantVisible(_aiSidebarMenu.Checked); };
            _aiSidebarMenu.Text = "AI Chat";
            _aiSidebarMenu.ShortcutKeys = Keys.Control | Keys.Shift | Keys.I;
            viewMenu.DropDownItems.Add(_aiSidebarMenu);
            ToolStripMenuItem commandPaletteMenu = new ToolStripMenuItem("Command Palette...", null, delegate { ShowCommandPalette(); });
            commandPaletteMenu.ShortcutKeys = Keys.Control | Keys.Shift | Keys.P;
            viewMenu.DropDownItems.Add(commandPaletteMenu);
            menu.Items.Add(viewMenu);

            ToolStripMenuItem goMenu = new ToolStripMenuItem("Go");
            ToolStripMenuItem goToLine = new ToolStripMenuItem("Go to Line...", null, delegate { GoToLine(); });
            goToLine.ShortcutKeys = Keys.Control | Keys.G;
            goMenu.DropDownItems.Add(goToLine);
            goMenu.DropDownItems.Add("Open File...", null, delegate { OpenFileDialog(); });
            menu.Items.Add(goMenu);

            ToolStripMenuItem runMenu = new ToolStripMenuItem("Run");
            runMenu.DropDownItems.Add("Run and Debug unavailable in this build", null, delegate
            {
                MessageBox.Show(this, "Code execution is disabled until an isolated execution worker is available.", "Run and Debug", MessageBoxButtons.OK, MessageBoxIcon.Information);
            });
            menu.Items.Add(runMenu);

            ToolStripMenuItem terminalMenu = new ToolStripMenuItem("Terminal");
            terminalMenu.DropDownItems.Add("Integrated terminal unavailable in this build", null, delegate
            {
                MessageBox.Show(this, "This native build does not provide an integrated terminal.", "Terminal", MessageBoxButtons.OK, MessageBoxIcon.Information);
            });
            menu.Items.Add(terminalMenu);

            ToolStripMenuItem aiMenu = new ToolStripMenuItem("AI");
            ToolStripMenuItem omniRouteMenu = new ToolStripMenuItem("OmniRoute");
            omniRouteMenu.DropDownItems.Add("Install / Start Local Gateway", null, async delegate { ShowProfile(); await InstallOrStartGatewayAsync(); });
            omniRouteMenu.DropDownItems.Add("Open Dashboard", null, delegate { OpenDashboard(); });
            aiMenu.DropDownItems.Add(omniRouteMenu);
            menu.Items.Add(aiMenu);

            ToolStripMenuItem settingsMenu = new ToolStripMenuItem("Settings");
            settingsMenu.DropDownItems.Add("General...", null, delegate { ShowSettingsTab("General"); });
            settingsMenu.DropDownItems.Add("Appearance...", null, delegate { ShowSettingsTab("Appearance"); });
            settingsMenu.DropDownItems.Add("Editor...", null, delegate { ShowSettingsTab("Editor"); });
            settingsMenu.DropDownItems.Add("Explorer...", null, delegate { ShowSettingsTab("Explorer"); });
            settingsMenu.DropDownItems.Add("AI Agent...", null, delegate { ShowSettingsTab("AI Agent"); });
            ToolStripMenuItem providersMenu = new ToolStripMenuItem("AI Providers");
            providersMenu.DropDownItems.Add("OmniRoute...", null, delegate { ShowProfile(); });
            settingsMenu.DropDownItems.Add(providersMenu);
            menu.Items.Add(settingsMenu);

            ToolStripMenuItem helpMenu = new ToolStripMenuItem("Help");
            helpMenu.DropDownItems.Add("About MY OMNI", null, delegate
            {
                MessageBox.Show(this, "MY OMNI\r\nNative Windows coding workbench\r\nOmniRoute-powered assistant", "About MY OMNI", MessageBoxButtons.OK, MessageBoxIcon.Information);
            });
            menu.Items.Add(helpMenu);
            MainMenuStrip = menu;
            shell.Controls.Add(menu, 0, 0);

            SplitContainer workspace = new SplitContainer();
            _workspaceSplit = workspace;
            workspace.Dock = DockStyle.Fill;
            workspace.FixedPanel = FixedPanel.Panel1;
            workspace.BackColor = Color.FromArgb(38, 50, 68);
            workspace.Panel1Collapsed = true;
            shell.Controls.Add(workspace, 0, 1);

            TableLayoutPanel explorerLayout = new TableLayoutPanel();
            explorerLayout.Dock = DockStyle.Fill;
            explorerLayout.Margin = Padding.Empty;
            explorerLayout.Padding = Padding.Empty;
            explorerLayout.ColumnCount = 2;
            explorerLayout.RowCount = 1;
            explorerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 48));
            explorerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            explorerLayout.BackColor = Color.FromArgb(15, 23, 42);
            workspace.Panel1.Controls.Add(explorerLayout);

            FlowLayoutPanel activityBar = new FlowLayoutPanel();
            activityBar.Dock = DockStyle.Fill;
            activityBar.FlowDirection = FlowDirection.TopDown;
            activityBar.WrapContents = false;
            activityBar.AutoScroll = true;
            activityBar.Padding = new Padding(3, 8, 3, 3);
            activityBar.BackColor = Color.FromArgb(11, 17, 32);
            explorerLayout.Controls.Add(activityBar, 0, 0);
            AddActivityButton(activityBar, "◫", "Explorer", "Explorer");
            AddActivityButton(activityBar, "⌕", "Search", "Search");
            AddActivityButton(activityBar, "⑂", "Source Control", "Source Control");
            AddActivityButton(activityBar, "▶", "Run and Debug", "Run and Debug");
            AddActivityButton(activityBar, "▦", "Extensions", "Extensions");
            AddActivityButton(activityBar, "⚙", "Settings", "Settings");
            AddActivityButton(activityBar, "●", "Accounts", "Accounts");

            Panel explorer = new Panel();
            explorer.Dock = DockStyle.Fill;
            explorer.BackColor = Color.FromArgb(15, 23, 42);
            explorerLayout.Controls.Add(explorer, 1, 0);
            TableLayoutPanel sidebarLayout = new TableLayoutPanel();
            sidebarLayout.Dock = DockStyle.Fill;
            sidebarLayout.Margin = Padding.Empty;
            sidebarLayout.Padding = Padding.Empty;
            sidebarLayout.ColumnCount = 1;
            sidebarLayout.RowCount = 2;
            sidebarLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            sidebarLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            explorer.Controls.Add(sidebarLayout);
            _sidebarTitle.Text = "EXPLORER";
            _sidebarTitle.ForeColor = Color.FromArgb(156, 169, 187);
            _sidebarTitle.BackColor = Color.Transparent;
            _sidebarTitle.Padding = new Padding(12, 10, 0, 0);
            _sidebarTitle.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
            _sidebarTitle.Dock = DockStyle.Fill;
            sidebarLayout.Controls.Add(_sidebarTitle, 0, 0);
            _sidebarContent.Dock = DockStyle.Fill;
            _sidebarContent.BackColor = explorer.BackColor;
            sidebarLayout.Controls.Add(_sidebarContent, 0, 1);
            _tree.Dock = DockStyle.Fill;
            _tree.BackColor = explorer.BackColor;
            _tree.ForeColor = ForeColor;
            _tree.BorderStyle = BorderStyle.None;
            _tree.HideSelection = false;
            _treeImages.ColorDepth = ColorDepth.Depth32Bit;
            _treeImages.ImageSize = new Size(16, 16);
            AddExplorerIcon("folder", Color.FromArgb(224, 177, 82), "D", true);
            AddExplorerIcon("file", Color.FromArgb(126, 158, 190), "F", false);
            AddExplorerIcon("code", Color.FromArgb(86, 185, 210), "<>", false);
            AddExplorerIcon("script", Color.FromArgb(221, 193, 91), "JS", false);
            AddExplorerIcon("python", Color.FromArgb(102, 166, 112), "PY", false);
            AddExplorerIcon("json", Color.FromArgb(194, 157, 89), "{}", false);
            AddExplorerIcon("markup", Color.FromArgb(216, 131, 95), "<> ", false);
            AddExplorerIcon("style", Color.FromArgb(167, 130, 205), "#", false);
            _tree.ImageList = _treeImages;
            _tree.NodeMouseDoubleClick += OnTreeNodeDoubleClick;
            _tree.NodeMouseClick += delegate(object sender, TreeNodeMouseClickEventArgs e)
            {
                if (e.Button == MouseButtons.Right) _tree.SelectedNode = e.Node;
            };
            _tree.ContextMenuStrip = CreateExplorerContextMenu();
            _openEditorsList.Dock = DockStyle.Fill;
            _openEditorsList.BackColor = explorer.BackColor;
            _openEditorsList.ForeColor = ForeColor;
            _openEditorsList.BorderStyle = BorderStyle.None;
            _openEditorsList.DoubleClick += delegate
            {
                OpenDocument document = _openEditorsList.SelectedItem as OpenDocument;
                if (document != null)
                {
                    foreach (TabPage page in _tabs.TabPages)
                    {
                        if (page.Tag == document) { _tabs.SelectedTab = page; break; }
                    }
                }
            };
            _tabs.SelectedIndexChanged += delegate { RefreshOpenEditors(); };
            SwitchSidebarView("Explorer");

            SplitContainer editorChat = new SplitContainer();
            _editorSplit = editorChat;
            editorChat.Dock = DockStyle.Fill;
            editorChat.FixedPanel = FixedPanel.Panel2;
            editorChat.BackColor = Color.FromArgb(38, 50, 68);
            workspace.Panel2.Controls.Add(editorChat);

            Panel editorPanel = new Panel();
            editorPanel.Dock = DockStyle.Fill;
            editorPanel.BackColor = Color.FromArgb(30, 30, 30);
            editorChat.Panel1.Controls.Add(editorPanel);
            _tabs.Dock = DockStyle.Fill;
            _tabs.Appearance = TabAppearance.Normal;
            _tabs.BackColor = editorPanel.BackColor;
            _tabs.Padding = new Point(14, 5);
            _tabs.DrawMode = TabDrawMode.OwnerDrawFixed;
            _tabs.SizeMode = TabSizeMode.Fixed;
            _tabs.ItemSize = new Size(156, 28);
            _tabs.DrawItem += OnEditorTabDrawItem;
            _tabs.MouseDown += OnEditorTabMouseDown;
            _tabs.SelectedIndexChanged += delegate { UpdateFileStatus(); UpdateWelcomeVisibility(); };
            editorPanel.Controls.Add(_tabs);
            EnableFileDrop(editorPanel);
            BuildWelcomePanel(editorPanel);
            UpdateWelcomeVisibility();

            BuildAssistantPanel(editorChat.Panel2);

            StatusStrip status = new StatusStrip();
            status.BackColor = Color.FromArgb(17, 24, 39);
            status.ForeColor = Color.FromArgb(156, 169, 187);
            _workspaceStatus.Text = String.Empty;
            _fileStatus.Text = "Ready";
            _gatewayStatus.Text = "OmniRoute not connected";
            status.Items.Add(_branchStatus);
            status.Items.Add(new ToolStripStatusLabel("  |  "));
            status.Items.Add(_encodingStatus);
            status.Items.Add(new ToolStripStatusLabel("  |  "));
            status.Items.Add(_languageStatus);
            status.Items.Add(new ToolStripStatusLabel("  |  "));
            status.Items.Add(_positionStatus);
            status.Items.Add(new ToolStripStatusLabel { Spring = true });
            status.Items.Add(_fileStatus);
            status.Items.Add(new ToolStripStatusLabel("  |  "));
            status.Items.Add(_gatewayStatus);
            status.Items.Add(new ToolStripStatusLabel("  |  "));
            status.Items.Add(_notificationStatus);
            _encodingStatus.Text = "UTF-8";
            _languageStatus.Text = "Plain Text";
            _positionStatus.Text = "Ln 1, Col 1";
            _notificationStatus.Text = "No notifications";
            status.SizingGrip = false;
            status.Dock = DockStyle.Fill;
            shell.Controls.Add(status, 0, 2);
        }

        private void BuildWelcomePanel(Control host)
        {
            _welcomePanel.Dock = DockStyle.Fill;
            _welcomePanel.BackColor = Color.FromArgb(30, 30, 30);
            _welcomePanel.Padding = new Padding(28, 24, 28, 20);
            host.Controls.Add(_welcomePanel);
            _welcomePanel.BringToFront();

            TableLayoutPanel layout = new TableLayoutPanel();
            layout.Dock = DockStyle.Fill;
            layout.ColumnCount = 1;
            layout.RowCount = 1;
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.BackColor = _welcomePanel.BackColor;
            _welcomePanel.Controls.Add(layout);

            FlowLayoutPanel welcome = new FlowLayoutPanel();
            welcome.Dock = DockStyle.Fill;
            welcome.FlowDirection = FlowDirection.TopDown;
            welcome.WrapContents = false;
            welcome.AutoScroll = true;
            welcome.Padding = new Padding(8, 20, 8, 16);
            welcome.BackColor = _welcomePanel.BackColor;
            layout.Controls.Add(welcome, 0, 0);

            Label title = new Label { Text = "Welcome to Open Editor", AutoSize = true, ForeColor = Color.FromArgb(237, 245, 255), Font = new Font("Segoe UI Semibold", 25F, FontStyle.Bold), Margin = new Padding(0, 0, 0, 8) };
            welcome.Controls.Add(title);

            Label startHeading = MakeHeading("START");
            startHeading.Margin = new Padding(0, 8, 0, 10);
            welcome.Controls.Add(startHeading);
            AddWelcomeAction(welcome, "New File", delegate { NewFile(); });
            AddWelcomeAction(welcome, "Open File...", delegate { OpenFileDialog(); });
            AddWelcomeAction(welcome, "Open Folder...", delegate { OpenWorkspace(); });
            AddWelcomeAction(welcome, "Clone Git Repository...", delegate { CloneRepository(); });

            Label recentHeading = MakeHeading("RECENT PROJECTS");
            recentHeading.Margin = new Padding(0, 24, 0, 8);
            welcome.Controls.Add(recentHeading);
            _welcomeRecentPanel.Dock = DockStyle.Top;
            _welcomeRecentPanel.FlowDirection = FlowDirection.TopDown;
            _welcomeRecentPanel.WrapContents = false;
            _welcomeRecentPanel.AutoSize = false;
            _welcomeRecentPanel.Width = Math.Max(240, welcome.ClientSize.Width - welcome.Padding.Horizontal - 16);
            _welcomeRecentPanel.BackColor = _welcomePanel.BackColor;
            _welcomeRecentPanel.Margin = Padding.Empty;
            welcome.Controls.Add(_welcomeRecentPanel);
            welcome.SizeChanged += delegate
            {
                _welcomeRecentPanel.Width = Math.Max(240, welcome.ClientSize.Width - welcome.Padding.Horizontal - 16);
                RefreshWelcomeRecents();
            };
            RefreshWelcomeRecents();
        }

        private void AddActivityButton(Control host, string glyph, string name, string view)
        {
            Button button = new Button();
            button.Text = glyph;
            button.Width = 40;
            button.Height = 40;
            button.Margin = new Padding(0, 0, 0, 5);
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.BackColor = Color.FromArgb(11, 17, 32);
            button.ForeColor = Color.FromArgb(178, 191, 207);
            button.Font = new Font("Segoe UI Symbol", 16F);
            button.Cursor = Cursors.Hand;
            button.AccessibleName = name;
            button.MouseEnter += delegate { button.BackColor = Color.FromArgb(24, 38, 58); button.ForeColor = _accentColor; };
            button.MouseLeave += delegate { button.BackColor = Color.FromArgb(11, 17, 32); button.ForeColor = Color.FromArgb(178, 191, 207); };
            _navigationToolTip.SetToolTip(button, name);
            button.Click += delegate { SwitchSidebarView(view); };
            host.Controls.Add(button);
        }

        private void SwitchSidebarView(string view)
        {
            _sidebarTitle.Text = view.ToUpperInvariant();
            _sidebarContent.Controls.Clear();
            if (view == "Explorer") BuildExplorerSidebar();
            else if (view == "Search") BuildSearchSidebar();
            else if (view == "Source Control") BuildSourceControlSidebar();
            else if (view == "Run and Debug") BuildRunSidebar();
            else if (view == "Extensions") BuildExtensionsSidebar();
            else if (view == "Settings") BuildSettingsSidebar();
            else BuildAccountsSidebar();
        }

        private void BuildExplorerSidebar()
        {
            TableLayoutPanel layout = new TableLayoutPanel();
            layout.Dock = DockStyle.Fill;
            layout.ColumnCount = 1;
            layout.RowCount = 5;
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 34));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 66));
            layout.BackColor = Color.FromArgb(15, 23, 42);
            _sidebarContent.Controls.Add(layout);
            Label editorsHeading = MakeHeading("OPEN EDITORS");
            editorsHeading.Dock = DockStyle.Fill;
            layout.Controls.Add(editorsHeading, 0, 0);
            layout.Controls.Add(_openEditorsList, 0, 1);
            if (!String.IsNullOrEmpty(_workspaceRoot))
            {
                Label foldersHeading = MakeHeading(Path.GetFileName(_workspaceRoot).ToUpperInvariant());
                foldersHeading.Dock = DockStyle.Fill;
                layout.Controls.Add(foldersHeading, 0, 2);
            }
            FlowLayoutPanel projectActions = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Padding = new Padding(6, 3, 0, 0), BackColor = Color.FromArgb(15, 23, 42) };
            Button openFolder = CreateToolbarButton("Open Folder", delegate { OpenWorkspace(); });
            openFolder.AutoSize = false;
            openFolder.Width = 83;
            openFolder.Height = 28;
            openFolder.Margin = new Padding(0, 0, 4, 0);
            Button clone = CreateToolbarButton("Clone", delegate { CloneRepository(); });
            clone.AutoSize = false;
            clone.Width = 68;
            clone.Height = 28;
            clone.Margin = new Padding(0, 0, 0, 0);
            projectActions.Controls.Add(openFolder);
            projectActions.Controls.Add(clone);
            layout.Controls.Add(projectActions, 0, 3);
            layout.Controls.Add(_tree, 0, 4);
            RefreshOpenEditors();
        }

        private void BuildSearchSidebar()
        {
            TableLayoutPanel layout = new TableLayoutPanel();
            layout.Dock = DockStyle.Fill;
            layout.Padding = new Padding(10, 8, 10, 8);
            layout.ColumnCount = 1;
            layout.RowCount = 3;
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            _sidebarContent.Controls.Add(layout);
            Label label = CreateInfoLabel("Find files in the open workspace");
            layout.Controls.Add(label, 0, 0);
            TextBox query = new TextBox();
            StyleInput(query);
            layout.Controls.Add(query, 0, 1);
            ListBox results = new ListBox { Dock = DockStyle.Fill, BackColor = Color.FromArgb(15, 23, 42), ForeColor = ForeColor, BorderStyle = BorderStyle.None };
            layout.Controls.Add(results, 0, 2);
            query.TextChanged += delegate
            {
                results.Items.Clear();
                if (String.IsNullOrWhiteSpace(query.Text)) return;
                List<string> files = new List<string>();
                CollectTreeFiles(_tree.Nodes, files);
                foreach (string file in files)
                {
                    if (Path.GetFileName(file).IndexOf(query.Text, StringComparison.OrdinalIgnoreCase) >= 0) results.Items.Add(file);
                }
            };
            results.DoubleClick += delegate
            {
                string path = results.SelectedItem as string;
                if (!String.IsNullOrEmpty(path) && File.Exists(path)) OpenDocument(path);
            };
        }

        private void CollectTreeFiles(TreeNodeCollection nodes, List<string> files)
        {
            foreach (TreeNode node in nodes)
            {
                string path = node.Tag as string;
                if (!String.IsNullOrEmpty(path) && File.Exists(path)) files.Add(path);
                CollectTreeFiles(node.Nodes, files);
            }
        }

        private void BuildSourceControlSidebar()
        {
            FlowLayoutPanel content = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(10), BackColor = Color.FromArgb(15, 23, 42) };
            _sidebarContent.Controls.Add(content);
            Label result = CreateInfoLabel(String.IsNullOrEmpty(_workspaceRoot) ? "Open a folder to inspect Git status." : "Checking repository status...");
            result.MaximumSize = new Size(190, 0);
            content.Controls.Add(result);
            Button refresh = CreateToolbarButton("Refresh Git Status", delegate { RefreshGitStatus(result); });
            refresh.Margin = new Padding(0, 8, 0, 0);
            content.Controls.Add(refresh);
            if (!String.IsNullOrEmpty(_workspaceRoot)) RefreshGitStatus(result);
        }

        private async void RefreshGitStatus(Label target)
        {
            if (String.IsNullOrEmpty(_workspaceRoot)) { target.Text = "Open a folder to inspect Git status."; return; }
            target.Text = "Checking repository status...";
            try
            {
                Tuple<int, string> result = await Task.Run(delegate
                {
                    using (Process process = new Process())
                    {
                        ProcessStartInfo start = new ProcessStartInfo("git", "-C " + Quote(_workspaceRoot) + " status --short --branch");
                        start.UseShellExecute = false;
                        start.CreateNoWindow = true;
                        start.RedirectStandardOutput = true;
                        start.RedirectStandardError = true;
                        process.StartInfo = start;
                        process.Start();
                        string output = process.StandardOutput.ReadToEnd();
                        string error = process.StandardError.ReadToEnd();
                        process.WaitForExit();
                        return Tuple.Create(process.ExitCode, process.ExitCode == 0 ? output : error);
                    }
                });
                target.Text = result.Item1 == 0 ? (String.IsNullOrWhiteSpace(result.Item2) ? "Working tree clean" : Clip(result.Item2.Trim(), 1800)) : "Git status unavailable: " + Clip(result.Item2.Trim(), 300);
            }
            catch (Exception error) { target.Text = "Git status unavailable: " + error.Message; }
        }

        private void BuildRunSidebar()
        {
            Label message = CreateInfoLabel("Run and Debug is not available in this native build. No code is executed by the workbench.");
            message.Dock = DockStyle.Top;
            message.Padding = new Padding(10);
            message.MaximumSize = new Size(220, 0);
            _sidebarContent.Controls.Add(message);
        }

        private void BuildExtensionsSidebar()
        {
            Label message = CreateInfoLabel("This build has no extension host or extension installer. The editor remains limited to its built-in features.");
            message.Dock = DockStyle.Top;
            message.Padding = new Padding(10);
            message.MaximumSize = new Size(220, 0);
            _sidebarContent.Controls.Add(message);
        }

        private void BuildSettingsSidebar()
        {
            FlowLayoutPanel content = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(10), BackColor = Color.FromArgb(15, 23, 42) };
            _sidebarContent.Controls.Add(content);
            content.Controls.Add(CreateInfoLabel("Open Editor settings"));
            AddSettingsNavigationButton(content, "General", "General");
            AddSettingsNavigationButton(content, "Appearance", "Appearance");
            AddSettingsNavigationButton(content, "Editor", "Editor");
            AddSettingsNavigationButton(content, "Explorer", "Explorer");
            AddSettingsNavigationButton(content, "AI Agent", "AI Agent");
            AddSettingsNavigationButton(content, "AI Providers", "AI Providers");
        }

        private void AddSettingsNavigationButton(Control host, string label, string page)
        {
            Button button = CreateToolbarButton(label, delegate { ShowSettingsTab(page); });
            button.Margin = new Padding(0, 6, 0, 0);
            host.Controls.Add(button);
        }

        private void BuildAccountsSidebar()
        {
            FlowLayoutPanel content = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(10), BackColor = Color.FromArgb(15, 23, 42) };
            _sidebarContent.Controls.Add(content);
            content.Controls.Add(CreateInfoLabel(_apiKeySaved ? "An OmniRoute key is saved for this Windows user." : "No provider key is saved."));
        }

        private void RefreshOpenEditors()
        {
            if (_openEditorsList == null || _openEditorsList.IsDisposed) return;
            _openEditorsList.BeginUpdate();
            _openEditorsList.Items.Clear();
            foreach (OpenDocument document in _documents) _openEditorsList.Items.Add(document);
            _openEditorsList.EndUpdate();
        }

        private void AddWelcomeAction(Control host, string text, Action action)
        {
            Button button = CreateToolbarButton("  " + text, action);
            button.AutoSize = false;
            button.TextAlign = ContentAlignment.MiddleLeft;
            button.Width = 280;
            button.Height = 38;
            button.Margin = new Padding(0, 2, 0, 5);
            host.Controls.Add(button);
        }

        private Label CreateInfoLabel(string text)
        {
            return new Label { Text = text, AutoSize = true, MaximumSize = new Size(180, 0), ForeColor = Color.FromArgb(178, 191, 207), Font = new Font("Segoe UI", 9F), Margin = new Padding(0, 4, 0, 4) };
        }

        private void AddInfoDivider(Control host)
        {
            Label divider = new Label { AutoSize = false, Width = 220, Height = 1, BackColor = Color.FromArgb(45, 59, 80), Margin = new Padding(0, 14, 0, 14) };
            host.Controls.Add(divider);
        }

        private void UpdateWelcomeVisibility()
        {
            if (_welcomePanel == null || _welcomePanel.IsDisposed) return;
            bool hasOpenDocuments = _tabs.TabPages.Count > 0;
            _tabs.Visible = hasOpenDocuments;
            _welcomePanel.Visible = !hasOpenDocuments;
            if (hasOpenDocuments) _tabs.BringToFront();
            else
            {
                _welcomePanel.BringToFront();
                _welcomePanel.Invalidate(true);
            }
        }

        private void BuildAssistantPanel(Control host)
        {
            Panel chatHost = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(15, 23, 42) };
            host.Controls.Add(chatHost);
            BuildAssistantChatTab(chatHost);
            BuildProfileWindow();
        }

        private void BuildProfileWindow()
        {
            _profileWindow = new Form();
            _profileWindow.Text = "Open Editor Settings";
            _profileWindow.StartPosition = FormStartPosition.CenterParent;
            _profileWindow.MinimumSize = new Size(650, 560);
            _profileWindow.Size = new Size(760, 700);
            _profileWindow.BackColor = Color.FromArgb(15, 23, 42);
            _profileWindow.ForeColor = ForeColor;
            _settingsTabs = new TabControl { Dock = DockStyle.Fill, BackColor = Color.FromArgb(15, 23, 42), Padding = new Point(12, 4) };
            _profileWindow.Controls.Add(_settingsTabs);
            AddSettingsTab("General", BuildGeneralSettingsPage);
            AddSettingsTab("Appearance", BuildAppearanceSettingsPage);
            AddSettingsTab("Editor", BuildEditorSettingsPage);
            AddSettingsTab("Explorer", BuildExplorerSettingsPage);
            AddSettingsTab("AI Agent", BuildAiAgentSettingsPage);
            TabPage providers = new TabPage("AI Providers") { BackColor = Color.FromArgb(15, 23, 42), ForeColor = ForeColor, UseVisualStyleBackColor = false };
            _settingsTabs.TabPages.Add(providers);
            BuildProfileTab(providers);
            _profileWindow.FormClosing += delegate(object sender, FormClosingEventArgs e)
            {
                if (e.CloseReason == CloseReason.UserClosing && !_isClosing)
                {
                    e.Cancel = true;
                    _profileWindow.Hide();
                }
            };
        }

        private void AddSettingsTab(string name, Action<Control> buildPage)
        {
            TabPage page = new TabPage(name) { BackColor = Color.FromArgb(15, 23, 42), ForeColor = ForeColor, UseVisualStyleBackColor = false };
            _settingsTabs.TabPages.Add(page);
            buildPage(page);
        }

        private FlowLayoutPanel CreateSettingsPage(Control host, string title, string summary)
        {
            FlowLayoutPanel page = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Padding = new Padding(22, 18, 22, 18), BackColor = Color.FromArgb(15, 23, 42) };
            host.Controls.Add(page);
            Label heading = new Label { Text = title, AutoSize = true, ForeColor = Color.FromArgb(237, 245, 255), Font = new Font("Segoe UI", 17F, FontStyle.Bold), Margin = new Padding(0, 0, 0, 5) };
            page.Controls.Add(heading);
            Label description = new Label { Text = summary, AutoSize = true, MaximumSize = new Size(620, 0), ForeColor = Color.FromArgb(156, 169, 187), Font = new Font("Segoe UI", 9.5F), Margin = new Padding(0, 0, 0, 18) };
            page.Controls.Add(description);
            return page;
        }

        private CheckBox AddSettingsCheckBox(Control host, string text, bool initialValue, Action<bool> changed)
        {
            CheckBox check = new CheckBox { Text = text, AutoSize = true, Checked = initialValue, ForeColor = Color.FromArgb(204, 204, 204), BackColor = Color.Transparent, Font = new Font("Segoe UI", 10F), Margin = new Padding(0, 8, 0, 8) };
            check.CheckedChanged += delegate { changed(check.Checked); };
            host.Controls.Add(check);
            return check;
        }

        private NumericUpDown AddSettingsNumber(Control host, string labelText, int minimum, int maximum, int value, Action<int> changed)
        {
            TableLayoutPanel row = new TableLayoutPanel { Width = 600, Height = 42, ColumnCount = 2, RowCount = 1, Margin = new Padding(0, 4, 0, 4), BackColor = Color.Transparent };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112));
            Label label = new Label { Text = labelText, Dock = DockStyle.Fill, ForeColor = Color.FromArgb(204, 204, 204), TextAlign = ContentAlignment.MiddleLeft };
            row.Controls.Add(label, 0, 0);
            NumericUpDown number = new NumericUpDown { Dock = DockStyle.Fill, Minimum = minimum, Maximum = maximum, Value = Math.Max(minimum, Math.Min(maximum, value)), BackColor = Color.FromArgb(30, 30, 30), ForeColor = Color.FromArgb(212, 212, 212), Margin = new Padding(4) };
            number.ValueChanged += delegate { changed((int)number.Value); };
            row.Controls.Add(number, 1, 0);
            host.Controls.Add(row);
            return number;
        }

        private void BuildGeneralSettingsPage(Control host)
        {
            FlowLayoutPanel page = CreateSettingsPage(host, "General", "Application startup and workspace behavior.");
            _autoStartGatewayCheckBox = AddSettingsCheckBox(page, "Start OmniRoute automatically when Open Editor opens", _autoStartGateway, delegate(bool value)
            {
                _autoStartGateway = value;
                SaveUserSettings();
            });
        }

        private void BuildAppearanceSettingsPage(Control host)
        {
            FlowLayoutPanel page = CreateSettingsPage(host, "Appearance", "Customize the active accent color. The native workbench currently uses its built-in dark theme.");
            Button choose = CreateToolbarButton("Choose Accent Color...", delegate
            {
                using (ColorDialog dialog = new ColorDialog { Color = _accentColor, FullOpen = true })
                {
                    if (dialog.ShowDialog(_profileWindow) != DialogResult.OK) return;
                    _accentColor = dialog.Color;
                    _accentPreviewLabel.ForeColor = _accentColor;
                    ApplyAccentColorToControls(this);
                    ApplyAccentColorToControls(_profileWindow);
                    SaveUserSettings();
                }
            });
            choose.Margin = new Padding(0, 4, 0, 10);
            page.Controls.Add(choose);
            _accentPreviewLabel = new Label { Text = "Accent preview", AutoSize = true, ForeColor = _accentColor, Font = new Font("Segoe UI", 10F, FontStyle.Bold), Margin = new Padding(0, 4, 0, 4) };
            page.Controls.Add(_accentPreviewLabel);
        }

        private void BuildEditorSettingsPage(Control host)
        {
            FlowLayoutPanel page = CreateSettingsPage(host, "Editor", "Text appearance for open and newly created files.");
            _editorFontSizeSetting = AddSettingsNumber(page, "Font size", 8, 24, _editorFontSize, delegate(int value)
            {
                _editorFontSize = value;
                ApplyEditorPreferences();
                SaveUserSettings();
            });
            _editorWordWrapCheckBox = AddSettingsCheckBox(page, "Word wrap", _editorWordWrap, delegate(bool value)
            {
                _editorWordWrap = value;
                ApplyEditorPreferences();
                SaveUserSettings();
            });
        }

        private void BuildExplorerSettingsPage(Control host)
        {
            FlowLayoutPanel page = CreateSettingsPage(host, "Explorer", "File visibility options for the current project tree.");
            _showHiddenFilesCheckBox = AddSettingsCheckBox(page, "Show hidden files and folders", _showHiddenFiles, delegate(bool value)
            {
                _showHiddenFiles = value;
                RefreshWorkspaceExplorer();
                SaveUserSettings();
            });
            page.Controls.Add(CreateInfoLabel("Hidden and sensitive files remain excluded from AI workspace tools."));
        }

        private void BuildAiAgentSettingsPage(Control host)
        {
            FlowLayoutPanel page = CreateSettingsPage(host, "AI Agent", "Control active-file context and workspace tool limits.");
            _autoIncludeActiveEditorCheckBox = AddSettingsCheckBox(page, "Automatically include the active editor in chat context", _autoIncludeActiveEditor, delegate(bool value)
            {
                _autoIncludeActiveEditor = value;
                SaveUserSettings();
            });
            _maximumAgentCharactersSetting = AddSettingsNumber(page, "Maximum context characters per file", 1000, 50000, _maximumAgentCharacters, delegate(int value)
            {
                _maximumAgentCharacters = value;
                SaveUserSettings();
            });
            _maximumWorkspaceFilesSetting = AddSettingsNumber(page, "Maximum workspace files per tool operation", 100, 3000, _maximumWorkspaceFiles, delegate(int value)
            {
                _maximumWorkspaceFiles = value;
                SaveUserSettings();
            });
            page.Controls.Add(CreateInfoLabel("AI file edits always require explicit review. Apply & Save is the only AI action that writes approved changes to disk."));
        }

        private void BuildAssistantChatTab(Control host)
        {
            TableLayoutPanel panel = new TableLayoutPanel();
            panel.Dock = DockStyle.Fill;
            panel.Margin = Padding.Empty;
            panel.Padding = Padding.Empty;
            panel.ColumnCount = 1;
            panel.RowCount = 4;
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 116));
            panel.BackColor = Color.FromArgb(15, 23, 42);
            host.Controls.Add(panel);

            TableLayoutPanel header = new TableLayoutPanel();
            header.Dock = DockStyle.Fill;
            header.Padding = new Padding(14, 8, 14, 8);
            header.ColumnCount = 4;
            header.RowCount = 1;
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 58));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 60));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 58));
            header.BackColor = Color.FromArgb(17, 24, 39);
            panel.Controls.Add(header, 0, 0);
            Label title = new Label { Text = "MY OMNI AI", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.FromArgb(237, 245, 255), BackColor = Color.Transparent, Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold) };
            header.Controls.Add(title, 0, 0);
            Button newChatButton = new Button { Text = "New", Dock = DockStyle.Fill, Margin = new Padding(2) };
            StyleButton(newChatButton, false);
            newChatButton.Click += delegate { StartNewChat(); };
            _navigationToolTip.SetToolTip(newChatButton, "Start a new chat");
            header.Controls.Add(newChatButton, 1, 0);
            _retryButton = new Button { Text = "Retry", Dock = DockStyle.Fill, Margin = new Padding(2), Visible = false };
            StyleButton(_retryButton, false);
            _retryButton.Click += async delegate { await RegenerateLastResponseAsync(); };
            _navigationToolTip.SetToolTip(_retryButton, "Regenerate the last response");
            header.Controls.Add(_retryButton, 2, 0);
            _copyButton = new Button { Text = "Copy", Dock = DockStyle.Fill, Margin = new Padding(2), Visible = false };
            StyleButton(_copyButton, false);
            _copyButton.Click += delegate { CopyChatText(); };
            _navigationToolTip.SetToolTip(_copyButton, "Copy selected text, or the whole conversation");
            header.Controls.Add(_copyButton, 3, 0);
            UpdateChatActionVisibility();

            TableLayoutPanel modelBar = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(14, 5, 14, 5), ColumnCount = 2, RowCount = 1, BackColor = Color.FromArgb(17, 24, 39) };
            modelBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 68));
            modelBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 32));
            panel.Controls.Add(modelBar, 0, 1);
            _chatModelSelector.DropDownStyle = ComboBoxStyle.DropDown;
            _chatModelSelector.Items.Add("auto");
            _chatModelSelector.Text = _model;
            StyleComboBox(_chatModelSelector);
            _chatModelSelector.Dock = DockStyle.Fill;
            _chatModelSelector.TextChanged += delegate
            {
                if (String.IsNullOrWhiteSpace(_chatModelSelector.Text)) return;
                _model = _chatModelSelector.Text.Trim();
                if (_modelInput.Text != _model) _modelInput.Text = _model;
                if (_homeModelLabel != null) _homeModelLabel.Text = "Selected model: " + _model;
            };
            modelBar.Controls.Add(_chatModelSelector, 0, 0);
            _chatConnectionStatus.Text = "Disconnected";
            _chatConnectionStatus.TextAlign = ContentAlignment.MiddleRight;
            _chatConnectionStatus.ForeColor = Color.FromArgb(156, 169, 187);
            _chatConnectionStatus.Dock = DockStyle.Fill;
            _navigationToolTip.SetToolTip(_chatConnectionStatus, "OmniRoute gateway and API status");
            modelBar.Controls.Add(_chatConnectionStatus, 1, 0);

            _chatLog.Dock = DockStyle.Fill;
            _chatLog.ReadOnly = true;
            _chatLog.BorderStyle = BorderStyle.None;
            _chatLog.BackColor = Color.FromArgb(10, 15, 25);
            _chatLog.ForeColor = Color.FromArgb(220, 230, 242);
            _chatLog.Font = new Font("Segoe UI", 9.5F);
            _chatLog.DetectUrls = false;
            _chatLog.ContextMenuStrip = CreateChatContextMenu();
            panel.Controls.Add(_chatLog, 0, 2);

            Panel composer = new Panel();
            composer.Dock = DockStyle.Fill;
            composer.Padding = new Padding(14, 10, 14, 10);
            composer.BackColor = Color.FromArgb(17, 24, 39);
            panel.Controls.Add(composer, 0, 3);
            TableLayoutPanel composerLayout = new TableLayoutPanel();
            composerLayout.Dock = DockStyle.Fill;
            composerLayout.ColumnCount = 3;
            composerLayout.RowCount = 1;
            composerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            composerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 78));
            composerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 88));
            composerLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            composer.Controls.Add(composerLayout);
            _chatInput.Multiline = true;
            _chatInput.AcceptsReturn = true;
            _chatInput.ScrollBars = ScrollBars.Vertical;
            StyleInput(_chatInput);
            _chatInput.Dock = DockStyle.Fill;
            _chatInput.KeyDown += OnChatKeyDown;
            composerLayout.Controls.Add(_chatInput, 0, 0);
            Button attachButton = new Button { Text = "Attach", Dock = DockStyle.Fill, Margin = new Padding(4) };
            StyleButton(attachButton, false);
            attachButton.Click += delegate { AttachFileToChat(); };
            composerLayout.Controls.Add(attachButton, 1, 0);
            _sendButton.Text = "Send";
            _sendButton.Dock = DockStyle.Fill;
            StyleButton(_sendButton, true);
            _sendButton.Click += async delegate { await SendChatAsync(); };
            composerLayout.Controls.Add(_sendButton, 2, 0);

        }

        private void StartNewChat()
        {
            if (_busy)
            {
                MessageBox.Show(this, "Stop the current response before starting a new chat.", "Assistant is busy", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (_history.Count > 0 && MessageBox.Show(this, "Clear the current conversation and start a new chat?", "New Chat", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            _history.Clear();
            _lastPrompt = String.Empty;
            _hasAssistantResponse = false;
            UpdateChatActionVisibility();
            _chatLog.Clear();
            _chatInput.Clear();
        }

        private async Task RegenerateLastResponseAsync()
        {
            if (_busy || String.IsNullOrWhiteSpace(_lastPrompt)) return;
            if (_history.Count > 0 && _history[_history.Count - 1].role == "assistant") _history.RemoveAt(_history.Count - 1);
            if (_history.Count > 0 && _history[_history.Count - 1].role == "user") _history.RemoveAt(_history.Count - 1);
            _hasAssistantResponse = false;
            UpdateChatActionVisibility();
            _chatInput.Text = _lastPrompt;
            await SendChatAsync();
        }

        private void UpdateChatActionVisibility()
        {
            bool visible = _hasAssistantResponse && !_busy;
            if (_retryButton != null) _retryButton.Visible = visible;
            if (_copyButton != null) _copyButton.Visible = visible;
            TableLayoutPanel header = _retryButton == null ? null : _retryButton.Parent as TableLayoutPanel;
            if (header != null && header.ColumnStyles.Count >= 4)
            {
                header.ColumnStyles[2].Width = visible ? 54 : 0;
                header.ColumnStyles[3].Width = visible ? 52 : 0;
            }
        }

        private void CopyChatText()
        {
            string text = _chatLog.SelectionLength > 0 ? _chatLog.SelectedText : _chatLog.Text;
            if (String.IsNullOrEmpty(text)) return;
            try { Clipboard.SetText(text); }
            catch (Exception error) { MessageBox.Show(this, error.Message, "Could not copy chat text", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        private void AttachFileToChat()
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "Attach a text file to the next message";
                dialog.Filter = "All files|*.*";
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    FileInfo info = new FileInfo(dialog.FileName);
                    if (info.Length > MaxFileBytes) throw new InvalidOperationException("Attachments are limited to 512 KB.");
                    byte[] bytes = File.ReadAllBytes(dialog.FileName);
                    if (Array.IndexOf(bytes, (byte)0) >= 0) throw new InvalidOperationException("Binary files cannot be attached as text.");
                    string content = File.ReadAllText(dialog.FileName, Encoding.UTF8);
                    if (content.Length > _maximumAgentCharacters) content = content.Substring(0, _maximumAgentCharacters) + "\r\n[Attachment truncated.]";
                    _chatInput.AppendText((String.IsNullOrWhiteSpace(_chatInput.Text) ? String.Empty : "\r\n\r\n") + "Attached file: " + Path.GetFileName(dialog.FileName) + "\r\n```text\r\n" + content + "\r\n```");
                    _chatInput.Focus();
                }
                catch (Exception error) { MessageBox.Show(this, error.Message, "Could not attach file", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            }
        }

        private void BuildProfileTab(Control host)
        {
            TableLayoutPanel layout = new TableLayoutPanel();
            layout.Dock = DockStyle.Fill;
            layout.Padding = new Padding(12, 8, 12, 12);
            layout.ColumnCount = 1;
            layout.RowCount = 5;
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 128));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 216));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.BackColor = Color.FromArgb(15, 23, 42);
            host.Controls.Add(layout);

            Label heading = MakeHeading("OMNIROUTE CONNECTION");
            heading.Dock = DockStyle.Fill;
            heading.Padding = new Padding(0, 10, 0, 0);
            layout.Controls.Add(heading, 0, 0);

            _connectionLabel.Text = "Connect to OmniRoute to start chatting.";
            _connectionLabel.ForeColor = Color.FromArgb(156, 169, 187);
            _connectionLabel.AutoEllipsis = true;
            _connectionLabel.Dock = DockStyle.Fill;
            _connectionLabel.Padding = new Padding(10, 8, 8, 4);
            _connectionLabel.BackColor = Color.FromArgb(17, 24, 39);
            layout.Controls.Add(_connectionLabel, 0, 1);

            GroupBox gatewayGroup = new GroupBox { Text = "LOCAL OMNIROUTE GATEWAY", Dock = DockStyle.Fill, ForeColor = Color.FromArgb(190, 205, 222), Padding = new Padding(10, 8, 10, 6) };
            layout.Controls.Add(gatewayGroup, 0, 2);
            TableLayoutPanel gatewayLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Padding = new Padding(4) };
            gatewayLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            gatewayLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            gatewayGroup.Controls.Add(gatewayLayout);
            Label endpoint = new Label { Text = "Endpoint   http://127.0.0.1:20128", Dock = DockStyle.Fill, ForeColor = Color.FromArgb(156, 169, 187), TextAlign = ContentAlignment.MiddleLeft };
            gatewayLayout.Controls.Add(endpoint, 0, 0);
            FlowLayoutPanel gatewayButtons = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Padding = Padding.Empty, Margin = Padding.Empty };
            gatewayLayout.Controls.Add(gatewayButtons, 0, 1);
            _installButton.Text = "Install / Start";
            _installButton.Width = 126;
            _installButton.Height = 32;
            StyleButton(_installButton, true);
            _installButton.Click += async delegate { await InstallOrStartGatewayAsync(); };
            gatewayButtons.Controls.Add(_installButton);
            Button dashboardButton = new Button { Text = "Open Dashboard", Width = 126, Height = 32, Margin = new Padding(8, 3, 3, 3) };
            StyleButton(dashboardButton, false);
            dashboardButton.Click += delegate { OpenDashboard(); };
            gatewayButtons.Controls.Add(dashboardButton);

            GroupBox apiGroup = new GroupBox { Text = "OMNIROUTE API", Dock = DockStyle.Fill, ForeColor = Color.FromArgb(190, 205, 222), Padding = new Padding(10, 8, 10, 6) };
            layout.Controls.Add(apiGroup, 0, 3);
            TableLayoutPanel apiLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 6, Padding = new Padding(4) };
            apiLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));
            apiLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            apiLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));
            apiLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            apiLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 66));
            apiLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            apiGroup.Controls.Add(apiLayout);
            Label keyLabel = new Label { Text = "API key", Dock = DockStyle.Fill, ForeColor = Color.FromArgb(156, 169, 187), TextAlign = ContentAlignment.MiddleLeft };
            apiLayout.Controls.Add(keyLabel, 0, 0);
            _apiKeyInput.UseSystemPasswordChar = true;
            StyleInput(_apiKeyInput);
            _apiKeyInput.Dock = DockStyle.Fill;
            _apiKeyInput.TextChanged += delegate
            {
                if (!String.IsNullOrEmpty(_apiKeyInput.Text))
                    _apiKeyStatus.Text = "New key entered. Click Connect to validate and save it.";
                else
                    _apiKeyStatus.Text = _apiKeySaved
                        ? "A key is saved securely. Leave blank to keep it, or enter a replacement."
                        : "No API key saved.";
            };
            apiLayout.Controls.Add(_apiKeyInput, 0, 1);
            Label modelLabel = new Label { Text = "Model", Dock = DockStyle.Fill, ForeColor = Color.FromArgb(156, 169, 187), TextAlign = ContentAlignment.MiddleLeft };
            apiLayout.Controls.Add(modelLabel, 0, 2);
            _modelInput.Text = "auto";
            _modelInput.DropDownStyle = ComboBoxStyle.DropDown;
            _modelInput.Items.Add("auto");
            StyleComboBox(_modelInput);
            _modelInput.Dock = DockStyle.Fill;
            _modelInput.TextChanged += delegate
            {
                if (!String.IsNullOrWhiteSpace(_modelInput.Text)) _model = _modelInput.Text.Trim();
                if (_homeModelLabel != null) _homeModelLabel.Text = "Selected model: " + _model;
                if (_chatModelSelector.Text != _modelInput.Text) _chatModelSelector.Text = _modelInput.Text;
            };
            apiLayout.Controls.Add(_modelInput, 0, 3);
            FlowLayoutPanel apiButtons = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = true, Padding = Padding.Empty, Margin = Padding.Empty };
            apiLayout.Controls.Add(apiButtons, 0, 4);
            _connectButton.Text = "Connect";
            _connectButton.Width = 100;
            _connectButton.Height = 32;
            StyleButton(_connectButton, true);
            _connectButton.Click += async delegate { await ConnectAsync(); };
            apiButtons.Controls.Add(_connectButton);
            _testConnectionButton.Text = "Test Connection";
            _testConnectionButton.Width = 118;
            _testConnectionButton.Height = 32;
            _testConnectionButton.Margin = new Padding(4, 3, 3, 3);
            StyleButton(_testConnectionButton, false);
            _testConnectionButton.Click += async delegate { await TestConnectionAsync(); };
            apiButtons.Controls.Add(_testConnectionButton);
            Button forgetButton = new Button { Text = "Forget Key", Width = 100, Height = 32, Margin = new Padding(8, 3, 3, 3) };
            StyleButton(forgetButton, false);
            forgetButton.Click += delegate { ForgetApiKey(); };
            apiButtons.Controls.Add(forgetButton);
            _apiKeyStatus.Text = "API keys are protected for this Windows user.";
            _apiKeyStatus.Dock = DockStyle.Fill;
            _apiKeyStatus.ForeColor = Color.FromArgb(125, 145, 164);
            _apiKeyStatus.TextAlign = ContentAlignment.MiddleLeft;
            apiLayout.Controls.Add(_apiKeyStatus, 0, 5);
        }

        private void OnAssistantTabDrawItem(object sender, DrawItemEventArgs e)
        {
            TabControl tabs = sender as TabControl;
            if (tabs == null || e.Index < 0 || e.Index >= tabs.TabPages.Count) return;
            bool selected = e.Index == tabs.SelectedIndex;
            using (SolidBrush background = new SolidBrush(selected ? Color.FromArgb(23, 35, 53) : Color.FromArgb(15, 23, 42)))
                e.Graphics.FillRectangle(background, e.Bounds);
            Color textColor = selected ? Color.FromArgb(125, 211, 252) : Color.FromArgb(156, 169, 187);
            TextRenderer.DrawText(e.Graphics, tabs.TabPages[e.Index].Text, tabs.Font, e.Bounds, textColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        private void ShowProfile()
        {
            ShowSettingsTab("AI Providers");
        }

        private void ShowSettingsTab(string pageName)
        {
            if (_profileWindow == null || _profileWindow.IsDisposed) return;
            if (_settingsTabs != null)
            {
                foreach (TabPage page in _settingsTabs.TabPages)
                {
                    if (String.Equals(page.Text, pageName, StringComparison.OrdinalIgnoreCase))
                    {
                        _settingsTabs.SelectedTab = page;
                        break;
                    }
                }
            }
            if (!_profileWindow.Visible) _profileWindow.Show(this);
            else _profileWindow.Activate();
        }

        private void ApplyEditorPreferences()
        {
            foreach (OpenDocument document in _documents)
            {
                document.Editor.Font = new Font("Consolas", _editorFontSize);
                document.Editor.WordWrap = _editorWordWrap;
                if (document.Gutter != null) document.Gutter.Invalidate();
            }
        }

        private void RunEditorCommand(Action<RichTextBox> command)
        {
            OpenDocument document = ActiveDocument();
            if (document != null) command(document.Editor);
        }

        private void SaveAllDocuments()
        {
            TabPage previous = _tabs.SelectedTab;
            foreach (OpenDocument document in _documents)
            {
                if (!document.Dirty) continue;
                foreach (TabPage page in _tabs.TabPages)
                {
                    if (page.Tag == document) { _tabs.SelectedTab = page; break; }
                }
                SaveActiveDocument();
                if (document.Dirty) break;
            }
            if (previous != null && _tabs.TabPages.Contains(previous)) _tabs.SelectedTab = previous;
        }

        private void ForgetApiKey()
        {
            if (!File.Exists(_keyPath))
            {
                _connectionLabel.Text = "No saved API key to remove.";
                return;
            }
            DialogResult result = MessageBox.Show(this, "Remove the saved OmniRoute API key from this Windows user profile?", "Forget API Key", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (result != DialogResult.Yes) return;
            try
            {
                File.Delete(_keyPath);
                _apiKeySaved = false;
                _apiKeyInput.Clear();
                _connected = false;
                _connectionLabel.Text = "Saved API key removed. Enter a key to connect.";
                _apiKeyStatus.Text = "No API key saved.";
                _gatewayStatus.Text = "Gateway online · API key needed";
            }
            catch (Exception error) { MessageBox.Show(this, error.Message, "Could not remove API key", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        private Label MakeHeading(string text)
        {
            return new Label { Text = text, ForeColor = Color.FromArgb(156, 169, 187), BackColor = Color.Transparent, Padding = new Padding(12, 10, 0, 0), Font = new Font("Segoe UI", 8.5F, FontStyle.Bold) };
        }

        private void AddToolbarButton(FlowLayoutPanel toolbar, string text, Action action)
        {
            toolbar.Controls.Add(CreateToolbarButton(text, action));
        }

        private Button CreateToolbarButton(string text, Action action)
        {
            Button button = new Button();
            button.Text = text;
            button.AutoSize = true;
            button.Height = 32;
            button.Margin = new Padding(3, 0, 5, 0);
            StyleButton(button, text == "Install / Start OmniRoute");
            button.Click += delegate { action(); };
            return button;
        }

        private void AddToolbarButton(FlowLayoutPanel toolbar, string text, Func<Task> action)
        {
            Button button = new Button();
            button.Text = text;
            button.AutoSize = true;
            button.Height = 32;
            button.Margin = new Padding(3, 0, 5, 0);
            StyleButton(button, true);
            button.Click += async delegate { await action(); };
            toolbar.Controls.Add(button);
        }

        private void StyleButton(Button button, bool primary)
        {
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 1;
            button.Height = Math.Max(button.Height, 30);
            Color normalBackground = primary ? _accentColor : Color.FromArgb(23, 35, 53);
            Color hoverBackground = primary ? Color.FromArgb(149, 221, 255) : Color.FromArgb(31, 48, 70);
            button.MouseEnter += delegate { button.BackColor = hoverBackground; };
            button.MouseLeave += delegate { button.BackColor = normalBackground; };
            button.AccessibleDescription = primary ? "OpenEditorPrimary" : "OpenEditorSecondary";
            button.FlatAppearance.BorderColor = primary ? _accentColor : Color.FromArgb(45, 59, 80);
            button.BackColor = primary ? _accentColor : Color.FromArgb(23, 35, 53);
            button.ForeColor = primary ? Color.FromArgb(8, 34, 54) : Color.FromArgb(237, 245, 255);
            button.Cursor = Cursors.Hand;
        }

        private void StyleInput(TextBox input)
        {
            input.BorderStyle = BorderStyle.FixedSingle;
            input.Font = new Font("Segoe UI", 9.5F);
            input.BackColor = Color.FromArgb(10, 15, 25);
            input.ForeColor = Color.FromArgb(237, 245, 255);
            input.Margin = new Padding(0, 3, 0, 3);
        }

        private void StyleComboBox(ComboBox input)
        {
            input.FlatStyle = FlatStyle.Flat;
            input.BackColor = Color.FromArgb(10, 15, 25);
            input.ForeColor = Color.FromArgb(237, 245, 255);
            input.Font = new Font("Segoe UI", 9F);
            input.Margin = new Padding(0, 3, 0, 3);
        }

        private void ToggleAssistant()
        {
            if (_editorSplit == null) return;
            SetAssistantVisible(_editorSplit.Panel2Collapsed);
        }

        private void SetAssistantVisible(bool visible)
        {
            if (_editorSplit == null) return;
            if (visible)
            {
                _editorSplit.Panel2Collapsed = false;
                _editorSplit.Panel2MinSize = 280;
                int available = _editorSplit.ClientSize.Width - _editorSplit.SplitterWidth;
                _chatAnimationTarget = Math.Max(280, Math.Min(420, available - 700));
                if (_chatAnimationTarget < 280) _chatAnimationTarget = 280;
                _chatAnimationTimer.Start();
            }
            else
            {
                _editorSplit.Panel2MinSize = 0;
                _chatAnimationTarget = 48;
                _chatAnimationTimer.Start();
            }
            _aiSidebarMenu.Checked = visible;
        }

        private void AnimateAssistantPanel(object sender, EventArgs e)
        {
            if (_editorSplit == null || _editorSplit.IsDisposed)
            {
                _chatAnimationTimer.Stop();
                return;
            }
            int distance = _editorSplit.SplitterDistance;
            int difference = _chatAnimationTarget - distance;
            if (Math.Abs(difference) <= 12)
            {
                _editorSplit.SplitterDistance = _chatAnimationTarget;
                _chatAnimationTimer.Stop();
                if (_chatAnimationTarget == 48)
                {
                    _editorSplit.Panel2Collapsed = true;
                    _editorSplit.Panel2MinSize = 280;
                }
                return;
            }
            _editorSplit.SplitterDistance = distance + difference / 4;
        }

        private void ToggleWorkspaceSidebar()
        {
            if (_workspaceSplit == null) return;
            SetWorkspaceSidebarVisible(_workspaceSplit.Panel1Collapsed);
        }

        private void SetWorkspaceSidebarVisible(bool visible)
        {
            if (_workspaceSplit == null) return;
            if (visible)
            {
                _workspaceSplit.Panel1Collapsed = false;
                _workspaceSplit.Panel1MinSize = 0;
                _sidebarAnimationTarget = 260;
            }
            else
            {
                _workspaceSplit.Panel1MinSize = 0;
                _sidebarAnimationTarget = 48;
            }
            _sidebarAnimationTimer.Start();
        }

        private void AnimateWorkspaceSidebar(object sender, EventArgs e)
        {
            if (_workspaceSplit == null || _workspaceSplit.IsDisposed)
            {
                _sidebarAnimationTimer.Stop();
                return;
            }
            int distance = _workspaceSplit.SplitterDistance;
            int difference = _sidebarAnimationTarget - distance;
            if (Math.Abs(difference) <= 10)
            {
                _workspaceSplit.SplitterDistance = _sidebarAnimationTarget;
                _sidebarAnimationTimer.Stop();
                if (_sidebarAnimationTarget == 48) _workspaceSplit.Panel1Collapsed = true;
                else _workspaceSplit.Panel1MinSize = 160;
                return;
            }
            _workspaceSplit.SplitterDistance = distance + difference / 4;
        }

        private void ShowAssistantAndFocusKey()
        {
            ShowProfile();
            BeginInvoke(new Action(delegate { _apiKeyInput.Focus(); }));
        }

        private void ShowProfileAndFocusKey()
        {
            ShowProfile();
            BeginInvoke(new Action(delegate { _apiKeyInput.Focus(); }));
        }

        private void OnFormKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control && e.Shift && e.KeyCode == Keys.P)
                ShowCommandPalette();
            else if (e.Control && e.Shift && e.KeyCode == Keys.O)
                OpenWorkspace();
            else if (e.Control && e.Shift && e.KeyCode == Keys.S)
                SaveAsActiveDocument();
            else if (e.Control && e.Shift && e.KeyCode == Keys.I)
                ToggleAssistant();
            else if (e.Control && e.KeyCode == Keys.B)
                ToggleWorkspaceSidebar();
            else if (e.Control && e.KeyCode == Keys.N)
            {
                NewFile();
            }
            else if (e.Control && e.KeyCode == Keys.S)
            {
                SaveActiveDocument();
            }
            else if (e.Control && (e.KeyCode == Keys.O || e.KeyCode == Keys.P))
            {
                OpenFileDialog();
            }
            else return;
            e.Handled = true;
            e.SuppressKeyPress = true;
        }

        private void ShowCommandPalette()
        {
            List<PaletteCommand> commands = new List<PaletteCommand>
            {
                new PaletteCommand { Name = "New File", Execute = delegate { NewFile(); } },
                new PaletteCommand { Name = "Open File", Execute = delegate { OpenFileDialog(); } },
                new PaletteCommand { Name = "Open Folder", Execute = delegate { OpenWorkspace(); } },
                new PaletteCommand { Name = "Save", Execute = delegate { SaveActiveDocument(); } },
                new PaletteCommand { Name = "Save As", Execute = delegate { SaveAsActiveDocument(); } },
                new PaletteCommand { Name = "Save All", Execute = delegate { SaveAllDocuments(); } },
                new PaletteCommand { Name = "Toggle AI Chat", Execute = delegate { ToggleAssistant(); } },
                new PaletteCommand { Name = "Settings: General", Execute = delegate { ShowSettingsTab("General"); } },
                new PaletteCommand { Name = "Settings: Appearance", Execute = delegate { ShowSettingsTab("Appearance"); } },
                new PaletteCommand { Name = "Settings: Editor", Execute = delegate { ShowSettingsTab("Editor"); } },
                new PaletteCommand { Name = "Settings: Explorer", Execute = delegate { ShowSettingsTab("Explorer"); } },
                new PaletteCommand { Name = "Settings: AI Agent", Execute = delegate { ShowSettingsTab("AI Agent"); } },
                new PaletteCommand { Name = "Settings: AI Providers", Execute = delegate { ShowSettingsTab("AI Providers"); } },
                new PaletteCommand { Name = "Open OmniRoute Settings", Execute = delegate { ShowProfile(); } },
                new PaletteCommand { Name = "Install / Start OmniRoute", Execute = async delegate { ShowProfile(); await InstallOrStartGatewayAsync(); } },
                new PaletteCommand { Name = "Open OmniRoute Dashboard", Execute = delegate { OpenDashboard(); } },
                new PaletteCommand { Name = "Go to Line", Execute = delegate { GoToLine(); } }
            };

            using (Form dialog = new Form())
            using (TextBox query = new TextBox())
            using (ListBox results = new ListBox())
            {
                dialog.Text = "Command Palette";
                dialog.StartPosition = FormStartPosition.CenterParent;
                dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                dialog.MinimizeBox = false;
                dialog.MaximizeBox = false;
                dialog.ShowInTaskbar = false;
                dialog.KeyPreview = true;
                dialog.ClientSize = new Size(620, 420);
                dialog.BackColor = Color.FromArgb(30, 30, 30);
                dialog.ForeColor = Color.FromArgb(212, 212, 212);

                TableLayoutPanel layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 1, RowCount = 2, BackColor = dialog.BackColor };
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
                layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                dialog.Controls.Add(layout);
                query.Dock = DockStyle.Fill;
                query.Font = new Font("Segoe UI", 11F);
                StyleInput(query);
                layout.Controls.Add(query, 0, 0);
                results.Dock = DockStyle.Fill;
                results.IntegralHeight = false;
                results.BorderStyle = BorderStyle.None;
                results.BackColor = Color.FromArgb(37, 37, 38);
                results.ForeColor = Color.FromArgb(204, 204, 204);
                results.Font = new Font("Segoe UI", 10F);
                layout.Controls.Add(results, 0, 1);

                Action refreshResults = delegate
                {
                    string filter = query.Text.Trim();
                    results.BeginUpdate();
                    results.Items.Clear();
                    foreach (PaletteCommand command in commands)
                        if (String.IsNullOrEmpty(filter) || command.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                            results.Items.Add(command);
                    results.EndUpdate();
                    if (results.Items.Count > 0) results.SelectedIndex = 0;
                };
                query.TextChanged += delegate { refreshResults(); };
                query.KeyDown += delegate(object sender, KeyEventArgs e)
                {
                    if (e.KeyCode == Keys.Enter)
                    {
                        ExecutePaletteCommand(dialog, results);
                        e.SuppressKeyPress = true;
                    }
                    else if (e.KeyCode == Keys.Down && results.Items.Count > 0)
                    {
                        results.Focus();
                        e.SuppressKeyPress = true;
                    }
                    else if (e.KeyCode == Keys.Escape) dialog.Close();
                };
                results.DoubleClick += delegate { ExecutePaletteCommand(dialog, results); };
                results.KeyDown += delegate(object sender, KeyEventArgs e)
                {
                    if (e.KeyCode == Keys.Enter) ExecutePaletteCommand(dialog, results);
                    else if (e.KeyCode == Keys.Escape) dialog.Close();
                };
                dialog.Shown += delegate { refreshResults(); query.Focus(); };
                dialog.ShowDialog(this);
            }
            }

        private void ExecutePaletteCommand(Form dialog, ListBox results)
        {
            PaletteCommand command = results.SelectedItem as PaletteCommand;
            if (command == null) return;
            dialog.Close();
            BeginInvoke(new Action(delegate { command.Execute(); }));
        }

        private void GoToLine()
        {
            OpenDocument document = ActiveDocument();
            if (document == null) return;
            string value = PromptForText("Go to Line", "Line number (1-" + Math.Max(1, document.Editor.Lines.Length) + ")", String.Empty);
            int requested;
            if (value == null) return;
            if (!Int32.TryParse(value, out requested) || requested < 1 || requested > document.Editor.Lines.Length)
            {
                MessageBox.Show(this, "Enter a valid line number for the active file.", "Go to Line", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            int index = document.Editor.GetFirstCharIndexFromLine(requested - 1);
            document.Editor.SelectionStart = Math.Max(0, index);
            document.Editor.SelectionLength = 0;
            document.Editor.ScrollToCaret();
            document.Editor.Focus();
        }

        private async void OnChatKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter && !e.Shift)
            {
                e.SuppressKeyPress = true;
                await SendChatAsync();
            }
        }

        private void OpenWorkspace()
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Choose a project folder";
                dialog.ShowNewFolderButton = false;
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                LoadWorkspace(dialog.SelectedPath);
            }
            }

        private void LoadWorkspace(string path)
        {
            _workspaceRoot = Path.GetFullPath(path);
            if (!Directory.Exists(_workspaceRoot)) throw new DirectoryNotFoundException("The selected project folder no longer exists.");
            _workspaceStatus.Text = Path.GetFileName(_workspaceRoot);
            _workspaceBytes = 0;
            _workspaceFileCount = 0;
            _tree.BeginUpdate();
            _tree.Nodes.Clear();
            TreeNode root = new TreeNode(Path.GetFileName(_workspaceRoot)) { Tag = _workspaceRoot, ImageKey = "folder", SelectedImageKey = "folder" };
            _tree.Nodes.Add(root);
            PopulateTree(root, _workspaceRoot);
            root.Expand();
            _tree.EndUpdate();
            AddRecentProject(_workspaceRoot);
            SetWorkspaceSidebarVisible(true);
            RefreshBranchStatus();
            if (_sidebarTitle.Text == "EXPLORER") SwitchSidebarView("Explorer");
            _fileStatus.Text = String.Format("{0} text files loaded", _workspaceFileCount);
        }

        private void AddRecentProject(string path)
        {
            string fullPath = Path.GetFullPath(path);
            _recentProjects.RemoveAll(delegate(RecentProject project) { return String.Equals(project.Path, fullPath, StringComparison.OrdinalIgnoreCase); });
            _recentProjects.Insert(0, new RecentProject { Name = Path.GetFileName(fullPath), Path = fullPath, LastOpened = DateTime.Now.ToString("o") });
            if (_recentProjects.Count > 12) _recentProjects.RemoveRange(12, _recentProjects.Count - 12);
            RefreshWelcomeRecents();
            try { PersistSettings(); }
            catch (Exception error) { MessageBox.Show(this, "The project opened, but its recent-project entry could not be saved.\r\n\r\n" + error.Message, "Could not save recent project", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        }

        private void RefreshWelcomeRecents()
        {
            if (_welcomeRecentPanel == null || _welcomeRecentPanel.IsDisposed) return;
            _welcomeRecentPanel.SuspendLayout();
            _welcomeRecentPanel.Controls.Clear();
            int rowWidth = Math.Max(240, _welcomeRecentPanel.ClientSize.Width - 4);
            int detailsWidth = Math.Max(100, rowWidth - 135);
            foreach (RecentProject project in _recentProjects)
            {
                bool exists = !String.IsNullOrEmpty(project.Path) && Directory.Exists(project.Path);
                DateTime lastOpened;
                string openedText = DateTime.TryParse(project.LastOpened, out lastOpened) ? lastOpened.ToLocalTime().ToString("g") : "Unknown";
                FlowLayoutPanel row = new FlowLayoutPanel { Width = rowWidth, Height = 66, WrapContents = false, Margin = new Padding(0, 2, 0, 3), Padding = new Padding(0, 2, 0, 2), BackColor = Color.FromArgb(14, 21, 33) };
                Label details = new Label { Text = project.Name + "\r\n" + project.Path + "\r\nLast opened " + openedText, Width = detailsWidth, Height = 60, AutoEllipsis = true, ForeColor = exists ? Color.FromArgb(196, 209, 224) : Color.FromArgb(128, 138, 152), Font = new Font("Segoe UI", 8.5F), Margin = new Padding(5, 1, 4, 1) };
                row.Controls.Add(details);
                Button open = new Button { Text = exists ? "Open" : "Unavailable", Width = 105, Height = 32, Enabled = exists, Margin = new Padding(4, 14, 2, 4) };
                StyleButton(open, exists);
                open.Click += delegate
                {
                    try { LoadWorkspace(project.Path); }
                    catch (Exception error) { MessageBox.Show(this, error.Message, "Could not open project", MessageBoxButtons.OK, MessageBoxIcon.Error); }
                };
                row.Controls.Add(open);
                _welcomeRecentPanel.Controls.Add(row);
            }
            _welcomeRecentPanel.Height = _welcomeRecentPanel.Controls.Count * 72;
            _welcomeRecentPanel.ResumeLayout();
        }

        private void RefreshBranchStatus()
        {
            _branchStatus.Text = "No repository";
            if (String.IsNullOrEmpty(_workspaceRoot)) return;
            try
            {
                using (Process process = new Process())
                {
                    ProcessStartInfo start = new ProcessStartInfo("git", "-C " + Quote(_workspaceRoot) + " rev-parse --abbrev-ref HEAD");
                    start.UseShellExecute = false;
                    start.CreateNoWindow = true;
                    start.RedirectStandardOutput = true;
                    start.RedirectStandardError = true;
                    process.StartInfo = start;
                    process.Start();
                    string branch = process.StandardOutput.ReadToEnd().Trim();
                    process.WaitForExit();
                    if (process.ExitCode == 0 && !String.IsNullOrEmpty(branch)) _branchStatus.Text = "Git: " + branch;
                }
            }
            catch { _branchStatus.Text = "Git unavailable"; }
        }

        private void OpenExternalLink(string url)
        {
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
            catch (Exception error) { MessageBox.Show(this, error.Message, "Could not open link", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        private void ShowKeyboardShortcuts()
        {
            MessageBox.Show(this, "Ctrl+N  New File\r\nCtrl+O  Open File\r\nCtrl+Shift+O  Open Folder\r\nCtrl+S  Save\r\nCtrl+Shift+S  Save As\r\nCtrl+Shift+P  Command Palette\r\nCtrl+Shift+I  Toggle AI Chat", "Keyboard Shortcuts", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void ShowGettingStarted()
        {
            MessageBox.Show(this, "Open a folder or clone a Git repository to begin. Open a file from Explorer, edit it in a tab, and use Ctrl+S to save. Connect OmniRoute from Settings > AI Providers > OmniRoute to use the assistant.", "Getting Started", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void ShowIssueTrackerStatus()
        {
            MessageBox.Show(this, "This build does not have a configured issue-tracker destination. Visit the project maintainer's release page to find the current support channel.", "Report an Issue", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void NewFile()
        {
            OpenDocument document = CreateEditorDocument(String.Empty, String.Empty, true);
            document.Name = "Untitled-" + (++_untitledCount);
            UpdateTabTitle(document);
            UpdateWelcomeVisibility();
            document.Editor.Focus();
        }

        private async void CloneRepository()
        {
            string repositoryUrl = PromptForRepositoryUrl();
            if (String.IsNullOrWhiteSpace(repositoryUrl)) return;
            Uri repositoryUri;
            if (Uri.TryCreate(repositoryUrl, UriKind.Absolute, out repositoryUri) && !String.IsNullOrEmpty(repositoryUri.UserInfo))
            {
                MessageBox.Show(this, "Do not include credentials in the repository URL. Use Git Credential Manager or your SSH agent instead.", "Credentials in URL", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (repositoryUrl.IndexOf('"') >= 0 || repositoryUrl.IndexOf('\r') >= 0 || repositoryUrl.IndexOf('\n') >= 0)
            {
                MessageBox.Show(this, "The repository URL contains unsupported characters.", "Invalid repository URL", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string parentDirectory;
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Choose where to create the cloned repository";
                dialog.ShowNewFolderButton = true;
                if (!String.IsNullOrEmpty(_workspaceRoot) && Directory.Exists(_workspaceRoot)) dialog.SelectedPath = _workspaceRoot;
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                parentDirectory = dialog.SelectedPath;
            }

            Cursor previousCursor = Cursor;
            Cursor = Cursors.WaitCursor;
            _fileStatus.Text = "Cloning repository...";
            try
            {
                Tuple<int, string> result = await Task.Run(delegate
                {
                    using (Process process = new Process())
                    {
                        ProcessStartInfo start = new ProcessStartInfo("git", "clone -- " + Quote(repositoryUrl));
                        start.WorkingDirectory = parentDirectory;
                        start.UseShellExecute = false;
                        start.CreateNoWindow = true;
                        start.RedirectStandardOutput = true;
                        start.RedirectStandardError = true;
                        start.EnvironmentVariables["GIT_TERMINAL_PROMPT"] = "0";
                        process.StartInfo = start;
                        StringBuilder output = new StringBuilder();
                        object outputLock = new object();
                        DataReceivedEventHandler capture = delegate(object sender, DataReceivedEventArgs e)
                        {
                            if (String.IsNullOrEmpty(e.Data)) return;
                            lock (outputLock)
                            {
                                if (output.Length < 12000) output.AppendLine(e.Data);
                            }
                        };
                        process.OutputDataReceived += capture;
                        process.ErrorDataReceived += capture;
                        process.Start();
                        process.BeginOutputReadLine();
                        process.BeginErrorReadLine();
                        process.WaitForExit();
                        process.WaitForExit();
                        lock (outputLock) return Tuple.Create(process.ExitCode, output.ToString());
                    }
                });
                if (result.Item1 != 0)
                {
                    _fileStatus.Text = "Git clone failed";
                    MessageBox.Show(this, "Git exited with code " + result.Item1 + ".\r\n\r\n" + Clip(result.Item2, 12000), "Could not clone repository", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                string directoryName = RepositoryDirectoryName(repositoryUrl);
                string clonedPath = Path.Combine(parentDirectory, directoryName);
                if (!Directory.Exists(clonedPath))
                {
                    _fileStatus.Text = "Repository cloned";
                    MessageBox.Show(this, "The repository was cloned successfully to:\r\n" + parentDirectory, "Repository cloned", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                LoadWorkspace(clonedPath);
                MessageBox.Show(this, "Repository cloned and opened:\r\n" + clonedPath, "Repository cloned", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception error)
            {
                _fileStatus.Text = "Git clone failed";
                MessageBox.Show(this, error.Message, "Could not clone repository", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                Cursor = previousCursor;
            }
        }

        private string PromptForRepositoryUrl()
        {
            using (Form dialog = new Form())
            using (TextBox input = new TextBox())
            using (Button clone = new Button())
            using (Button cancel = new Button())
            {
                dialog.Text = "Clone Git Repository";
                dialog.StartPosition = FormStartPosition.CenterParent;
                dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                dialog.MinimizeBox = false;
                dialog.MaximizeBox = false;
                dialog.ShowInTaskbar = false;
                dialog.ClientSize = new Size(520, 134);
                dialog.BackColor = Color.FromArgb(15, 23, 42);
                dialog.ForeColor = ForeColor;
                Label label = new Label { Text = "Repository URL", Left = 18, Top = 16, Width = 480, Height = 22, ForeColor = ForeColor };
                input.Left = 18;
                input.Top = 43;
                input.Width = 484;
                StyleInput(input);
                clone.Text = "Clone";
                clone.Left = 326;
                clone.Top = 88;
                clone.Width = 82;
                clone.DialogResult = DialogResult.OK;
                StyleButton(clone, true);
                cancel.Text = "Cancel";
                cancel.Left = 416;
                cancel.Top = 88;
                cancel.Width = 86;
                cancel.DialogResult = DialogResult.Cancel;
                StyleButton(cancel, false);
                dialog.Controls.Add(label);
                dialog.Controls.Add(input);
                dialog.Controls.Add(clone);
                dialog.Controls.Add(cancel);
                dialog.AcceptButton = clone;
                dialog.CancelButton = cancel;
                return dialog.ShowDialog(this) == DialogResult.OK ? input.Text.Trim() : null;
            }
        }

        private string RepositoryDirectoryName(string repositoryUrl)
        {
            string value = repositoryUrl.TrimEnd('/', '\\');
            int separator = Math.Max(value.LastIndexOf('/'), value.LastIndexOf(':'));
            string name = separator >= 0 ? value.Substring(separator + 1) : value;
            if (name.EndsWith(".git", StringComparison.OrdinalIgnoreCase)) name = name.Substring(0, name.Length - 4);
            return String.IsNullOrWhiteSpace(name) ? "repository" : name;
        }

        private void PopulateTree(TreeNode parent, string directory)
        {
            if (_workspaceFileCount >= _maximumWorkspaceFiles || _workspaceBytes >= MaxWorkspaceBytes) return;
            string[] entries;
            try { entries = Directory.GetFileSystemEntries(directory); }
            catch { return; }
            Array.Sort(entries, StringComparer.OrdinalIgnoreCase);
            foreach (string entry in entries)
            {
                if (_workspaceFileCount >= _maximumWorkspaceFiles || _workspaceBytes >= MaxWorkspaceBytes) break;
                string name = Path.GetFileName(entry);
                if (String.IsNullOrEmpty(name)) continue;
                FileAttributes attributes;
                try { attributes = File.GetAttributes(entry); }
                catch { continue; }
                if (!_showHiddenFiles && (name.StartsWith(".", StringComparison.Ordinal) || (attributes & FileAttributes.Hidden) != 0)) continue;
                if (Directory.Exists(entry))
                {
                    if (name.Equals("node_modules", StringComparison.OrdinalIgnoreCase) || name.Equals(".git", StringComparison.OrdinalIgnoreCase) || name.Equals("dist", StringComparison.OrdinalIgnoreCase) || name.Equals("build", StringComparison.OrdinalIgnoreCase) || name.Equals("bin", StringComparison.OrdinalIgnoreCase) || name.Equals("obj", StringComparison.OrdinalIgnoreCase)) continue;
                    if ((attributes & FileAttributes.ReparsePoint) != 0) continue;
                    TreeNode folder = new TreeNode(name) { Tag = entry, ImageKey = "folder", SelectedImageKey = "folder" };
                    PopulateTree(folder, entry);
                    if (folder.Nodes.Count > 0) parent.Nodes.Add(folder);
                    continue;
                }
                try
                {
                    FileInfo info = new FileInfo(entry);
                    if (info.Length > MaxFileBytes || _workspaceBytes + info.Length > MaxWorkspaceBytes || !IsTextExtension(Path.GetExtension(entry))) continue;
                    if ((attributes & FileAttributes.ReparsePoint) != 0) continue;
                    string imageKey = ExplorerFileImageKey(Path.GetExtension(entry));
                    TreeNode file = new TreeNode(name) { Tag = entry, ImageKey = imageKey, SelectedImageKey = imageKey };
                    parent.Nodes.Add(file);
                    _workspaceBytes += info.Length;
                    _workspaceFileCount++;
                }
                catch { }
            }
        }

        private bool IsTextExtension(string extension)
        {
            string[] known = { ".cs", ".csproj", ".sln", ".ts", ".tsx", ".js", ".jsx", ".json", ".md", ".txt", ".html", ".css", ".xml", ".yml", ".yaml", ".toml", ".ini", ".config", ".py", ".go", ".rs", ".java", ".c", ".h", ".cpp", ".sql", ".sh", ".ps1", ".bat", ".cmd", ".vue", ".svelte", ".php", ".rb", ".env", ".gitignore", ".editorconfig", ".dockerfile" };
            return Array.IndexOf(known, extension.ToLowerInvariant()) >= 0 || Path.GetFileName(extension).Equals("dockerfile", StringComparison.OrdinalIgnoreCase);
        }

        private void OnTreeNodeDoubleClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            string filePath = e.Node.Tag as string;
            if (!String.IsNullOrEmpty(filePath) && File.Exists(filePath)) OpenDocument(filePath);
        }

        private void AddExplorerIcon(string key, Color color, string text, bool folder)
        {
            using (Bitmap bitmap = new Bitmap(16, 16))
            using (Graphics graphics = Graphics.FromImage(bitmap))
            using (SolidBrush brush = new SolidBrush(color))
            using (Font font = new Font("Segoe UI", 6.5F, FontStyle.Bold))
            {
                graphics.Clear(Color.Transparent);
                if (folder)
                {
                    graphics.FillRectangle(brush, 1, 5, 14, 9);
                    graphics.FillRectangle(brush, 2, 3, 6, 3);
                }
                else
                {
                    graphics.FillRectangle(brush, 2, 1, 12, 14);
                    TextRenderer.DrawText(graphics, text, font, new Rectangle(1, 3, 14, 10), Color.FromArgb(20, 25, 32), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                }
                _treeImages.Images.Add(key, bitmap);
            }
        }

        private string ExplorerFileImageKey(string extension)
        {
            string value = (extension ?? String.Empty).ToLowerInvariant();
            if (value == ".cs" || value == ".ts" || value == ".tsx" || value == ".cpp" || value == ".h" || value == ".java") return "code";
            if (value == ".js" || value == ".jsx") return "script";
            if (value == ".py") return "python";
            if (value == ".json") return "json";
            if (value == ".html" || value == ".xml" || value == ".vue" || value == ".svelte") return "markup";
            if (value == ".css") return "style";
            return "file";
        }

        private ContextMenuStrip CreateExplorerContextMenu()
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Items.Add("New File...", null, delegate { CreateWorkspaceFile(); });
            menu.Items.Add("New Folder...", null, delegate { CreateWorkspaceFolder(); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Rename...", null, delegate { RenameWorkspaceItem(); });
            menu.Items.Add("Delete...", null, delegate { DeleteWorkspaceItem(); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Refresh Explorer", null, delegate { RefreshWorkspaceExplorer(); });
            return menu;
        }

        private string PromptForText(string title, string prompt, string initialValue)
        {
            using (Form dialog = new Form())
            using (TextBox input = new TextBox())
            using (Button accept = new Button())
            using (Button cancel = new Button())
            {
                dialog.Text = title;
                dialog.StartPosition = FormStartPosition.CenterParent;
                dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                dialog.MinimizeBox = false;
                dialog.MaximizeBox = false;
                dialog.ShowInTaskbar = false;
                dialog.ClientSize = new Size(440, 132);
                dialog.BackColor = Color.FromArgb(15, 23, 42);
                dialog.ForeColor = ForeColor;
                Label label = new Label { Text = prompt, Left = 16, Top = 14, Width = 408, Height = 22, ForeColor = ForeColor };
                input.Left = 16;
                input.Top = 42;
                input.Width = 408;
                input.Text = initialValue ?? String.Empty;
                StyleInput(input);
                accept.Text = "OK";
                accept.Left = 254;
                accept.Top = 86;
                accept.Width = 80;
                accept.DialogResult = DialogResult.OK;
                StyleButton(accept, true);
                cancel.Text = "Cancel";
                cancel.Left = 344;
                cancel.Top = 86;
                cancel.Width = 80;
                cancel.DialogResult = DialogResult.Cancel;
                StyleButton(cancel, false);
                dialog.Controls.Add(label);
                dialog.Controls.Add(input);
                dialog.Controls.Add(accept);
                dialog.Controls.Add(cancel);
                dialog.AcceptButton = accept;
                dialog.CancelButton = cancel;
                return dialog.ShowDialog(this) == DialogResult.OK ? input.Text.Trim() : null;
            }
        }

        private bool IsInsideWorkspace(string path)
        {
            if (String.IsNullOrEmpty(_workspaceRoot) || String.IsNullOrEmpty(path)) return false;
            string root = Path.GetFullPath(_workspaceRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string candidate = Path.GetFullPath(path);
            return String.Equals(candidate, root, StringComparison.OrdinalIgnoreCase) || candidate.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }

        private string SelectedWorkspaceDirectory()
        {
            if (String.IsNullOrEmpty(_workspaceRoot) || !Directory.Exists(_workspaceRoot)) throw new InvalidOperationException("Open a workspace folder first.");
            string selected = _tree.SelectedNode == null ? _workspaceRoot : _tree.SelectedNode.Tag as string;
            if (String.IsNullOrEmpty(selected)) selected = _workspaceRoot;
            string directory = Directory.Exists(selected) ? selected : Path.GetDirectoryName(selected);
            if (String.IsNullOrEmpty(directory) || !IsInsideWorkspace(directory)) throw new InvalidOperationException("The selected location is outside the open workspace.");
            if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0) throw new InvalidOperationException("Linked folders are not modified by the Explorer.");
            return directory;
        }

        private string WorkspaceChildPath(string parent, string name)
        {
            if (String.IsNullOrWhiteSpace(name) || name == "." || name == ".." || Path.GetFileName(name) != name || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                throw new ArgumentException("Enter a valid file or folder name.");
            string path = Path.GetFullPath(Path.Combine(parent, name));
            if (!IsInsideWorkspace(path)) throw new InvalidOperationException("The requested path is outside the open workspace.");
            return path;
        }

        private void CreateWorkspaceFile()
        {
            try
            {
                string parent = SelectedWorkspaceDirectory();
                string name = PromptForText("New File", "File name", "new-file.txt");
                if (name == null) return;
                string path = WorkspaceChildPath(parent, name);
                if (File.Exists(path) || Directory.Exists(path)) throw new IOException("An item with that name already exists.");
                File.WriteAllText(path, String.Empty, new UTF8Encoding(false));
                RefreshWorkspaceExplorer();
                OpenDocument(path);
            }
            catch (Exception error) { MessageBox.Show(this, error.Message, "Could not create file", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        private void CreateWorkspaceFolder()
        {
            try
            {
                string parent = SelectedWorkspaceDirectory();
                string name = PromptForText("New Folder", "Folder name", "New Folder");
                if (name == null) return;
                string path = WorkspaceChildPath(parent, name);
                if (File.Exists(path) || Directory.Exists(path)) throw new IOException("An item with that name already exists.");
                Directory.CreateDirectory(path);
                RefreshWorkspaceExplorer();
            }
            catch (Exception error) { MessageBox.Show(this, error.Message, "Could not create folder", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        private void RenameWorkspaceItem()
        {
            try
            {
                if (_tree.SelectedNode == null) throw new InvalidOperationException("Select a file or folder to rename.");
                string source = _tree.SelectedNode.Tag as string;
                if (String.IsNullOrEmpty(source) || !IsInsideWorkspace(source) || String.Equals(source, _workspaceRoot, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("The workspace root cannot be renamed here.");
                string name = PromptForText("Rename", "New name", Path.GetFileName(source));
                if (name == null) return;
                string destination = WorkspaceChildPath(Path.GetDirectoryName(source), name);
                if (File.Exists(destination) || Directory.Exists(destination)) throw new IOException("An item with that name already exists.");
                bool isDirectory = Directory.Exists(source);
                if (isDirectory) Directory.Move(source, destination);
                else File.Move(source, destination);
                string sourcePrefix = source.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
                foreach (OpenDocument document in _documents)
                {
                    if (String.Equals(document.Path, source, StringComparison.OrdinalIgnoreCase)) document.Path = destination;
                    else if (isDirectory && document.Path.StartsWith(sourcePrefix, StringComparison.OrdinalIgnoreCase)) document.Path = destination + document.Path.Substring(source.Length);
                    else continue;
                    document.Name = Path.GetFileName(document.Path);
                    UpdateTabTitle(document);
                }
                RefreshWorkspaceExplorer();
            }
            catch (Exception error) { MessageBox.Show(this, error.Message, "Could not rename item", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        private void DeleteWorkspaceItem()
        {
            try
            {
                if (_tree.SelectedNode == null) throw new InvalidOperationException("Select a file or folder to delete.");
                string path = _tree.SelectedNode.Tag as string;
                if (String.IsNullOrEmpty(path) || !IsInsideWorkspace(path) || String.Equals(path, _workspaceRoot, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("The workspace root cannot be deleted here.");
                bool isDirectory = Directory.Exists(path);
                string prefix = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
                foreach (OpenDocument document in _documents)
                {
                    if (String.Equals(document.Path, path, StringComparison.OrdinalIgnoreCase) || (isDirectory && document.Path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
                        throw new InvalidOperationException("Close open editor tabs inside this item before deleting it.");
                }
                DialogResult confirmation = MessageBox.Show(this, "Permanently delete this " + (isDirectory ? "folder and its contents" : "file") + "?\r\n\r\n" + path, "Confirm Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (confirmation != DialogResult.Yes) return;
                if (isDirectory) Directory.Delete(path, true);
                else if (File.Exists(path)) File.Delete(path);
                RefreshWorkspaceExplorer();
            }
            catch (Exception error) { MessageBox.Show(this, error.Message, "Could not delete item", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        private void RefreshWorkspaceExplorer()
        {
            if (String.IsNullOrEmpty(_workspaceRoot) || !Directory.Exists(_workspaceRoot)) return;
            _workspaceBytes = 0;
            _workspaceFileCount = 0;
            _tree.BeginUpdate();
            _tree.Nodes.Clear();
            TreeNode root = new TreeNode(Path.GetFileName(_workspaceRoot)) { Tag = _workspaceRoot, ImageKey = "folder", SelectedImageKey = "folder" };
            _tree.Nodes.Add(root);
            PopulateTree(root, _workspaceRoot);
            root.Expand();
            _tree.EndUpdate();
            _fileStatus.Text = String.Format("{0} text files loaded", _workspaceFileCount);
            RefreshBranchStatus();
        }

        private void OpenFileDialog()
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "Open source file";
                dialog.Filter = "Text and source files|*.cs;*.csproj;*.sln;*.ts;*.tsx;*.js;*.jsx;*.json;*.md;*.txt;*.html;*.css;*.xml;*.yml;*.yaml;*.py;*.go;*.rs;*.java;*.c;*.h;*.cpp;*.sql;*.sh;*.ps1;*.vue;*.svelte;*.php;*.rb|All files|*.*";
                if (dialog.ShowDialog(this) == DialogResult.OK) OpenDocument(dialog.FileName);
            }
        }

        private void OpenDocument(string path)
        {
            string fullPath = Path.GetFullPath(path);
            foreach (OpenDocument existing in _documents)
            {
                if (String.Equals(existing.Path, fullPath, StringComparison.OrdinalIgnoreCase))
                {
                    foreach (TabPage page in _tabs.TabPages)
                    {
                        if (page.Tag == existing) { _tabs.SelectedTab = page; break; }
                    }
                    return;
                }
            }
            FileInfo info = new FileInfo(fullPath);
            if (info.Length > MaxFileBytes) { MessageBox.Show(this, "Files larger than 512 KB are not opened by this editor.", "My Omni", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            byte[] bytes = File.ReadAllBytes(fullPath);
            if (Array.IndexOf(bytes, (byte)0) >= 0) { MessageBox.Show(this, "This file appears to be binary and cannot be shown as text.", "My Omni", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            string content = File.ReadAllText(fullPath, Encoding.UTF8);
            CreateEditorDocument(fullPath, content, false);
        }

        private OpenDocument CreateEditorDocument(string fullPath, string content, bool dirty)
        {
            OpenDocument document = new OpenDocument { Path = fullPath, Name = String.IsNullOrEmpty(fullPath) ? "Untitled" : Path.GetFileName(fullPath), Editor = new RichTextBox(), Dirty = dirty };
            document.Editor.Dock = DockStyle.Fill;
            document.Editor.AcceptsTab = true;
            document.Editor.WordWrap = _editorWordWrap;
            document.Editor.DetectUrls = false;
            document.Editor.Font = new Font("Consolas", _editorFontSize);
            document.Editor.BackColor = Color.FromArgb(30, 30, 30);
            document.Editor.ForeColor = Color.FromArgb(212, 212, 212);
            document.Editor.BorderStyle = BorderStyle.None;
            Panel editorHost = new Panel { Dock = DockStyle.Fill, BackColor = document.Editor.BackColor };
            Panel gutter = new Panel { Dock = DockStyle.Left, Width = 48, BackColor = Color.FromArgb(24, 24, 24) };
            document.Gutter = gutter;
            gutter.Paint += delegate(object sender, PaintEventArgs e) { DrawLineNumbers(e.Graphics, gutter, document.Editor); };
            document.Editor.VScroll += delegate { gutter.Invalidate(); };
            editorHost.Controls.Add(document.Editor);
            editorHost.Controls.Add(gutter);
            document.Editor.ContextMenuStrip = CreateEditorContextMenu(document.Editor);
            EnableFileDrop(document.Editor);
            document.Editor.Text = content;
            document.Editor.TextChanged += delegate
            {
                document.Dirty = true;
                UpdateTabTitle(document);
                gutter.Invalidate();
                _pendingHighlight = document;
                _highlightTimer.Stop();
                _highlightTimer.Start();
            };
            document.Editor.SelectionChanged += delegate
            {
                gutter.Invalidate();
                if (ActiveDocument() == document) UpdateFileStatus();
            };
            TabPage tab = new TabPage(document.Name) { Tag = document, BackColor = Color.FromArgb(30, 30, 30), UseVisualStyleBackColor = false };
            tab.Controls.Add(editorHost);
            _documents.Add(document);
            _tabs.TabPages.Add(tab);
            _tabs.SelectedTab = tab;
            UpdateTabTitle(document);
            UpdateFileStatus();
            _pendingHighlight = document;
            _highlightTimer.Stop();
            _highlightTimer.Start();
            UpdateWelcomeVisibility();
            return document;
        }

        private void DrawLineNumbers(Graphics graphics, Panel gutter, RichTextBox editor)
        {
            if (gutter.ClientSize.Width <= 0 || gutter.ClientSize.Height <= 0) return;
            graphics.Clear(gutter.BackColor);
            int firstCharacter = editor.GetCharIndexFromPosition(new Point(1, 1));
            int firstLine = Math.Max(0, editor.GetLineFromCharIndex(firstCharacter));
            int lineCount = editor.Lines.Length;
            int currentLine = editor.GetLineFromCharIndex(editor.SelectionStart);
            for (int line = firstLine; line < lineCount; line++)
            {
                int character = editor.GetFirstCharIndexFromLine(line);
                if (character < 0) continue;
                int y = editor.GetPositionFromCharIndex(character).Y;
                if (y >= gutter.ClientSize.Height) break;
                Color color = line == currentLine ? Color.FromArgb(79, 205, 190) : Color.FromArgb(112, 126, 145);
                TextRenderer.DrawText(graphics, (line + 1).ToString(), editor.Font,
                    new Rectangle(2, y, gutter.ClientSize.Width - 8, editor.Font.Height + 2), color,
                    TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            }
        }

        private ContextMenuStrip CreateEditorContextMenu(RichTextBox editor)
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Items.Add("Undo", null, delegate { if (editor.CanUndo) editor.Undo(); });
            menu.Items.Add("Redo", null, delegate { if (editor.CanRedo) editor.Redo(); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Cut", null, delegate { editor.Cut(); });
            menu.Items.Add("Copy", null, delegate { editor.Copy(); });
            menu.Items.Add("Paste", null, delegate { editor.Paste(); });
            menu.Items.Add("Select All", null, delegate { editor.SelectAll(); });
            return menu;
        }

        private ContextMenuStrip CreateChatContextMenu()
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Items.Add("Copy", null, delegate { CopyChatText(); });
            menu.Items.Add("Select All", null, delegate { _chatLog.SelectAll(); });
            return menu;
        }

        private void EnableFileDrop(Control control)
        {
            control.AllowDrop = true;
            control.DragEnter += delegate(object sender, DragEventArgs e)
            {
                e.Effect = e.Data != null && e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
            };
            control.DragDrop += delegate(object sender, DragEventArgs e)
            {
                string[] paths = e.Data == null ? null : e.Data.GetData(DataFormats.FileDrop) as string[];
                if (paths == null) return;
                foreach (string path in paths)
                {
                    if (!File.Exists(path)) continue;
                    try { OpenDocument(path); }
                    catch (Exception error) { MessageBox.Show(this, error.Message, "Could not open dropped file", MessageBoxButtons.OK, MessageBoxIcon.Error); }
                }
            };
        }

        private void OnEditorTabDrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= _tabs.TabPages.Count) return;
            TabPage page = _tabs.TabPages[e.Index];
            bool selected = e.Index == _tabs.SelectedIndex;
            Color backgroundColor = selected ? Color.FromArgb(23, 35, 53) : Color.FromArgb(15, 23, 42);
            Color foregroundColor = selected ? Color.FromArgb(237, 245, 255) : Color.FromArgb(156, 169, 187);
            using (SolidBrush background = new SolidBrush(backgroundColor)) e.Graphics.FillRectangle(background, e.Bounds);
            Rectangle textBounds = new Rectangle(e.Bounds.Left + 10, e.Bounds.Top, e.Bounds.Width - 38, e.Bounds.Height);
            TextRenderer.DrawText(e.Graphics, page.Text, _tabs.Font, textBounds, foregroundColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            Rectangle close = new Rectangle(e.Bounds.Right - 23, e.Bounds.Top + (e.Bounds.Height - 14) / 2, 14, 14);
            using (Pen pen = new Pen(foregroundColor, 1.5F))
            {
                e.Graphics.DrawLine(pen, close.Left + 3, close.Top + 3, close.Right - 3, close.Bottom - 3);
                e.Graphics.DrawLine(pen, close.Right - 3, close.Top + 3, close.Left + 3, close.Bottom - 3);
            }
        }

        private void OnEditorTabMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            for (int index = 0; index < _tabs.TabPages.Count; index++)
            {
                Rectangle bounds = _tabs.GetTabRect(index);
                Rectangle close = new Rectangle(bounds.Right - 25, bounds.Top + (bounds.Height - 18) / 2, 18, 18);
                if (!close.Contains(e.Location)) continue;
                _tabs.SelectedIndex = index;
                CloseEditorTab(index);
                return;
            }
        }

        private void CloseEditorTab(int index)
        {
            if (index < 0 || index >= _tabs.TabPages.Count) return;
            TabPage page = _tabs.TabPages[index];
            OpenDocument document = page.Tag as OpenDocument;
            if (document != null && document.Dirty)
            {
                DialogResult result = MessageBox.Show(this, "Save changes to " + document.Name + " before closing?", "Unsaved Changes", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning);
                if (result == DialogResult.Cancel) return;
                if (result == DialogResult.Yes)
                {
                    SaveActiveDocument();
                    if (document.Dirty) return;
                }
            }
            _tabs.TabPages.Remove(page);
            if (document != null) _documents.Remove(document);
            page.Dispose();
            RefreshOpenEditors();
            UpdateFileStatus();
            UpdateWelcomeVisibility();
        }

        private void HighlightDocument(OpenDocument document)
        {
            if (document == null || document.Editor.IsDisposed) return;
            string text = document.Editor.Text;
            int selectionStart = document.Editor.SelectionStart;
            int selectionLength = document.Editor.SelectionLength;
            int firstVisibleLine = document.Editor.GetLineFromCharIndex(
                document.Editor.GetCharIndexFromPosition(new Point(1, 1)));
            string extension = Path.GetExtension(document.Path).ToLowerInvariant();
            string comments = null;
            string keywords = null;
            bool ignoreKeywordCase = false;

            if (extension == ".py" || extension == ".rb" || extension == ".sh" || extension == ".yaml" || extension == ".yml" || extension == ".toml" || extension == ".ini")
                comments = "#[^\\r\\n]*";
            else if (extension == ".sql")
            {
                comments = "--[^\\r\\n]*|/\\*[\\s\\S]*?\\*/";
                keywords = "SELECT|FROM|WHERE|INSERT|UPDATE|DELETE|CREATE|ALTER|DROP|JOIN|LEFT|RIGHT|INNER|OUTER|ON|AS|AND|OR|NOT|NULL|INTO|VALUES|SET|GROUP|BY|ORDER|HAVING|LIMIT|PRIMARY|KEY|FOREIGN|REFERENCES|TABLE|INDEX";
                ignoreKeywordCase = true;
            }
            else if (extension == ".ps1" || extension == ".psm1")
            {
                comments = "#[^\\r\\n]*|<#[\\s\\S]*?#>";
                keywords = "param|function|if|else|elseif|foreach|for|while|switch|return|try|catch|finally|throw|class|using|begin|process|end";
                ignoreKeywordCase = true;
            }
            else if (extension == ".html" || extension == ".xml" || extension == ".vue" || extension == ".svelte")
            {
                comments = "<!--[\\s\\S]*?-->|/\\*[\\s\\S]*?\\*/|//[^\\r\\n]*";
            }
            else if (extension == ".cs" || extension == ".js" || extension == ".jsx" || extension == ".ts" || extension == ".tsx" ||
                extension == ".go" || extension == ".rs" || extension == ".java" || extension == ".c" || extension == ".h" ||
                extension == ".cpp" || extension == ".php" || extension == ".css")
                comments = "/\\*[\\s\\S]*?\\*/|//[^\\r\\n]*";

            if (extension == ".cs")
                keywords = "abstract|as|base|bool|break|byte|case|catch|char|checked|class|const|continue|decimal|default|delegate|do|double|else|enum|event|explicit|extern|false|finally|fixed|float|for|foreach|goto|if|implicit|in|int|interface|internal|is|lock|long|namespace|new|null|object|operator|out|override|params|private|protected|public|readonly|ref|return|sbyte|sealed|short|sizeof|stackalloc|static|string|struct|switch|this|throw|true|try|typeof|uint|ulong|unchecked|unsafe|ushort|using|var|virtual|void|volatile|while|async|await|record|init|yield";
            else if (extension == ".js" || extension == ".jsx" || extension == ".ts" || extension == ".tsx")
                keywords = "as|async|await|break|case|catch|class|const|continue|debugger|default|delete|do|else|export|extends|false|finally|for|from|function|if|import|in|instanceof|let|new|null|of|return|static|super|switch|this|throw|true|try|typeof|undefined|var|void|while|with|yield|interface|type|enum|implements|private|protected|public|readonly";
            else if (extension == ".py")
                keywords = "and|as|assert|async|await|break|class|continue|def|del|elif|else|except|False|finally|for|from|global|if|import|in|is|lambda|nonlocal|not|or|pass|raise|return|True|try|while|with|yield|None";
            else if (extension == ".go")
                keywords = "break|case|chan|const|continue|default|defer|else|fallthrough|for|func|go|goto|if|import|interface|map|package|range|return|select|struct|switch|type|var|true|false|nil";
            else if (extension == ".rs")
                keywords = "as|async|await|break|const|continue|crate|dyn|else|enum|extern|false|fn|for|if|impl|in|let|loop|match|mod|move|mut|pub|ref|return|self|static|struct|super|trait|true|type|unsafe|use|where|while";
            else if (extension == ".java" || extension == ".c" || extension == ".h" || extension == ".cpp")
                keywords = "auto|bool|break|case|catch|char|class|const|continue|default|delete|do|double|else|enum|false|float|for|if|int|long|namespace|new|nullptr|private|protected|public|return|short|signed|sizeof|static|string|struct|switch|this|throw|true|try|typedef|typename|unsigned|using|void|volatile|while";
            else if (extension == ".json" || extension == ".yaml" || extension == ".yml")
                keywords = "true|false|null";

            document.Editor.SuspendLayout();
            try
            {
                document.Editor.SelectAll();
                document.Editor.SelectionColor = Color.FromArgb(220, 230, 242);
                if (!String.IsNullOrEmpty(keywords))
                {
                    RegexOptions options = RegexOptions.CultureInvariant | (ignoreKeywordCase ? RegexOptions.IgnoreCase : RegexOptions.None);
                    ApplySyntaxColor(document.Editor, text, "\\b(?:" + keywords + ")\\b", Color.FromArgb(86, 156, 214), options);
                }
                ApplySyntaxColor(document.Editor, text, "\"(?:\\\\.|[^\"\\\\])*\"|'(?:\\\\.|[^'\\\\])*'|`(?:\\\\.|[^`\\\\])*`",
                    Color.FromArgb(206, 145, 120), RegexOptions.CultureInvariant);
                ApplySyntaxColor(document.Editor, text, "\\b[0-9]+(?:\\.[0-9]+)?\\b",
                    Color.FromArgb(181, 206, 168), RegexOptions.CultureInvariant);
                if (!String.IsNullOrEmpty(comments))
                    ApplySyntaxColor(document.Editor, text, comments, Color.FromArgb(106, 153, 85), RegexOptions.CultureInvariant);

                document.Editor.Select(selectionStart, selectionLength);
                int firstVisibleIndex = document.Editor.GetFirstCharIndexFromLine(firstVisibleLine);
                if (firstVisibleIndex >= 0)
                {
                    document.Editor.Select(firstVisibleIndex, 0);
                    document.Editor.ScrollToCaret();
                    document.Editor.Select(selectionStart, selectionLength);
                }
            }
            finally { document.Editor.ResumeLayout(); }
        }

        private void ApplySyntaxColor(RichTextBox editor, string text, string pattern, Color color, RegexOptions options)
        {
            foreach (Match match in Regex.Matches(text, pattern, options))
            {
                editor.Select(match.Index, match.Length);
                editor.SelectionColor = color;
            }
        }

        private void UpdateTabTitle(OpenDocument document)
        {
            foreach (TabPage tab in _tabs.TabPages)
            {
                if (tab.Tag == document) tab.Text = (document.Dirty ? "* " : String.Empty) + document.Name;
            }
            RefreshOpenEditors();
            UpdateFileStatus();
        }

        private OpenDocument ActiveDocument()
        {
            return _tabs.SelectedTab == null ? null : _tabs.SelectedTab.Tag as OpenDocument;
        }

        private void SaveActiveDocument()
        {
            OpenDocument document = ActiveDocument();
            if (document == null) return;
            if (String.IsNullOrEmpty(document.Path))
            {
                SaveAsActiveDocument();
                return;
            }
            try
            {
                WriteFileAtomically(document.Path, new UTF8Encoding(false).GetBytes(document.Editor.Text));
                document.Dirty = false;
                UpdateTabTitle(document);
                _fileStatus.Text = "Saved";
            }
            catch (Exception error) { MessageBox.Show(this, error.Message, "Could not save file", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        private void SaveAsActiveDocument()
        {
            OpenDocument document = ActiveDocument();
            if (document == null) return;
            using (SaveFileDialog dialog = new SaveFileDialog())
            {
                dialog.Title = "Save As";
                dialog.Filter = "All files|*.*";
                dialog.OverwritePrompt = true;
                dialog.FileName = String.IsNullOrEmpty(document.Path) ? document.Name : Path.GetFileName(document.Path);
                string initialDirectory = String.IsNullOrEmpty(document.Path) ? _workspaceRoot : Path.GetDirectoryName(document.Path);
                if (!String.IsNullOrEmpty(initialDirectory) && Directory.Exists(initialDirectory)) dialog.InitialDirectory = initialDirectory;
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                string targetPath = Path.GetFullPath(dialog.FileName);
                try
                {
                    WriteFileAtomically(targetPath, new UTF8Encoding(false).GetBytes(document.Editor.Text));
                    document.Path = targetPath;
                    document.Name = Path.GetFileName(targetPath);
                    document.Dirty = false;
                    UpdateTabTitle(document);
                    _fileStatus.Text = "Saved";
                }
                catch (Exception error) { MessageBox.Show(this, error.Message, "Could not save file", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            }
        }

        private void UpdateFileStatus()
        {
            OpenDocument document = ActiveDocument();
            _fileStatus.Text = document == null ? "Ready" : (document.Dirty ? "Unsaved changes" : "Saved") + "  |  " + Path.GetExtension(document.Path).TrimStart('.').ToUpperInvariant();
            _languageStatus.Text = document == null ? "Plain Text" : LanguageName(Path.GetExtension(document.Path));
            if (document == null)
            {
                _positionStatus.Text = "Ln 1, Col 1";
                return;
            }
            int caret = document.Editor.SelectionStart;
            int line = document.Editor.GetLineFromCharIndex(caret) + 1;
            int lineStart = document.Editor.GetFirstCharIndexOfCurrentLine();
            _positionStatus.Text = "Ln " + line + ", Col " + (caret - Math.Max(0, lineStart) + 1);
        }

        private string LanguageName(string extension)
        {
            string value = (extension ?? String.Empty).TrimStart('.').ToLowerInvariant();
            if (value == "cs") return "C#";
            if (value == "js" || value == "jsx") return "JavaScript";
            if (value == "ts" || value == "tsx") return "TypeScript";
            if (value == "py") return "Python";
            if (value == "json") return "JSON";
            if (value == "html" || value == "htm") return "HTML";
            if (value == "css") return "CSS";
            if (value == "sql") return "SQL";
            if (value == "md") return "Markdown";
            return String.IsNullOrEmpty(value) ? "Plain Text" : value.ToUpperInvariant();
        }

        private void LoadSettings()
        {
            try
            {
                if (File.Exists(_settingsPath))
                {
                    Dictionary<string, object> settings = _serializer.Deserialize<Dictionary<string, object>>(File.ReadAllText(_settingsPath));
                    if (settings != null && settings.ContainsKey("model") && settings["model"] is string) _model = (string)settings["model"];
                    object settingValue;
                    bool booleanValue;
                    int integerValue;
                    if (settings != null && settings.TryGetValue("autoStartGateway", out settingValue) && Boolean.TryParse(Convert.ToString(settingValue), out booleanValue)) _autoStartGateway = booleanValue;
                    if (settings != null && settings.TryGetValue("autoIncludeActiveEditor", out settingValue) && Boolean.TryParse(Convert.ToString(settingValue), out booleanValue)) _autoIncludeActiveEditor = booleanValue;
                    if (settings != null && settings.TryGetValue("showHiddenFiles", out settingValue) && Boolean.TryParse(Convert.ToString(settingValue), out booleanValue)) _showHiddenFiles = booleanValue;
                    if (settings != null && settings.TryGetValue("editorWordWrap", out settingValue) && Boolean.TryParse(Convert.ToString(settingValue), out booleanValue)) _editorWordWrap = booleanValue;
                    if (settings != null && settings.TryGetValue("editorFontSize", out settingValue) && Int32.TryParse(Convert.ToString(settingValue), out integerValue)) _editorFontSize = Math.Max(8, Math.Min(24, integerValue));
                    if (settings != null && settings.TryGetValue("maximumAgentCharacters", out settingValue) && Int32.TryParse(Convert.ToString(settingValue), out integerValue)) _maximumAgentCharacters = Math.Max(1000, Math.Min(50000, integerValue));
                    if (settings != null && settings.TryGetValue("maximumWorkspaceFiles", out settingValue) && Int32.TryParse(Convert.ToString(settingValue), out integerValue)) _maximumWorkspaceFiles = Math.Max(100, Math.Min(3000, integerValue));
                    if (settings != null && settings.TryGetValue("accentColor", out settingValue))
                    {
                        string accent = Convert.ToString(settingValue);
                        if (!String.IsNullOrWhiteSpace(accent) && accent.StartsWith("#", StringComparison.Ordinal)) _accentColor = ColorTranslator.FromHtml(accent);
                    }
                    if (settings != null && settings.ContainsKey("recentProjects") && settings["recentProjects"] != null)
                    {
                        List<RecentProject> savedProjects = _serializer.ConvertToType<List<RecentProject>>(settings["recentProjects"]);
                        if (savedProjects != null)
                        {
                            foreach (RecentProject project in savedProjects)
                            {
                                if (project != null && !String.IsNullOrWhiteSpace(project.Path) && Directory.Exists(project.Path) && _recentProjects.Count < 12)
                                    _recentProjects.Add(project);
                            }
                        }
                    }
                }
                _modelInput.Text = _model;
                RefreshSettingsControls();
                RefreshWelcomeRecents();
            }
            catch (IOException error)
            {
                _connectionLabel.Text = "Could not load saved settings: " + error.Message;
            }
            catch (UnauthorizedAccessException error)
            {
                _connectionLabel.Text = "Could not load saved settings: " + error.Message;
            }
            catch (ArgumentException error)
            {
                _connectionLabel.Text = "Could not load saved settings: " + error.Message;
            }
            catch (InvalidOperationException error)
            {
                _connectionLabel.Text = "Could not load saved settings: " + error.Message;
            }
            _connected = false;
            if (!String.IsNullOrEmpty(LoadApiKey()))
            {
                _connectionLabel.Text = "Saved API key found. Checking OmniRoute...";
                _apiKeyStatus.Text = "A key is saved securely. Leave blank to keep it, or enter a replacement.";
            }
            ApplyAccentColorToControls(this);
            ApplyAccentColorToControls(_profileWindow);
            UpdateConnectionStatus();
        }

        private string LoadApiKey()
        {
            try
            {
                if (!File.Exists(_keyPath))
                {
                    _apiKeySaved = false;
                    return String.Empty;
                }
                byte[] encrypted = File.ReadAllBytes(_keyPath);
                string key = Encoding.UTF8.GetString(ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser));
                _apiKeySaved = !String.IsNullOrEmpty(key);
                return key;
            }
            catch (IOException error)
            {
                ShowApiKeyReadError(error);
                return String.Empty;
            }
            catch (UnauthorizedAccessException error)
            {
                ShowApiKeyReadError(error);
                return String.Empty;
            }
            catch (CryptographicException error)
            {
                ShowApiKeyReadError(error);
                return String.Empty;
            }
            catch (ArgumentException error)
            {
                ShowApiKeyReadError(error);
                return String.Empty;
            }
        }

        private void ShowApiKeyReadError(Exception error)
        {
            _apiKeySaved = false;
            _apiKeyStatus.Text = "Could not read the saved API key: " + error.Message;
            _connectionLabel.Text = "Saved API key could not be read. Re-enter it and click Connect.";
            _gatewayStatus.Text = "API key needs attention";
        }

        private void SaveSettings(string apiKey, string model)
        {
            Directory.CreateDirectory(_dataRoot);
            byte[] encrypted = ProtectedData.Protect(Encoding.UTF8.GetBytes(apiKey), null, DataProtectionScope.CurrentUser);
            WriteFileAtomically(_keyPath, encrypted);
            string savedKey = Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(_keyPath), null, DataProtectionScope.CurrentUser));
            if (!String.Equals(savedKey, apiKey, StringComparison.Ordinal))
                throw new IOException("The saved API key could not be verified.");
            _apiKeySaved = true;
            _model = String.IsNullOrWhiteSpace(model) ? "auto" : model.Trim().Substring(0, Math.Min(model.Trim().Length, 300));
            PersistSettings();
            _modelInput.Text = _model;
            _apiKeyStatus.Text = "API key saved and verified. Leave blank to keep it, or enter a replacement.";
        }

        private void PersistSettings()
        {
            Directory.CreateDirectory(_dataRoot);
            Dictionary<string, object> settings = new Dictionary<string, object>();
            settings.Add("model", _model);
            settings.Add("recentProjects", _recentProjects);
            settings.Add("autoStartGateway", _autoStartGateway);
            settings.Add("autoIncludeActiveEditor", _autoIncludeActiveEditor);
            settings.Add("showHiddenFiles", _showHiddenFiles);
            settings.Add("editorWordWrap", _editorWordWrap);
            settings.Add("editorFontSize", _editorFontSize);
            settings.Add("maximumAgentCharacters", _maximumAgentCharacters);
            settings.Add("maximumWorkspaceFiles", _maximumWorkspaceFiles);
            settings.Add("accentColor", ColorTranslator.ToHtml(_accentColor));
            WriteFileAtomically(_settingsPath, Encoding.UTF8.GetBytes(_serializer.Serialize(settings)));
        }

        private void RefreshSettingsControls()
        {
            if (_autoStartGatewayCheckBox != null) _autoStartGatewayCheckBox.Checked = _autoStartGateway;
            if (_editorWordWrapCheckBox != null) _editorWordWrapCheckBox.Checked = _editorWordWrap;
            if (_showHiddenFilesCheckBox != null) _showHiddenFilesCheckBox.Checked = _showHiddenFiles;
            if (_autoIncludeActiveEditorCheckBox != null) _autoIncludeActiveEditorCheckBox.Checked = _autoIncludeActiveEditor;
            if (_editorFontSizeSetting != null) _editorFontSizeSetting.Value = _editorFontSize;
            if (_maximumAgentCharactersSetting != null) _maximumAgentCharactersSetting.Value = _maximumAgentCharacters;
            if (_maximumWorkspaceFilesSetting != null) _maximumWorkspaceFilesSetting.Value = _maximumWorkspaceFiles;
            if (_accentPreviewLabel != null) _accentPreviewLabel.ForeColor = _accentColor;
        }

        private void SaveUserSettings()
        {
            if (_loadingSettings) return;
            try
            {
                PersistSettings();
                _notificationStatus.Text = "Settings saved";
            }
            catch (Exception error)
            {
                _notificationStatus.Text = "Settings save failed";
                MessageBox.Show(this, error.Message, "Could not save settings", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ApplyAccentColorToControls(Control parent)
        {
            if (parent == null) return;
            foreach (Control control in parent.Controls)
            {
                Button button = control as Button;
                if (button != null && button.AccessibleDescription == "OpenEditorPrimary")
                {
                    button.BackColor = _accentColor;
                    button.FlatAppearance.BorderColor = _accentColor;
                }
                ApplyAccentColorToControls(control);
            }
        }

        private void WriteFileAtomically(string path, byte[] contents)
        {
            string temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (FileStream stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    stream.Write(contents, 0, contents.Length);
                    stream.Flush(true);
                }
                if (File.Exists(path)) File.Replace(temporaryPath, path, null);
                else File.Move(temporaryPath, path);
            }
            finally
            {
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            }
        }

        private string GatewayCliPath()
        {
            return Path.Combine(_gatewayRoot, "node_modules", "omniroute", "bin", "omniroute.mjs");
        }

        private bool IsGatewayInstalled()
        {
            string cli = GatewayCliPath();
            string packagePath = Path.Combine(_gatewayRoot, "node_modules", "omniroute", "package.json");
            if (!File.Exists(cli) || !File.Exists(packagePath)) return false;

            Dictionary<string, object> package;
            try { package = _serializer.Deserialize<Dictionary<string, object>>(File.ReadAllText(packagePath)); }
            catch (InvalidOperationException) { return false; }
            catch (ArgumentException) { return false; }
            object version;
            return package != null &&
                package.TryGetValue("version", out version) &&
                String.Equals(Convert.ToString(version), OmniRouteVersion, StringComparison.Ordinal);
        }

        private async Task<bool> IsGatewayRunningAsync()
        {
            try
            {
                using (CancellationTokenSource timeout = new CancellationTokenSource(2500))
                using (HttpResponseMessage response = await _http.GetAsync("http://127.0.0.1:20128/", timeout.Token))
                {
                    return response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden || response.StatusCode == HttpStatusCode.MethodNotAllowed;
                }
            }
            catch { return false; }
        }

        private void StartGatewayProcess()
        {
            if (_gatewayProcess != null && !_gatewayProcess.HasExited) return;
            string cli = GatewayCliPath();
            if (!File.Exists(cli)) throw new InvalidOperationException("OmniRoute is not installed. Choose Install / Start OmniRoute first.");
            string nodePath = EnsureRuntime();
            ProcessStartInfo start = new ProcessStartInfo(nodePath, Quote(cli) + " serve --ready-timeout 180000");
            start.WorkingDirectory = _gatewayRoot;
            start.UseShellExecute = false;
            start.CreateNoWindow = true;
            start.RedirectStandardOutput = true;
            start.RedirectStandardError = true;
            start.EnvironmentVariables["OMNIROUTE_SERVER_HOST"] = "127.0.0.1";
            start.EnvironmentVariables["PORT"] = "20128";
            start.EnvironmentVariables["OMNIROUTE_READY_TIMEOUT_MS"] = "180000";
            start.EnvironmentVariables["OMNIROUTE_NO_UPDATE_NOTIFIER"] = "1";
            _gatewayProcess = Process.Start(start);
            if (_gatewayProcess == null) throw new InvalidOperationException("Windows could not start the OmniRoute process.");
            MonitorGatewayProcess(_gatewayProcess, true);
        }

        private void MonitorGatewayProcess(Process process, bool captureOutput)
        {
            if (captureOutput)
            {
                process.OutputDataReceived += delegate(object sender, DataReceivedEventArgs e) { if (!String.IsNullOrEmpty(e.Data)) SetConnectionStatus(e.Data); };
                process.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs e) { if (!String.IsNullOrEmpty(e.Data)) SetConnectionStatus(e.Data); };
            }
            process.EnableRaisingEvents = true;
            process.Exited += delegate
            {
                if (_isClosing) return;
                SetConnectionStatus("OmniRoute process exited. Click Install / Start OmniRoute to retry.");
                if (!IsDisposed && IsHandleCreated)
                {
                    try { BeginInvoke(new Action(delegate { if (!_isClosing) { _connected = false; _gatewayStatus.Text = "OmniRoute stopped"; UpdateConnectionStatus(); } })); }
                    catch { }
                }
            };
            if (captureOutput)
            {
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
            }
        }

        private void AdoptGatewayProcessIfOwned()
        {
            if (_gatewayProcess != null)
            {
                try { if (!_gatewayProcess.HasExited) return; }
                catch { }
                _gatewayProcess.Dispose();
                _gatewayProcess = null;
            }
            string nodePath = Path.Combine(_runtimeRoot, "node.exe");
            if (!File.Exists(nodePath)) return;
            try
            {
                ProcessStartInfo start = new ProcessStartInfo("netstat.exe", "-ano -p tcp");
                start.UseShellExecute = false;
                start.CreateNoWindow = true;
                start.RedirectStandardOutput = true;
                using (Process netstat = Process.Start(start))
                {
                    if (netstat == null) return;
                    string output = netstat.StandardOutput.ReadToEnd();
                    netstat.WaitForExit();
                    string[] lines = output.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (string line in lines)
                    {
                        string[] fields = line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                        int processId;
                        if (fields.Length < 5 || !fields[0].Equals("TCP", StringComparison.OrdinalIgnoreCase) ||
                            !fields[1].EndsWith(":20128", StringComparison.OrdinalIgnoreCase) ||
                            !fields[3].Equals("LISTENING", StringComparison.OrdinalIgnoreCase) ||
                            !Int32.TryParse(fields[fields.Length - 1], out processId)) continue;
                        Process candidate = null;
                        try
                        {
                            candidate = Process.GetProcessById(processId);
                            string imagePath = candidate.MainModule == null ? String.Empty : candidate.MainModule.FileName;
                            if (!String.IsNullOrEmpty(imagePath) && String.Equals(Path.GetFullPath(imagePath), Path.GetFullPath(nodePath), StringComparison.OrdinalIgnoreCase))
                            {
                                _gatewayProcess = candidate;
                                MonitorGatewayProcess(candidate, false);
                                return;
                            }
                        }
                        catch
                        {
                            if (candidate != null) candidate.Dispose();
                        }
                        if (candidate != null && candidate != _gatewayProcess) candidate.Dispose();
                    }
                }
            }
            catch { }
        }

        private void SetConnectionStatus(string text)
        {
            if (IsDisposed || !IsHandleCreated) return;
            try { BeginInvoke(new Action(delegate { _connectionLabel.Text = Clip(text, 120); })); }
            catch { }
        }

        private async Task<bool> WaitForGatewayAsync()
        {
            for (int attempt = 0; attempt < 180; attempt++)
            {
                if (await IsGatewayRunningAsync()) return true;
                await Task.Delay(1000);
            }
            return false;
        }

        private async Task<bool> EnsureGatewayRunningAsync()
        {
            if (await IsGatewayRunningAsync())
            {
                AdoptGatewayProcessIfOwned();
                return true;
            }
            if (!IsGatewayInstalled())
            {
                _connected = false;
                _gatewayStatus.Text = "OmniRoute not installed";
                _connectionLabel.Text = "Choose Install / Start OmniRoute to install or repair the local gateway.";
                return false;
            }
            try
            {
                _gatewayStatus.Text = "Starting OmniRoute...";
                _connectionLabel.Text = "Starting the installed local gateway...";
                StartGatewayProcess();
                if (await WaitForGatewayAsync())
                {
                    AdoptGatewayProcessIfOwned();
                    _gatewayStatus.Text = String.IsNullOrEmpty(LoadApiKey()) ? "Gateway online · API key needed" : "Gateway online · checking saved key";
                    return true;
                }
                _gatewayStatus.Text = "OmniRoute did not start";
                _connectionLabel.Text = "OmniRoute did not respond at 127.0.0.1:20128. Use Install / Start to retry.";
            }
            catch (Exception error)
            {
                _gatewayStatus.Text = "OmniRoute start failed";
                _connectionLabel.Text = "Could not start OmniRoute: " + error.Message;
            }
            _connected = false;
            return false;
        }

        private async Task AutoStartGatewayAsync()
        {
            if (!await IsGatewayRunningAsync() && !IsGatewayInstalled())
            {
                DialogResult install = MessageBox.Show(this,
                    "OmniRoute is missing or outdated. Install the local gateway and start it now? This downloads OmniRoute and its dependencies from npm.",
                    "Install OmniRoute", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (install == DialogResult.Yes)
                {
                    await InstallOrStartGatewayAsync();
                    return;
                }
                _connected = false;
                _gatewayStatus.Text = "OmniRoute not installed";
                _connectionLabel.Text = "Installation skipped. Choose Install / Start OmniRoute when you are ready.";
                return;
            }
            bool running = await EnsureGatewayRunningAsync();
            if (!running) return;
            string savedKey = LoadApiKey();
            if (String.IsNullOrEmpty(savedKey))
            {
                _connected = false;
                _connectionLabel.Text = "OmniRoute is running. Enter an API key from its dashboard and connect.";
                return;
            }
            try { await ConnectWithKeyAsync(savedKey, false); }
            catch (Exception error)
            {
                _connected = false;
                _gatewayStatus.Text = "API key needs attention";
                _connectionLabel.Text = error.Message;
            }
        }

        private async Task TestConnectionAsync()
        {
            string key = _apiKeyInput.Text.Trim();
            if (String.IsNullOrEmpty(key)) key = LoadApiKey();
            if (key.Length < 8)
            {
                MessageBox.Show(this, "Enter an API key or save one before testing the connection.", "Test Connection", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            _testConnectionButton.Enabled = false;
            _gatewayStatus.Text = "Testing OmniRoute connection...";
            _connectionLabel.Text = "Checking gateway and validating API key...";
            try
            {
                if (!await EnsureGatewayRunningAsync()) throw new InvalidOperationException(_connectionLabel.Text);
                await ConnectWithKeyAsync(key, false);
                _apiKeyStatus.Text = "Connection test passed. The key was not saved or changed.";
            }
            catch (Exception error)
            {
                _connected = false;
                _connectionLabel.Text = error.Message;
                _gatewayStatus.Text = "Connection error";
                UpdateConnectionStatus();
            }
            finally { _testConnectionButton.Enabled = true; }
        }

        private async Task ConnectAsync()
        {
            string key = _apiKeyInput.Text.Trim();
            if (String.IsNullOrEmpty(key)) key = LoadApiKey();
            if (key.Length < 8) { MessageBox.Show(this, "Enter your OmniRoute API key.", "Connect AI", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            _connectButton.Enabled = false;
            try
            {
                if (!await EnsureGatewayRunningAsync()) throw new InvalidOperationException(_connectionLabel.Text);
                await ConnectWithKeyAsync(key, true);
            }
            catch (Exception error)
            {
                _connected = false;
                _connectionLabel.Text = error.Message;
                _apiKeyStatus.Text = "Could not connect or save the API key: " + error.Message;
                _gatewayStatus.Text = error is HttpRequestException ? "OmniRoute is offline" : "Connection failed";
                UpdateConnectionStatus();
            }
            finally { _connectButton.Enabled = true; }
        }

        private async Task ConnectWithKeyAsync(string key, bool saveKey)
        {
            _connectionLabel.Text = "Checking your OmniRoute API key...";
            try
            {
                using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, Endpoint + "/models?prefix=alias&kind=chat"))
                using (HttpResponseMessage response = await _http.SendAsync(RequestWithKey(request, key)))
                {
                    if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden) throw new InvalidOperationException("OmniRoute rejected this API key. Create a key in its dashboard and try again.");
                    response.EnsureSuccessStatusCode();
                    UpdateModelsFromPayload(await response.Content.ReadAsStringAsync());
                }
            }
            catch (HttpRequestException error) { throw new InvalidOperationException("Could not reach OmniRoute at 127.0.0.1:20128. Click Install / Start OmniRoute and retry.", error); }
            if (saveKey)
            {
                try { SaveSettings(key, _modelInput.Text); }
                catch (IOException error) { _apiKeyStatus.Text = "Could not save the API key: " + error.Message; throw; }
                catch (UnauthorizedAccessException error) { _apiKeyStatus.Text = "Could not save the API key: " + error.Message; throw; }
                catch (CryptographicException error) { _apiKeyStatus.Text = "Could not save the API key: " + error.Message; throw; }
            }
            if (saveKey) _apiKeyInput.Clear();
            _connected = true;
            _gatewayStatus.Text = "OmniRoute connected · " + _model;
            _connectionLabel.Text = "Connected to OmniRoute · " + _model;
            UpdateConnectionStatus();
        }

        private HttpRequestMessage RequestWithKey(HttpRequestMessage request, string key)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
            return request;
        }

        private void UpdateModelsFromPayload(string json)
        {
            try
            {
                Dictionary<string, object> payload = _serializer.DeserializeObject(json) as Dictionary<string, object>;
                object[] rows = payload != null && payload.ContainsKey("data") ? payload["data"] as object[] : null;
                List<string> available = new List<string>();
                if (rows != null)
                {
                    foreach (object rowObject in rows)
                    {
                        Dictionary<string, object> row = rowObject as Dictionary<string, object>;
                        if (row != null && row.ContainsKey("id") && row["id"] is string)
                        {
                            string id = (string)row["id"];
                            if (!String.IsNullOrWhiteSpace(id) && !available.Contains(id)) available.Add(id);
                        }
                    }
                }

                string selected = String.IsNullOrWhiteSpace(_modelInput.Text) ? "auto" : _modelInput.Text.Trim();
                _modelInput.BeginUpdate();
                _modelInput.Items.Clear();
                _modelInput.Items.Add("auto");
                foreach (string model in available) _modelInput.Items.Add(model);
                _modelInput.Text = selected;
                _modelInput.EndUpdate();
                _chatModelSelector.BeginUpdate();
                _chatModelSelector.Items.Clear();
                _chatModelSelector.Items.Add("auto");
                foreach (string model in available) _chatModelSelector.Items.Add(model);
                _chatModelSelector.Text = selected;
                _chatModelSelector.EndUpdate();
                if (available.Count == 0) _connectionLabel.Text = "Connected · no model aliases returned; enter a model manually.";
            }
            catch { _connectionLabel.Text = "Connected · model list could not be parsed; enter a model manually."; }
        }

        private async Task SendChatAsync()
        {
            if (_busy) { if (_chatCancellation != null) _chatCancellation.Cancel(); return; }
            string prompt = _chatInput.Text.Trim();
            if (prompt.Length == 0) return;
            string key = LoadApiKey();
            if (String.IsNullOrEmpty(key)) { _connectionLabel.Text = "Enter an OmniRoute API key from the dashboard, then click Connect."; ShowProfileAndFocusKey(); return; }
            if (!await EnsureGatewayRunningAsync())
            {
                AppendChat("My Omni", _connectionLabel.Text);
                return;
            }
            if (!_connected)
            {
                try { await ConnectWithKeyAsync(key, false); }
                catch (Exception error)
                {
                    _connectionLabel.Text = error.Message;
                    AppendChat("My Omni", error.Message);
                    return;
                }
            }

            _busy = true;
            _sendButton.Text = "Stop";
            UpdateChatActionVisibility();
            _chatInput.Clear();
            AppendChat("You", prompt);
            _history.Add(new ChatMessage { role = "user", content = prompt });
            _chatCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            try
            {
                bool workspaceMode = !String.IsNullOrEmpty(_workspaceRoot) && Directory.Exists(_workspaceRoot);
                OpenDocument active = ActiveDocument();
                bool activeEditorMode = !workspaceMode && active != null;
                object[] tools = workspaceMode ? BuildWorkspaceTools() : activeEditorMode ? BuildActiveEditorTools() : null;
                string systemPrompt = workspaceMode
                    ? "You are the Open Editor coding assistant. Use workspace tools only for files in the open workspace. Propose edits for approval; never claim an edit was saved unless the user chose Apply & Save."
                    : activeEditorMode
                        ? "You are the Open Editor coding assistant working with the active editor only. Use active-editor tools to inspect or propose changes. The user must approve every edit. Untitled files remain unsaved until the user chooses a save path."
                        : "You are the Open Editor programming assistant. Answer general questions and generate code in Markdown without requiring a project. You have no access to local files in this mode; do not claim to inspect or edit them. You may offer code for the user to apply to the editor.";
                List<object> messages = new List<object>();
                messages.Add(new Dictionary<string, object>
                {
                    { "role", "system" },
                    { "content", systemPrompt }
                });
                int firstMessage = Math.Max(0, _history.Count - 19);
                for (int i = firstMessage; i < _history.Count; i++)
                {
                    ChatMessage historyMessage = _history[i];
                    messages.Add(new Dictionary<string, object> { { "role", historyMessage.role }, { "content", historyMessage.content } });
                }
                if (_autoIncludeActiveEditor && active != null && messages.Count > 0)
                {
                    string context = active.Editor.Text;
                    if (context.Length > _maximumAgentCharacters) context = context.Substring(0, _maximumAgentCharacters);
                    Dictionary<string, object> latest = messages[messages.Count - 1] as Dictionary<string, object>;
                    string activeName = String.IsNullOrEmpty(active.Path) ? active.Name : RelativePath(active.Path);
                    latest["content"] = Convert.ToString(latest["content"]) + "\n\nActive file: " + activeName + "\n```\n" + context + "\n```";
                }
                string reply = await RunWorkspaceAgentAsync(messages, key, _chatCancellation.Token, tools, workspaceMode);
                _history.Add(new ChatMessage { role = "assistant", content = reply });
                if (_history.Count > 20) _history.RemoveRange(0, _history.Count - 20);
                AppendChat("My Omni", reply);
                _lastPrompt = prompt;
                _hasAssistantResponse = true;
                UpdateChatActionVisibility();
            }
            catch (OperationCanceledException)
            {
                RemoveUnansweredPrompt(prompt);
                AppendChat("My Omni", "Request cancelled or timed out.");
            }
            catch (HttpRequestException)
            {
                RemoveUnansweredPrompt(prompt);
                _connected = false;
                _gatewayStatus.Text = "OmniRoute is offline";
                _connectionLabel.Text = "OmniRoute stopped responding. Click Install / Start OmniRoute and retry.";
                AppendChat("My Omni", _connectionLabel.Text);
            }
            catch (Exception error)
            {
                RemoveUnansweredPrompt(prompt);
                AppendChat("My Omni", "Request failed: " + error.Message);
            }
            finally
            {
                if (_chatCancellation != null) _chatCancellation.Dispose();
                _chatCancellation = null;
                _busy = false;
                _sendButton.Text = "Send";
                UpdateChatActionVisibility();
                _chatInput.Focus();
            }
        }

        private async Task<string> RunWorkspaceAgentAsync(List<object> messages, string key, CancellationToken cancellationToken, object[] tools, bool workspaceMode)
        {
            int totalToolCalls = 0;
            for (int round = 0; round < 6; round++)
            {
                using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, Endpoint + "/chat/completions"))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
                    Dictionary<string, object> requestBody = new Dictionary<string, object>
                    {
                        { "model", _model },
                        { "messages", messages },
                        { "stream", false }
                    };
                    if (tools != null && tools.Length > 0) requestBody.Add("tools", tools);
                    request.Content = new StringContent(_serializer.Serialize(requestBody), Encoding.UTF8, "application/json");
                    using (HttpResponseMessage response = await _http.SendAsync(request, cancellationToken))
                    {
                        string body = await response.Content.ReadAsStringAsync();
                        if (!response.IsSuccessStatusCode)
                        {
                            string apiError = ReadApiError(body);
                            throw new InvalidOperationException("OmniRoute returned HTTP " + (int)response.StatusCode +
                                (String.IsNullOrEmpty(apiError) ? "." : ": " + Clip(apiError, 300)));
                        }

                        Dictionary<string, object> payload = _serializer.DeserializeObject(body) as Dictionary<string, object>;
                        object[] choices = null;
                        object choicesValue;
                        if (payload != null && payload.TryGetValue("choices", out choicesValue)) choices = choicesValue as object[];
                        Dictionary<string, object> choice = choices != null && choices.Length > 0 ? choices[0] as Dictionary<string, object> : null;
                        object messageValue;
                        Dictionary<string, object> assistantMessage = choice != null && choice.TryGetValue("message", out messageValue)
                            ? messageValue as Dictionary<string, object>
                            : null;
                        object callsValue;
                        object[] toolCalls = assistantMessage != null && assistantMessage.TryGetValue("tool_calls", out callsValue)
                            ? callsValue as object[]
                            : null;
                        if (toolCalls == null || toolCalls.Length == 0) return ReadChatReply(body);
                        totalToolCalls += toolCalls.Length;
                        if (totalToolCalls > 12)
                            throw new InvalidOperationException("The workspace agent reached its 12-tool limit for one request.");

                        messages.Add(assistantMessage);
                        foreach (object callValue in toolCalls)
                        {
                            Dictionary<string, object> call = callValue as Dictionary<string, object>;
                            string callId = GetString(call, "id");
                            if (String.IsNullOrEmpty(callId)) throw new InvalidOperationException("OmniRoute returned a tool call without an ID.");
                            object functionValue;
                            Dictionary<string, object> function = call != null && call.TryGetValue("function", out functionValue)
                                ? functionValue as Dictionary<string, object>
                                : null;
                            string toolName = GetString(function, "name");
                            string arguments = GetString(function, "arguments");
                            string result = ExecuteAgentTool(toolName, arguments, workspaceMode);
                            messages.Add(new Dictionary<string, object> { { "role", "tool" }, { "tool_call_id", callId }, { "content", result } });
                            AppendChat(workspaceMode ? "Workspace agent" : "Editor agent", toolName + ": " + Clip(result, 180));
                        }
                    }
                }
            }
            throw new InvalidOperationException("The AI agent reached its tool-call limit before completing a response.");
        }

        private object[] BuildWorkspaceTools()
        {
            return new object[]
            {
                CreateWorkspaceTool("list_workspace_files", "List readable source and text files in the currently open workspace.",
                    new Dictionary<string, object>(), new string[0]),
                CreateWorkspaceTool("read_workspace_file", "Read a workspace-relative source/text file. Sensitive files are excluded.",
                    new Dictionary<string, object> { { "path", WorkspaceToolStringParameter("Path relative to the open workspace.") } },
                    new string[] { "path" }),
                CreateWorkspaceTool("search_workspace_files", "Search readable workspace source/text files for a literal text query.",
                    new Dictionary<string, object> { { "query", WorkspaceToolStringParameter("Literal text to find in workspace files.") } },
                    new string[] { "query" }),
                CreateWorkspaceTool("write_workspace_file", "Propose creation or replacement of a workspace file. The user must review and approve the change before it is written.",
                    new Dictionary<string, object>
                    {
                        { "path", WorkspaceToolStringParameter("Path relative to the open workspace.") },
                        { "content", WorkspaceToolStringParameter("The complete proposed file contents.") }
                    },
                    new string[] { "path", "content" }),
                CreateWorkspaceTool("edit_workspace_file", "Propose complete replacement contents for a workspace file. The user must approve the change; it is applied only to the editor buffer and is not saved to disk.",
                    new Dictionary<string, object>
                    {
                        { "path", WorkspaceToolStringParameter("Path relative to the open workspace.") },
                        { "content", WorkspaceToolStringParameter("The complete proposed file contents.") }
                    },
                    new string[] { "path", "content" })
            };
        }

        private object[] BuildActiveEditorTools()
        {
            return new object[]
            {
                CreateWorkspaceTool("read_active_editor", "Read the current active editor buffer, including unsaved content.",
                    new Dictionary<string, object>(), new string[0]),
                CreateWorkspaceTool("get_active_file_language", "Get the active editor file name and detected language.",
                    new Dictionary<string, object>(), new string[0]),
                CreateWorkspaceTool("replace_active_editor", "Propose replacement content for the active editor buffer. The user must review and approve the change; untitled files remain unsaved until the user chooses a save path.",
                    new Dictionary<string, object> { { "content", WorkspaceToolStringParameter("The complete proposed editor contents.") } },
                    new string[] { "content" })
            };
        }

        private Dictionary<string, object> CreateWorkspaceTool(string name, string description, Dictionary<string, object> properties, string[] required)
        {
            return new Dictionary<string, object>
            {
                { "type", "function" },
                { "function", new Dictionary<string, object>
                    {
                        { "name", name },
                        { "description", description },
                        { "parameters", new Dictionary<string, object>
                            {
                                { "type", "object" },
                                { "properties", properties },
                                { "required", required }
                            }
                        }
                    }
                }
            };
        }

        private Dictionary<string, object> WorkspaceToolStringParameter(string description)
        {
            return new Dictionary<string, object> { { "type", "string" }, { "description", description } };
        }

        private string ExecuteWorkspaceTool(string name, string argumentsJson)
        {
            try
            {
                Dictionary<string, object> arguments = _serializer.DeserializeObject(argumentsJson) as Dictionary<string, object>;
                if (name == "list_workspace_files") return ListWorkspaceFiles();
                if (arguments == null) return "Tool arguments were not a JSON object.";
                string path = GetString(arguments, "path");
                if (name == "read_workspace_file") return ReadWorkspaceFile(path);
                if (name == "search_workspace_files") return SearchWorkspaceFiles(GetString(arguments, "query"));
                if (name == "write_workspace_file" || name == "edit_workspace_file") return ProposeWorkspaceEdit(path, GetString(arguments, "content"));
                return "Unsupported workspace tool: " + Clip(name, 80);
            }
            catch (IOException error) { return FormatWorkspaceToolError(error); }
            catch (UnauthorizedAccessException error) { return FormatWorkspaceToolError(error); }
            catch (ArgumentException error) { return FormatWorkspaceToolError(error); }
            catch (InvalidOperationException error) { return FormatWorkspaceToolError(error); }
        }

        private string ExecuteAgentTool(string name, string argumentsJson, bool workspaceMode)
        {
            if (workspaceMode)
            {
                if (String.IsNullOrEmpty(_workspaceRoot) || !Directory.Exists(_workspaceRoot))
                    return "The workspace was closed before this operation could run.";
                return ExecuteWorkspaceTool(name, argumentsJson);
            }

            try
            {
                Dictionary<string, object> arguments = String.IsNullOrWhiteSpace(argumentsJson)
                    ? new Dictionary<string, object>()
                    : _serializer.DeserializeObject(argumentsJson) as Dictionary<string, object>;
                if (arguments == null) return "Tool arguments were not a JSON object.";
                OpenDocument document = ActiveDocument();
                if (document == null) return "There is no active editor. This request is now general chat only.";
                if (name == "read_active_editor")
                {
                    string content = document.Editor.Text;
                    if (content.Length > _maximumAgentCharacters) content = content.Substring(0, _maximumAgentCharacters) + "\n[Editor content truncated.]";
                    return "Active file: " + (String.IsNullOrEmpty(document.Path) ? document.Name : RelativePath(document.Path)) + "\nLanguage: " + LanguageName(Path.GetExtension(document.Path)) + "\n" + content;
                }
                if (name == "get_active_file_language")
                    return (String.IsNullOrEmpty(document.Path) ? document.Name : RelativePath(document.Path)) + " — " + LanguageName(Path.GetExtension(document.Path));
                if (name == "replace_active_editor") return ProposeActiveEditorEdit(GetString(arguments, "content"));
                return "Unsupported editor tool: " + Clip(name, 80);
            }
            catch (IOException error) { return FormatWorkspaceToolError(error); }
            catch (UnauthorizedAccessException error) { return FormatWorkspaceToolError(error); }
            catch (ArgumentException error) { return FormatWorkspaceToolError(error); }
            catch (InvalidOperationException error) { return FormatWorkspaceToolError(error); }
        }

        private string ProposeActiveEditorEdit(string content)
        {
            OpenDocument document = ActiveDocument();
            if (document == null) throw new InvalidOperationException("There is no active editor to update.");
            if (content == null) throw new InvalidOperationException("The proposed editor content is missing.");
            if (content.Length > MaxFileBytes || Encoding.UTF8.GetByteCount(content) > MaxFileBytes)
                throw new InvalidOperationException("The proposed editor content exceeds the 512 KB limit.");
            if (content.IndexOf('\0') >= 0) throw new InvalidOperationException("Binary content cannot be applied to the editor.");

            string displayName = String.IsNullOrEmpty(document.Path) ? document.Name : RelativePath(document.Path);
            DialogResult reviewResult = ReviewWorkspaceEdit(displayName, document.Editor.Text, content);
            if (reviewResult != DialogResult.OK && reviewResult != DialogResult.Yes)
                return "The user rejected the proposed editor change; the buffer was not changed.";

            document.Editor.Text = content;
            document.Dirty = true;
            UpdateTabTitle(document);
            if (reviewResult == DialogResult.Yes && !String.IsNullOrEmpty(document.Path))
            {
                WriteFileAtomically(document.Path, new UTF8Encoding(false).GetBytes(content));
                document.Dirty = false;
                UpdateTabTitle(document);
                return "The approved change was applied and saved to the active file.";
            }
            if (reviewResult == DialogResult.Yes && String.IsNullOrEmpty(document.Path))
            {
                SaveAsActiveDocument();
                return String.IsNullOrEmpty(document.Path)
                    ? "The approved change is in the untitled editor buffer and remains unsaved because no save path was selected."
                    : "The approved change was saved to " + Path.GetFileName(document.Path) + ".";
            }
            return "The approved change was applied to the active editor buffer. It remains unsaved until the user saves it.";
        }

        private string FormatWorkspaceToolError(Exception error)
        {
            return "Workspace operation failed: " + Clip(error.Message, 300);
        }

        private string ListWorkspaceFiles()
        {
            if (String.IsNullOrEmpty(_workspaceRoot) || !Directory.Exists(_workspaceRoot))
                return "No workspace is open. Ask the user to open a project folder first.";
            if ((File.GetAttributes(_workspaceRoot) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException("Linked or reparse-point workspace roots are not available to the workspace agent.");

            List<string> files = new List<string>();
            Stack<string> directories = new Stack<string>();
            directories.Push(_workspaceRoot);
            long totalBytes = 0;
            while (directories.Count > 0 && files.Count < _maximumWorkspaceFiles && totalBytes < MaxWorkspaceBytes)
            {
                string directory = directories.Pop();
                foreach (string entry in Directory.GetFileSystemEntries(directory))
                {
                    string name = Path.GetFileName(entry);
                    if (String.IsNullOrEmpty(name) || name.StartsWith(".", StringComparison.Ordinal) ||
                        name.Equals("node_modules", StringComparison.OrdinalIgnoreCase) ||
                        name.Equals("dist", StringComparison.OrdinalIgnoreCase) ||
                        name.Equals("build", StringComparison.OrdinalIgnoreCase) ||
                        name.Equals("bin", StringComparison.OrdinalIgnoreCase) ||
                        name.Equals("obj", StringComparison.OrdinalIgnoreCase))
                        continue;

                    FileAttributes attributes = File.GetAttributes(entry);
                    if ((attributes & FileAttributes.ReparsePoint) != 0) continue;
                    if ((attributes & FileAttributes.Directory) != 0)
                    {
                        directories.Push(entry);
                        continue;
                    }
                    if (!IsAgentReadableFile(entry) || (attributes & FileAttributes.Hidden) != 0) continue;
                    FileInfo info = new FileInfo(entry);
                    if (info.Length > MaxFileBytes || totalBytes + info.Length > MaxWorkspaceBytes) continue;
                    files.Add(RelativePath(entry));
                    totalBytes += info.Length;
                }
            }
            files.Sort(StringComparer.OrdinalIgnoreCase);
            string listing = _serializer.Serialize(files);
            return directories.Count > 0 ? listing + "\nWorkspace listing truncated at the configured file/size limit." : listing;
        }

        private string SearchWorkspaceFiles(string query)
        {
            if (String.IsNullOrWhiteSpace(_workspaceRoot) || !Directory.Exists(_workspaceRoot))
                throw new InvalidOperationException("Open a workspace before searching its files.");
            if (String.IsNullOrWhiteSpace(query)) throw new ArgumentException("Enter text to search for.");
            if ((File.GetAttributes(_workspaceRoot) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException("Linked or reparse-point workspace roots are not available to the workspace agent.");

            List<string> matches = new List<string>();
            Stack<string> directories = new Stack<string>();
            directories.Push(_workspaceRoot);
            int scannedFiles = 0;
            long scannedBytes = 0;
            while (directories.Count > 0 && scannedFiles < _maximumWorkspaceFiles && scannedBytes < MaxWorkspaceBytes && matches.Count < 100)
            {
                string directory = directories.Pop();
                foreach (string entry in Directory.GetFileSystemEntries(directory))
                {
                    string name = Path.GetFileName(entry);
                    if (String.IsNullOrEmpty(name) || name.StartsWith(".", StringComparison.Ordinal) ||
                        name.Equals("node_modules", StringComparison.OrdinalIgnoreCase) ||
                        name.Equals("dist", StringComparison.OrdinalIgnoreCase) || name.Equals("build", StringComparison.OrdinalIgnoreCase) ||
                        name.Equals("bin", StringComparison.OrdinalIgnoreCase) || name.Equals("obj", StringComparison.OrdinalIgnoreCase)) continue;
                    FileAttributes attributes = File.GetAttributes(entry);
                    if ((attributes & (FileAttributes.ReparsePoint | FileAttributes.Hidden)) != 0) continue;
                    if ((attributes & FileAttributes.Directory) != 0)
                    {
                        directories.Push(entry);
                        continue;
                    }
                    if (!IsAgentReadableFile(entry)) continue;
                    FileInfo info = new FileInfo(entry);
                    if (info.Length > MaxFileBytes || scannedBytes + info.Length > MaxWorkspaceBytes) continue;
                    scannedFiles++;
                    scannedBytes += info.Length;
                    byte[] bytes = File.ReadAllBytes(entry);
                    if (Array.IndexOf(bytes, (byte)0) >= 0) continue;
                    string[] lines = Encoding.UTF8.GetString(bytes).Split(new string[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);
                    for (int line = 0; line < lines.Length && matches.Count < 100; line++)
                    {
                        if (lines[line].IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                            matches.Add(RelativePath(entry) + ":" + (line + 1) + ": " + Clip(lines[line].Trim(), 160));
                    }
                    if (scannedFiles >= _maximumWorkspaceFiles || scannedBytes >= MaxWorkspaceBytes || matches.Count >= 100) break;
                }
            }
            return matches.Count == 0 ? "No matching text found in readable workspace files." : String.Join("\r\n", matches.ToArray());
        }

        private string ReadWorkspaceFile(string relativePath)
        {
            string fullPath = ResolveWorkspacePath(relativePath, false);
            if (!IsAgentReadableFile(fullPath)) throw new InvalidOperationException("This file type is excluded from AI workspace access.");
            OpenDocument openDocument = FindOpenDocument(fullPath);
            string content;
            if (openDocument != null) content = openDocument.Editor.Text;
            else
            {
                FileInfo info = new FileInfo(fullPath);
                if (info.Length > MaxFileBytes) throw new InvalidOperationException("Files over 512 KB are not available to the workspace agent.");
                byte[] bytes = File.ReadAllBytes(fullPath);
                if (Array.IndexOf(bytes, (byte)0) >= 0) throw new InvalidOperationException("Binary files are not available to the workspace agent.");
                content = File.ReadAllText(fullPath, Encoding.UTF8);
            }
            if (content.Length > _maximumAgentCharacters) content = content.Substring(0, _maximumAgentCharacters) + "\n[File content truncated.]";
            return content;
        }

        private string ProposeWorkspaceEdit(string relativePath, string content)
        {
            string fullPath = ResolveWorkspacePath(relativePath, true);
            if (!IsAgentReadableFile(fullPath)) throw new InvalidOperationException("Only supported source/text files may be edited by the workspace agent.");
            if (content == null) throw new InvalidOperationException("The proposed file content is missing.");
            if (content.Length > MaxFileBytes || Encoding.UTF8.GetByteCount(content) > MaxFileBytes)
                throw new InvalidOperationException("The proposed file is larger than the 512 KB editor limit.");
            if (content.IndexOf('\0') >= 0) throw new InvalidOperationException("Binary file content cannot be applied in the text editor.");

            OpenDocument document = FindOpenDocument(fullPath);
            string currentContent;
            if (document != null) currentContent = document.Editor.Text;
            else if (File.Exists(fullPath))
            {
                byte[] bytes = File.ReadAllBytes(fullPath);
                if (bytes.Length > MaxFileBytes || Array.IndexOf(bytes, (byte)0) >= 0)
                    throw new InvalidOperationException("This file is binary or exceeds the editor size limit.");
                currentContent = File.ReadAllText(fullPath, Encoding.UTF8);
            }
            else currentContent = String.Empty;

            DialogResult reviewResult = ReviewWorkspaceEdit(relativePath, currentContent, content);
            if (reviewResult != DialogResult.OK && reviewResult != DialogResult.Yes)
                return "The user declined the proposed edit; the file and editor buffer were not changed.";

            if (reviewResult == DialogResult.Yes)
            {
                WriteFileAtomically(fullPath, new UTF8Encoding(false).GetBytes(content));
                if (document == null) document = CreateEditorDocument(fullPath, content, false);
                else
                {
                    document.Editor.Text = content;
                    document.Dirty = false;
                    UpdateTabTitle(document);
                }
                return "The user approved the change and it was written to disk.";
            }

            if (document == null) document = CreateEditorDocument(fullPath, currentContent, false);
            document.Editor.Text = content;
            document.Dirty = true;
            UpdateTabTitle(document);
            return "Approved and applied to the open editor buffer. The user must save the file to write it to disk.";
        }

        private DialogResult ReviewWorkspaceEdit(string relativePath, string currentContent, string proposedContent)
        {
            using (Form review = new Form())
            {
                review.Text = "Review AI Edit - " + relativePath;
                review.StartPosition = FormStartPosition.CenterParent;
                review.Size = new Size(1000, 700);
                review.MinimumSize = new Size(720, 480);
                review.BackColor = Color.FromArgb(11, 17, 32);
                review.ForeColor = ForeColor;

                TableLayoutPanel layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(8) };
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
                layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
                review.Controls.Add(layout);
                layout.Controls.Add(new Label { Text = relativePath + " — review the complete change before applying it to the editor buffer.", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);

                SplitContainer comparison = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Vertical };
                review.Shown += delegate
                {
                    if (comparison.Width > 500) comparison.SplitterDistance = (comparison.Width - comparison.SplitterWidth) / 2;
                };
                layout.Controls.Add(comparison, 0, 1);
                RichTextBox before = CreateReviewTextBox(currentContent);
                RichTextBox after = CreateReviewTextBox(proposedContent);
                comparison.Panel1.Controls.Add(before);
                comparison.Panel2.Controls.Add(after);

                FlowLayoutPanel buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
                layout.Controls.Add(buttons, 0, 2);
                Button cancel = new Button { Text = "Reject", Width = 90, Height = 32, DialogResult = DialogResult.Cancel };
                StyleButton(cancel, false);
                Button apply = new Button { Text = "Apply to Editor", Width = 130, Height = 32, DialogResult = DialogResult.OK };
                StyleButton(apply, true);
                Button save = new Button { Text = "Apply & Save", Width = 120, Height = 32, DialogResult = DialogResult.Yes };
                StyleButton(save, true);
                buttons.Controls.Add(cancel);
                buttons.Controls.Add(apply);
                buttons.Controls.Add(save);
                review.CancelButton = cancel;
                review.AcceptButton = apply;
                return review.ShowDialog(this);
            }
        }

        private RichTextBox CreateReviewTextBox(string text)
        {
            RichTextBox box = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                WordWrap = false,
                DetectUrls = false,
                Font = new Font("Consolas", 9F),
                BackColor = Color.FromArgb(10, 15, 25),
                ForeColor = Color.FromArgb(220, 230, 242),
                Text = text
            };
            return box;
        }

        private OpenDocument FindOpenDocument(string fullPath)
        {
            foreach (OpenDocument document in _documents)
                if (String.Equals(document.Path, fullPath, StringComparison.OrdinalIgnoreCase)) return document;
            return null;
        }

        private string ResolveWorkspacePath(string relativePath, bool allowNewFile)
        {
            if (String.IsNullOrWhiteSpace(_workspaceRoot) || !Directory.Exists(_workspaceRoot))
                throw new InvalidOperationException("Open a workspace folder before using workspace file tools.");
            if (String.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath))
                throw new ArgumentException("Provide a workspace-relative file path.");

            string root = Path.GetFullPath(_workspaceRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string fullPath = Path.GetFullPath(Path.Combine(root, relativePath));
            if (!fullPath.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("The requested path is outside the open workspace.");

            string parent = Path.GetDirectoryName(fullPath);
            if (String.IsNullOrEmpty(parent) || !Directory.Exists(parent))
                throw new ArgumentException("The file's parent directory does not exist in the workspace.");
            string current = parent;
            while (current.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new ArgumentException("Files in linked or reparse-point directories are not available to the workspace agent.");
                if (String.Equals(current, root, StringComparison.OrdinalIgnoreCase)) break;
                current = Path.GetDirectoryName(current);
                if (String.IsNullOrEmpty(current)) break;
            }
            if (File.Exists(fullPath))
            {
                if ((File.GetAttributes(fullPath) & FileAttributes.ReparsePoint) != 0)
                    throw new ArgumentException("Linked or reparse-point files are not available to the workspace agent.");
            }
            else if (!allowNewFile) throw new FileNotFoundException("The requested workspace file does not exist.", fullPath);
            return fullPath;
        }

        private bool IsAgentReadableFile(string path)
        {
            string name = Path.GetFileName(path);
            string extension = Path.GetExtension(path);
            if (String.IsNullOrEmpty(name) || name.Equals(".env", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith(".env.", StringComparison.OrdinalIgnoreCase) ||
                name.IndexOf("secret", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("credential", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("password", StringComparison.OrdinalIgnoreCase) >= 0 ||
                extension.Equals(".key", StringComparison.OrdinalIgnoreCase) ||
                extension.Equals(".pem", StringComparison.OrdinalIgnoreCase) ||
                extension.Equals(".pfx", StringComparison.OrdinalIgnoreCase))
                return false;
            return IsTextExtension(extension);
        }

        private string GetString(Dictionary<string, object> dictionary, string key)
        {
            object value;
            return dictionary != null && dictionary.TryGetValue(key, out value) && value is string ? (string)value : String.Empty;
        }

        private string ReadChatReply(string json)
        {
            Dictionary<string, object> payload;
            try { payload = _serializer.DeserializeObject(json) as Dictionary<string, object>; }
            catch (InvalidOperationException error) { throw new InvalidOperationException("OmniRoute returned invalid chat JSON.", error); }
            if (payload == null) throw new InvalidOperationException("OmniRoute returned an empty chat response.");

            string apiError = ReadApiError(payload);
            if (!String.IsNullOrEmpty(apiError)) throw new InvalidOperationException("OmniRoute chat error: " + Clip(apiError, 300));

            object outputText;
            if (payload.TryGetValue("output_text", out outputText))
            {
                string text = ReadTextContent(outputText);
                if (!String.IsNullOrWhiteSpace(text)) return text;
            }

            object choicesValue;
            object[] choices = payload.TryGetValue("choices", out choicesValue) ? choicesValue as object[] : null;
            Dictionary<string, object> choice = choices != null && choices.Length > 0 ? choices[0] as Dictionary<string, object> : null;
            object messageValue;
            Dictionary<string, object> message = choice != null && choice.TryGetValue("message", out messageValue) ? messageValue as Dictionary<string, object> : null;
            object content;
            string reply = message != null && message.TryGetValue("content", out content) ? ReadTextContent(content) : String.Empty;
            if (String.IsNullOrWhiteSpace(reply) && choice != null && choice.TryGetValue("text", out content))
                reply = ReadTextContent(content);
            if (!String.IsNullOrWhiteSpace(reply)) return reply;

            object finishReason;
            string reason = choice != null && choice.TryGetValue("finish_reason", out finishReason)
                ? Convert.ToString(finishReason)
                : String.Empty;
            throw new InvalidOperationException("OmniRoute returned no assistant text" +
                (String.IsNullOrEmpty(reason) ? "." : " (finish reason: " + Clip(reason, 80) + ").") +
                " Check the selected model and provider configuration in the OmniRoute dashboard.");
        }

        private string ReadTextContent(object content)
        {
            string text = content as string;
            if (text != null) return text;

            object[] parts = content as object[];
            if (parts == null) return String.Empty;
            StringBuilder result = new StringBuilder();
            foreach (object part in parts)
            {
                string partText = part as string;
                if (partText != null)
                {
                    result.Append(partText);
                    continue;
                }
                Dictionary<string, object> item = part as Dictionary<string, object>;
                object value;
                if (item != null && item.TryGetValue("text", out value) && value is string)
                    result.Append((string)value);
            }
            return result.ToString();
        }

        private string ReadApiError(string json)
        {
            try { return ReadApiError(_serializer.DeserializeObject(json) as Dictionary<string, object>); }
            catch (InvalidOperationException) { return String.Empty; }
        }

        private string ReadApiError(Dictionary<string, object> payload)
        {
            if (payload == null) return String.Empty;
            object errorValue;
            if (!payload.TryGetValue("error", out errorValue)) return String.Empty;
            string errorText = errorValue as string;
            if (errorText != null) return errorText;
            Dictionary<string, object> error = errorValue as Dictionary<string, object>;
            object message;
            return error != null && error.TryGetValue("message", out message)
                ? ReadTextContent(message)
                : String.Empty;
        }

        private void RemoveUnansweredPrompt(string prompt)
        {
            int last = _history.Count - 1;
            if (last >= 0 && _history[last].role == "user" && _history[last].content == prompt)
                _history.RemoveAt(last);
        }

        private string RelativePath(string path)
        {
            if (String.IsNullOrEmpty(_workspaceRoot) || !path.StartsWith(_workspaceRoot, StringComparison.OrdinalIgnoreCase)) return Path.GetFileName(path);
            return path.Substring(_workspaceRoot.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Replace(Path.DirectorySeparatorChar, '/');
        }

        private void AppendChat(string speaker, string content)
        {
            if (_chatLog.TextLength > 500000) _chatLog.Clear();
            _chatLog.SelectionStart = _chatLog.TextLength;
            _chatLog.SelectionLength = 0;
            _chatLog.SelectionColor = speaker == "You" ? Color.FromArgb(125, 211, 252) : Color.FromArgb(74, 222, 128);
            _chatLog.AppendText(speaker + "\r\n");
            _chatLog.SelectionColor = Color.FromArgb(220, 230, 242);
            _chatLog.AppendText(content + "\r\n\r\n");
            _chatLog.ScrollToCaret();
        }

        private async Task InstallOrStartGatewayAsync()
        {
            if (await IsGatewayRunningAsync())
            {
                string savedKey = LoadApiKey();
                if (!String.IsNullOrEmpty(savedKey))
                {
                    try { await ConnectWithKeyAsync(savedKey, false); }
                    catch (Exception error) { _connectionLabel.Text = error.Message; }
                }
                else
                {
                    _gatewayStatus.Text = "Gateway online · API key needed";
                    _connectionLabel.Text = "OmniRoute is already running. Enter your API key and connect.";
                    OpenDashboard();
                }
                return;
            }

            _installButton.Enabled = false;
            string originalButtonText = _installButton.Text;
            try
            {
                _installButton.Text = "Preparing...";
                _gatewayStatus.Text = "Preparing OmniRoute setup";
                _connectionLabel.Text = "Extracting the bundled Node and npm runtime...";
                AppendChat("Setup", "Preparing the bundled Node and npm runtime...");

                string nodePath = await Task.Run(delegate { return EnsureRuntime(); });
                Directory.CreateDirectory(_gatewayRoot);
                if (!IsGatewayInstalled())
                {
                    string npmCli = Path.Combine(_runtimeRoot, "npm", "bin", "npm-cli.js");
                    if (!File.Exists(npmCli)) throw new InvalidOperationException("The bundled npm runtime is missing.");

                    _installButton.Text = "Installing...";
                    _gatewayStatus.Text = "Installing OmniRoute";
                    _connectionLabel.Text = "Installing OmniRoute into the local app data folder...";
                    AppendChat("Setup", "Installing the OmniRoute package and local gateway...");
                    ConfigureGatewayNpmScriptAllowList();
                    string npmArgs = Quote(npmCli) + " install omniroute@" + OmniRouteVersion + " --prefix " + Quote(_gatewayRoot) + " --no-fund --no-audit --loglevel error";
                    await RunNodeProcessAsync(nodePath, npmArgs, _runtimeRoot, delegate(string line)
                    {
                        _connectionLabel.Text = "OmniRoute install: " + Clip(line, 100);
                        AppendChat("npm", line);
                    });
                }

                if (!IsGatewayInstalled()) throw new InvalidOperationException("OmniRoute " + OmniRouteVersion + " was not installed correctly.");
                _installButton.Text = "Starting...";
                _gatewayStatus.Text = "Starting local gateway";
                _connectionLabel.Text = "Starting OmniRoute on 127.0.0.1:20128...";
                AppendChat("Setup", "Starting OmniRoute on 127.0.0.1:20128...");
                StartGatewayProcess();
                if (!await WaitForGatewayAsync()) throw new InvalidOperationException("OmniRoute installed, but it did not respond at 127.0.0.1:20128. Review the npm output above and retry.");

                string savedApiKey = LoadApiKey();
                if (!String.IsNullOrEmpty(savedApiKey))
                {
                    await ConnectWithKeyAsync(savedApiKey, false);
                }
                else
                {
                    _connectionLabel.Text = "OmniRoute is running. Create a key in its dashboard, then connect.";
                    _gatewayStatus.Text = "Gateway online · API key needed";
                    AppendChat("Setup", "OmniRoute is running at http://127.0.0.1:20128/. Connect a provider in its dashboard, create an API key, then return and connect.");
                    OpenDashboard();
                }
            }
            catch (Exception error)
            {
                _gatewayStatus.Text = "OmniRoute setup failed";
                _connectionLabel.Text = Clip(error.Message, 140);
                AppendChat("Setup error", error.Message);
            }
            finally
            {
                _installButton.Enabled = true;
                _installButton.Text = originalButtonText;
            }
        }

        private void ConfigureGatewayNpmScriptAllowList()
        {
            string packagePath = Path.Combine(_gatewayRoot, "package.json");
            Dictionary<string, object> package = new Dictionary<string, object>();
            if (File.Exists(packagePath))
            {
                try { package = _serializer.Deserialize<Dictionary<string, object>>(File.ReadAllText(packagePath)); }
                catch (InvalidOperationException error) { throw new InvalidOperationException("The local OmniRoute package manifest is invalid.", error); }
                catch (ArgumentException error) { throw new InvalidOperationException("The local OmniRoute package manifest is invalid.", error); }
                if (package == null) throw new InvalidOperationException("The local OmniRoute package manifest is invalid.");
            }

            object allowScriptsValue;
            Dictionary<string, object> allowScripts = null;
            if (package.TryGetValue("allowScripts", out allowScriptsValue))
            {
                allowScripts = allowScriptsValue as Dictionary<string, object>;
                if (allowScripts == null) throw new InvalidOperationException("The local OmniRoute allowScripts setting must be a JSON object.");
            }
            if (allowScripts == null) allowScripts = new Dictionary<string, object>();

            string[] allowedPackages = new string[] { "omniroute", "keytar", "onnxruntime-node", "@parcel/watcher", "@swc/core", "protobufjs", "esbuild" };
            foreach (string packageName in allowedPackages) allowScripts[packageName] = true;
            package["allowScripts"] = allowScripts;
            WriteFileAtomically(packagePath, new UTF8Encoding(false).GetBytes(_serializer.Serialize(package)));
        }

        private string EnsureRuntime()
        {
            Directory.CreateDirectory(_runtimeRoot);
            string nodePath = Path.Combine(_runtimeRoot, "node.exe");
            string npmRoot = Path.Combine(_runtimeRoot, "npm");
            string npmCli = Path.Combine(npmRoot, "bin", "npm-cli.js");
            if (!File.Exists(nodePath)) ExtractResource("MyOmniNative.node.exe", nodePath);
            if (!File.Exists(npmCli))
            {
                if (Directory.Exists(npmRoot)) Directory.Delete(npmRoot, true);
                Directory.CreateDirectory(npmRoot);
                using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("MyOmniNative.npm.zip"))
                {
                    if (stream == null) throw new InvalidOperationException("The bundled npm runtime is missing.");
                    using (ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Read))
                    {
                        foreach (ZipArchiveEntry entry in archive.Entries)
                        {
                            string relative = entry.FullName.Replace('/', Path.DirectorySeparatorChar);
                            string target = Path.GetFullPath(Path.Combine(npmRoot, relative));
                            string prefix = Path.GetFullPath(npmRoot) + Path.DirectorySeparatorChar;
                            if (!target.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Invalid bundled npm path.");
                            if (String.IsNullOrEmpty(entry.Name)) Directory.CreateDirectory(target);
                            else
                            {
                                Directory.CreateDirectory(Path.GetDirectoryName(target));
                                entry.ExtractToFile(target, true);
                            }
                        }
                    }
                }
            }
            if (!File.Exists(npmCli)) throw new InvalidOperationException("The bundled npm runtime did not extract correctly.");
            return nodePath;
        }

        private void ExtractResource(string resourceName, string outputPath)
        {
            using (Stream source = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
            {
                if (source == null) throw new InvalidOperationException("A required app runtime resource is missing: " + resourceName);
                using (FileStream destination = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None)) source.CopyTo(destination);
            }
        }

        private async Task RunNodeProcessAsync(string nodePath, string arguments, string workingDirectory, Action<string> onOutput)
        {
            await Task.Run(delegate
            {
                ProcessStartInfo start = new ProcessStartInfo(nodePath, arguments);
                start.WorkingDirectory = workingDirectory;
                start.UseShellExecute = false;
                start.CreateNoWindow = true;
                start.RedirectStandardOutput = true;
                start.RedirectStandardError = true;
                start.EnvironmentVariables["npm_config_update_notifier"] = "false";
                start.EnvironmentVariables["npm_config_fund"] = "false";
                start.EnvironmentVariables["npm_config_audit"] = "false";
                StringBuilder output = new StringBuilder();
                object outputLock = new object();
                Action<string> capture = delegate(string line)
                {
                    lock (outputLock)
                    {
                        output.AppendLine(line);
                        if (output.Length > 16000) output.Remove(0, output.Length - 16000);
                    }
                    onOutput(line);
                };
                using (Process process = new Process())
                {
                    process.StartInfo = start;
                    process.OutputDataReceived += delegate(object sender, DataReceivedEventArgs e) { if (!String.IsNullOrEmpty(e.Data)) BeginInvoke(new Action(delegate { capture(e.Data); })); };
                    process.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs e) { if (!String.IsNullOrEmpty(e.Data)) BeginInvoke(new Action(delegate { capture(e.Data); })); };
                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                    process.WaitForExit();
                    process.WaitForExit();
                    if (process.ExitCode != 0)
                    {
                        string details;
                        lock (outputLock) details = output.ToString();
                        throw new InvalidOperationException("OmniRoute npm install failed with exit code " + process.ExitCode + ".\r\n" + details);
                    }
                }
            });
        }

        private void OpenDashboard()
        {
            try { Process.Start(new ProcessStartInfo("http://127.0.0.1:20128/") { UseShellExecute = true }); }
            catch (Exception error) { MessageBox.Show(this, error.Message, "Could not open dashboard", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        private void UpdateConnectionStatus()
        {
            if (_connected)
            {
                _connectionLabel.Text = "Connected · " + _model;
                _gatewayStatus.Text = "OmniRoute connected";
            }
            else if (String.IsNullOrEmpty(_connectionLabel.Text) || _connectionLabel.Text.StartsWith("Connected", StringComparison.Ordinal))
            {
                _connectionLabel.Text = "Connect to OmniRoute to start chatting.";
                _gatewayStatus.Text = "OmniRoute not connected";
            }
            if (_homeModelLabel != null) _homeModelLabel.Text = "Selected model: " + _model;
            if (_chatConnectionStatus != null)
            {
                string status = _gatewayStatus.Text ?? String.Empty;
                if (_connected) _chatConnectionStatus.Text = "Connected";
                else if (status.IndexOf("starting", StringComparison.OrdinalIgnoreCase) >= 0 || status.IndexOf("testing", StringComparison.OrdinalIgnoreCase) >= 0)
                    _chatConnectionStatus.Text = "Starting";
                else if (status.IndexOf("error", StringComparison.OrdinalIgnoreCase) >= 0 || status.IndexOf("failed", StringComparison.OrdinalIgnoreCase) >= 0 || status.IndexOf("offline", StringComparison.OrdinalIgnoreCase) >= 0 || status.IndexOf("stopped", StringComparison.OrdinalIgnoreCase) >= 0)
                    _chatConnectionStatus.Text = "Error";
                else _chatConnectionStatus.Text = "Disconnected";
            }
        }

        private string Quote(string value) { return "\"" + value.Replace("\"", "\\\"") + "\""; }
        private string Clip(string value, int maxLength) { return value.Length > maxLength ? value.Substring(0, maxLength) : value; }

        private void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            List<OpenDocument> dirty = _documents.FindAll(delegate(OpenDocument document) { return document.Dirty; });
            if (dirty.Count > 0)
            {
                DialogResult result = MessageBox.Show(this, "There are unsaved files. Close without saving?", "My Omni", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning);
                if (result == DialogResult.Cancel || result == DialogResult.No) { e.Cancel = true; return; }
            }
            _isClosing = true;
            if (_chatCancellation != null) _chatCancellation.Cancel();
            StopOwnedGatewayProcess();
            _highlightTimer.Stop();
            _highlightTimer.Dispose();
            _sidebarAnimationTimer.Stop();
            _sidebarAnimationTimer.Dispose();
            _chatAnimationTimer.Stop();
            _chatAnimationTimer.Dispose();
        }

        private void StopOwnedGatewayProcess()
        {
            Process process = _gatewayProcess;
            _gatewayProcess = null;
            if (process == null) return;
            try
            {
                if (!process.HasExited)
                {
                    process.Kill();
                    if (!process.WaitForExit(5000))
                        MessageBox.Show(this, "The app-owned OmniRoute process did not stop within five seconds.", "OmniRoute shutdown", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (InvalidOperationException) { }
            catch (Exception error)
            {
                MessageBox.Show(this, "Could not stop the app-owned OmniRoute process. It may remain running.\r\n\r\n" + error.Message, "OmniRoute shutdown", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally { process.Dispose(); }
        }
    }

    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}
