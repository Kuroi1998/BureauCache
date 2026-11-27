// BureauCache — sous-vues de connexion du panneau de paramètres rapides.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

partial class QuickSettingsForm
{
    sealed class ConnectionRow
    {
        public string Key, Name, Detail, Action;
        public bool Password, Pairing, OpenBluetoothSettings;
        public int Auth;   // authentification du réseau Wi-Fi, pour valider le mot de passe (CDX-49)
        public Func<string, IntPtr, CancellationToken, bool> Change;
        public override string ToString() { return Name + " · " + Detail; }
    }

    sealed class ConnectionSnapshot
    {
        public readonly List<ConnectionRow> Rows = new List<ConnectionRow>();
        public string Message;
        public bool LocationDenied;
    }

    Panel connectionPage;
    Control[] visibleHome;
    bool wifiPage, homeScroll;
    CancellationTokenSource connectionWork;
    ListBox connectionList;
    TextBox connectionPassword;
    Panel passwordRow;
    Button connectionAction, connectionRefresh, connectionSettings, connectionPrivacy;
    Label connectionStatus;

    internal bool ShowingConnections { get { return connectionPage != null; } }

    void ShowConnections(bool wifi)
    {
        if (connectionPage != null) return;
        refresh.Stop();
        FlushLevel(volume);      // le minuteur s'arrête : la dernière position du curseur part maintenant (CDX-46)
        FlushLevel(brightness);
        wifiPage = wifi;
        visibleHome = Controls.Cast<Control>().Where(control => control.Visible).ToArray();
        AutoScrollPosition = Point.Empty;
        AutoScrollMinSize = Size.Empty;
        AutoScroll = false;
        foreach (Control control in visibleHome) control.Hide();

        connectionPage = new Panel { Dock = DockStyle.Fill, Padding = new Padding(Pad), BackColor = Theme.Bg };
        TableLayoutPanel layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 6, Margin = Padding.Empty };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        for (int i = 3; i < layout.RowCount; i++) layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var header = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 3, Margin = Padding.Empty };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        Button back = ConnectionButton(L.T("Retour", "Back", "Volver"), HideConnections);
        connectionRefresh = ConnectionButton(L.T("Actualiser", "Refresh", "Actualizar"), () => LoadConnections(true));
        Label title = ConnectionLabel(wifi ? "Wi-Fi" : "Bluetooth", true);
        title.Dock = DockStyle.Fill;
        header.Controls.Add(back, 0, 0);
        header.Controls.Add(title, 1, 0);
        header.Controls.Add(connectionRefresh, 2, 0);
        layout.Controls.Add(header, 0, 0);

        connectionStatus = ConnectionLabel(string.Empty, false);
        connectionStatus.Dock = DockStyle.Fill;
        connectionStatus.Margin = new Padding(0, Gap, 0, Gap);
        connectionStatus.MinimumSize = new Size(0, 2 * connectionStatus.Font.Height);
        layout.Controls.Add(connectionStatus, 0, 1);

        connectionList = new ListBox
        {
            AccessibleName = wifi ? L.T("Réseaux Wi-Fi disponibles", "Available Wi-Fi networks", "Redes Wi-Fi disponibles") :
                L.T("Appareils Bluetooth", "Bluetooth devices", "Dispositivos Bluetooth"),
            Dock = DockStyle.Fill, BorderStyle = BorderStyle.None, IntegralHeight = false,
            BackColor = Theme.Card, ForeColor = Theme.Fg, Font = Ui.Font("Segoe UI", 10f),
            DrawMode = DrawMode.OwnerDrawFixed, Margin = Padding.Empty
        };
        connectionList.ItemHeight = 2 * connectionList.Font.Height + Ui.S(12);
        connectionList.DrawItem += DrawConnectionRow;
        connectionList.SelectedIndexChanged += (s, e) => { connectionPassword.Clear(); UpdateConnectionAction(); };
        layout.Controls.Add(connectionList, 0, 2);

        passwordRow = new Panel { Dock = DockStyle.Top, Height = Ui.S(34), Margin = new Padding(0, Gap, 0, 0), Visible = false };
        Label passwordLabel = ConnectionLabel(L.T("Mot de passe", "Password", "Contraseña"), false);
        passwordLabel.Dock = DockStyle.Left;
        connectionPassword = new TextBox
        {
            Dock = DockStyle.Fill, UseSystemPasswordChar = true, MaxLength = 64, AccessibleName = passwordLabel.Text,
            BackColor = Theme.Card, ForeColor = Theme.Fg, Font = Ui.Font("Segoe UI", 10f)
        };
        connectionPassword.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; ChangeConnection(); }
        };
        passwordRow.Controls.Add(connectionPassword);
        passwordRow.Controls.Add(passwordLabel);
        layout.Controls.Add(passwordRow, 0, 3);

        var footer = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true, Margin = new Padding(0, Gap, 0, 0) };
        connectionAction = ConnectionButton(string.Empty, ChangeConnection);
        connectionSettings = ConnectionButton(L.T("Paramètres Windows", "Windows settings", "Configuración de Windows"),
            () => Launch(wifi ? "ms-settings:network-wifi" : "ms-settings:bluetooth", wifi ? "ms-settings:network-status" : null));
        connectionPrivacy = ConnectionButton(L.T("Autoriser la localisation", "Allow location access", "Permitir la ubicación"),
            () => Launch("ms-settings:privacy-location", null));
        connectionPrivacy.Visible = false;
        footer.Controls.Add(connectionAction);
        footer.Controls.Add(connectionSettings);
        layout.Controls.Add(footer, 0, 4);
        layout.Controls.Add(connectionPrivacy, 0, 5);

        connectionPage.Controls.Add(layout);
        Controls.Add(connectionPage);
        connectionPage.BringToFront();
        back.Select();
        UpdateConnectionAction();
        LoadConnections(true);
    }

    Button ConnectionButton(string caption, Action click)
    {
        Button button = new Button
        {
            Text = caption, AutoSize = true, Padding = new Padding(Gap, 0, Gap, 0), MinimumSize = new Size(0, Ui.S(33)),
            Margin = new Padding(0, 0, Gap, Gap / 2), FlatStyle = FlatStyle.Flat, UseVisualStyleBackColor = false,
            BackColor = Theme.Surface, ForeColor = Theme.Fg, Font = Ui.Font("Segoe UI", 9.5f)
        };
        button.FlatAppearance.BorderColor = Theme.Line;
        button.Click += (s, e) => click();
        return button;
    }

    static Label ConnectionLabel(string caption, bool heading)
    {
        return new Label
        {
            Text = caption, AutoSize = true, UseMnemonic = false, TextAlign = ContentAlignment.MiddleLeft,
            Font = Ui.Font(heading ? "Segoe UI Semibold" : "Segoe UI", heading ? 12f : 9.5f),
            ForeColor = heading ? Theme.Fg : Theme.FgDim
        };
    }

    void DrawConnectionRow(object sender, DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= connectionList.Items.Count) return;
        ConnectionRow row = (ConnectionRow)connectionList.Items[e.Index];
        bool selected = (e.State & DrawItemState.Selected) != 0;
        Rectangle bounds = Rectangle.Inflate(e.Bounds, -Gap, -Ui.S(3));
        using (var background = new SolidBrush(selected ? Theme.Hover : Theme.Card)) e.Graphics.FillRectangle(background, e.Bounds);
        const TextFormatFlags flags = TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix |
            TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine;
        Rectangle name = new Rectangle(bounds.X, bounds.Y, bounds.Width, bounds.Height / 2);
        Rectangle info = new Rectangle(bounds.X, name.Bottom, bounds.Width, bounds.Height - name.Height);
        TextRenderer.DrawText(e.Graphics, row.Name, e.Font, name, Theme.SelText(selected, Theme.Fg), flags);
        TextRenderer.DrawText(e.Graphics, row.Detail, Ui.Font("Segoe UI", 9f), info, Theme.SelText(selected, Theme.FgDim), flags);
        e.DrawFocusRectangle();
    }

    void UpdateConnectionAction()
    {
        var row = connectionList.SelectedItem as ConnectionRow;
        connectionAction.Visible = row != null && (row.Change != null || row.OpenBluetoothSettings);
        connectionAction.Enabled = connectionWork == null;
        connectionAction.Text = row == null ? string.Empty : row.Action;
        passwordRow.Visible = row != null && row.Password;
        connectionPassword.Enabled = connectionWork == null;
    }

    void LoadConnections(bool scan)
    {
        RunConnectionWork(scan, null, null);
    }

    void ChangeConnection()
    {
        var row = connectionList.SelectedItem as ConnectionRow;
        if (row == null || connectionWork != null) return;
        if (row.OpenBluetoothSettings) { Launch("ms-settings:bluetooth", null); return; }
        if (row.Change == null) return;
        string password = row.Password ? connectionPassword.Text : null;
        if (row.Password && !Native.ValidQuickWifiPassword(password, row.Auth))
        {
            connectionStatus.Text = L.T("Le mot de passe doit contenir de 8 à 63 caractères ASCII, ou 64 chiffres hexadécimaux.",
                "Enter 8 to 63 ASCII characters, or 64 hexadecimal digits.",
                "Introduce entre 8 y 63 caracteres ASCII, o 64 dígitos hexadecimales.");
            connectionPassword.Focus();
            return;
        }
        connectionPassword.Clear();
        RunConnectionWork(false, row, password);
    }

    async void RunConnectionWork(bool scan, ConnectionRow change, string password)
    {
        if (Sandbox.Active || connectionWork != null) return;
        bool wifi = wifiPage;
        var selected = connectionList.SelectedItem as ConnectionRow;
        string selection = selected == null ? null : selected.Key;
        IntPtr owner = change != null && change.Pairing ? Handle : IntPtr.Zero;
        CancellationTokenSource work = new CancellationTokenSource();
        connectionWork = work;
        connectionRefresh.Enabled = connectionList.Enabled = false;
        UpdateConnectionAction();
        connectionStatus.Text = change != null ? L.T("Opération en cours…", "Working…", "Operación en curso…") :
            L.T("Recherche en cours…", "Searching…", "Buscando…");
        ConnectionSnapshot snapshot;
        string operationMessage = null;
        try
        {
            snapshot = await Task.Run(() =>
            {
                if (change != null)
                {
                    bool changed = false;
                    try { changed = change.Change(password, owner, work.Token); } catch (Exception) { }
                    operationMessage = changed ? L.T("Opération terminée.", "Action complete.", "Operación completada.") :
                        L.T("Connexion non confirmée. Réessaie ou ouvre les paramètres Windows.",
                            "Connection not confirmed. Retry or open Windows settings.",
                            "Conexión sin confirmar. Reintenta o abre la configuración de Windows.");
                }
                if (work.IsCancellationRequested) return new ConnectionSnapshot();
                return wifi ? WifiSnapshot(scan, work.Token) : BluetoothSnapshot(work.Token);
            });
        }
        catch (Exception)
        {
            snapshot = new ConnectionSnapshot { Message = L.T("Windows ne renvoie pas la liste.", "Windows did not return the list.", "Windows no devolvió la lista.") };
        }
        finally { work.Dispose(); }
        if (connectionWork != work) return;
        connectionWork = null;
        ShowConnectionResults(snapshot, selection, operationMessage);
    }

    void ShowConnectionResults(ConnectionSnapshot snapshot, string selection, string operationMessage)
    {
        connectionRefresh.Enabled = connectionList.Enabled = true;
        connectionList.BeginUpdate();
        try
        {
            connectionList.Items.Clear();
            foreach (ConnectionRow row in snapshot.Rows) connectionList.Items.Add(row);
            if (selection != null)
                connectionList.SelectedIndex = snapshot.Rows.FindIndex(row => row.Key == selection);
        }
        finally { connectionList.EndUpdate(); }
        connectionPrivacy.Visible = snapshot.LocationDenied;
        connectionStatus.Text = operationMessage ?? snapshot.Message;
        UpdateConnectionAction();
    }

    void CancelConnections()
    {
        if (connectionWork != null) connectionWork.Cancel();
        connectionWork = null;
    }

    void HideConnections()
    {
        CancelConnections();
        connectionPage.Dispose();
        connectionPage = null;
        ApplyHomeScrolling();
        foreach (Control control in visibleHome) control.Show();
        if (!Sandbox.Active) { RefreshControls(); refresh.Start(); }
    }

    static ConnectionSnapshot WifiSnapshot(bool scan, CancellationToken cancellation)
    {
        List<Native.QuickWifiNetwork> networks = new List<Native.QuickWifiNetwork>();
        Native.QuickWifiStatus status = Native.ReadQuickWifi(scan, networks, cancellation);
        ConnectionSnapshot result = new ConnectionSnapshot();
        result.LocationDenied = status == Native.QuickWifiStatus.AccessDenied;
        var choices = networks.GroupBy(n => BitConverter.ToString(n.Ssid) + "/" + n.Auth + "/" + n.Cipher + "/" + n.BssType)
            .Select(group => group.OrderByDescending(n => n.Connected).ThenByDescending(n => n.CanJoin)
                .ThenByDescending(n => n.Profile != null).ThenByDescending(n => n.Signal).First())
            .OrderByDescending(n => n.Connected).ThenByDescending(n => n.Signal).ThenBy(n => n.Name);
        foreach (Native.QuickWifiNetwork network in choices)
        {
            string state = network.Connected ? L.T("Connecté", "Connected", "Conectada") :
                network.Profile != null ? L.T("Enregistré", "Saved", "Guardada") :
                network.Secured ? L.T("Sécurisé", "Secured", "Protegida") : L.T("Ouvert", "Open", "Abierta");
            if (!network.Connected && !network.CanJoin) state += L.T(" · Paramètres Windows", " · Windows settings", " · Configuración de Windows");
            result.Rows.Add(new ConnectionRow
            {
                Key = BitConverter.ToString(network.Ssid) + "/" + network.Auth + "/" + network.Cipher + "/" + network.BssType,
                Name = network.Name, Detail = state + " · " + network.Signal.ToString(L.Culture) + " %",
                Password = !network.Connected && network.CanJoin && network.NeedsPassword, Auth = network.Auth,
                Action = network.Connected ? L.T("Déconnecter", "Disconnect", "Desconectar") : L.T("Connecter", "Connect", "Conectar"),
                Change = network.Connected || network.CanJoin ?
                    (Func<string, IntPtr, CancellationToken, bool>)((password, owner, stop) => Native.ChangeQuickWifi(network, password, stop)) : null
            });
        }
        result.Message = result.LocationDenied ?
            L.T("Windows exige l’accès à la localisation pour afficher les réseaux.", "Windows requires location access to show networks.",
                "Windows necesita acceso a la ubicación para mostrar las redes.") :
            status == Native.QuickWifiStatus.NoAdapter ?
                L.T("Aucune carte Wi-Fi détectée.", "No Wi-Fi adapter detected.", "No se detectó un adaptador Wi-Fi.") :
            status != Native.QuickWifiStatus.Ready ?
                L.T("Wi-Fi indisponible. Vérifie qu’il est activé.", "Wi-Fi unavailable. Check that it is on.", "Wi-Fi no disponible. Comprueba que esté activado.") :
            result.Rows.Count == 0 ? L.T("Aucun réseau détecté.", "No networks found.", "No se encontraron redes.") :
            L.T("Sélectionne un réseau.", "Select a network.", "Selecciona una red.");
        return result;
    }

    static ConnectionSnapshot BluetoothSnapshot(CancellationToken cancellation)
    {
        List<Native.QuickBluetoothDevice> devices = new List<Native.QuickBluetoothDevice>();
        ConnectionSnapshot result = new ConnectionSnapshot();
        if (!Native.ReadQuickBluetooth(devices, cancellation))
        {
            result.Message = L.T("Bluetooth indisponible. Vérifie qu’il est activé.", "Bluetooth unavailable. Check that it is on.",
                "Bluetooth no disponible. Comprueba que esté activado.");
            return result;
        }
        foreach (Native.QuickBluetoothDevice device in devices.OrderByDescending(d => d.Connected).ThenByDescending(d => d.Paired).ThenBy(d => d.Name))
        {
            bool canPair = !device.Paired && device.CanPair;
            bool reconnect = device.Paired && !device.Connected;
            result.Rows.Add(new ConnectionRow
            {
                Key = device.Id, Name = string.IsNullOrWhiteSpace(device.Name) ? L.T("Appareil sans nom", "Unnamed device", "Dispositivo sin nombre") : device.Name,
                Detail = device.Connected ? L.T("Connecté", "Connected", "Conectado") :
                    device.Paired ? L.T("Associé · hors connexion", "Paired · disconnected", "Vinculado · desconectado") :
                    L.T("À proximité", "Nearby", "Cercano"),
                Action = canPair ? L.T("Associer", "Pair", "Vincular") :
                    reconnect ? L.T("Connecter via Windows", "Connect in Windows", "Conectar en Windows") : null,
                Pairing = canPair, OpenBluetoothSettings = reconnect,
                Change = canPair ? (Func<string, IntPtr, CancellationToken, bool>)((unused, owner, stop) => Native.PairQuickBluetooth(device, owner, stop)) : null
            });
        }
        result.Message = result.Rows.Count == 0 ?
            L.T("Aucun appareil détecté. Active son mode d’appairage et actualise.", "No devices found. Enable pairing mode and refresh.",
                "No se encontraron dispositivos. Activa el modo de vinculación y actualiza.") :
            L.T("Sélectionne un appareil à associer.", "Select a device to pair.", "Selecciona un dispositivo para vincular.");
        return result;
    }
}
