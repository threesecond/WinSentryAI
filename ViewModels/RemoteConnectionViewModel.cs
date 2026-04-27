using System;
using System.Security;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using WinSentryAI.Services;

namespace WinSentryAI.ViewModels
{
    public partial class RemoteConnectionViewModel : ObservableObject
    {
        private readonly ISettingsService _settingsService;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsConnectEnabled))]
        private string _hostname = string.Empty;

        [ObservableProperty] private string _credentialMode = "Current";
        [ObservableProperty] private string _domain = string.Empty;
        [ObservableProperty] private string _username = string.Empty;
        [ObservableProperty] private string _queryRangeHours = "24";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsConnectEnabled))]
        private bool _isConnecting;

        [ObservableProperty] private string _statusMessage = string.Empty;
        [ObservableProperty] private bool _statusIsSuccess;

        public bool IsConnectEnabled => !IsConnecting && !string.IsNullOrWhiteSpace(Hostname);

        public SecureString? Password { get; set; }
        public RemoteEventLogService? ConnectedService { get; private set; }
        public int QueryHours => int.TryParse(QueryRangeHours, out int h) ? h : 24;

        public IRelayCommand CancelCommand { get; }

        public RemoteConnectionViewModel(ISettingsService settingsService)
        {
            _settingsService = settingsService;
            CancelCommand = new RelayCommand(Cancel);

            Hostname = _settingsService.Get("Remote", "LastHost", string.Empty);
            Domain = _settingsService.Get("Remote", "LastDomain", string.Empty);
            Username = _settingsService.Get("Remote", "LastUsername", string.Empty);
            if (!string.IsNullOrWhiteSpace(Username)) CredentialMode = "Specify";
        }

        public async Task ConnectAsync()
        {
            IsConnecting = true;
            StatusMessage = GetString("Remote_Status_Connecting");
            StatusIsSuccess = false;
            ConnectedService = null;

            try
            {
                SecureString? pass = CredentialMode == "Specify" ? Password : null;
                string? domain = CredentialMode == "Specify" && !string.IsNullOrWhiteSpace(Domain) ? Domain : null;
                string? user = CredentialMode == "Specify" && !string.IsNullOrWhiteSpace(Username) ? Username : null;

                var service = new RemoteEventLogService(Hostname.Trim(), domain, user, pass);

                // Connection test: query last 1 minute, expect no exception = success
                await service.GetRetrospectiveEventsAsync(DateTime.Now.AddMinutes(-1), 1);

                ConnectedService = service;

                _settingsService.Set("Remote", "LastHost", Hostname.Trim());
                _settingsService.Set("Remote", "LastDomain", domain ?? string.Empty);
                _settingsService.Set("Remote", "LastUsername", user ?? string.Empty);
                _settingsService.Save();

                StatusMessage = GetString("Remote_Status_Success");
                StatusIsSuccess = true;

                await Task.Delay(600);
                CloseDialog(true);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Remote connection failed to {Host}", Hostname);
                StatusMessage = $"{GetString("Remote_Status_Failed")}: {ex.Message}";
                StatusIsSuccess = false;
                ConnectedService = null;
            }
            finally
            {
                IsConnecting = false;
            }
        }

        private void Cancel() => CloseDialog(false);

        private static void CloseDialog(bool result)
        {
            foreach (Window win in Application.Current.Windows)
            {
                if (win is RemoteConnectionWindow dialog)
                {
                    dialog.DialogResult = result;
                    dialog.Close();
                    return;
                }
            }
        }

        private static string GetString(string key) =>
            Application.Current.FindResource(key) as string ?? key;
    }
}
