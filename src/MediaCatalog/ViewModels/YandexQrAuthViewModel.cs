using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediaCatalog.Services;
using MediaCatalog.Shared.Services;
using Avalonia.Svg.Skia;

namespace MediaCatalog.ViewModels;

public partial class YandexQrAuthViewModel : ViewModelBase, IDialogParticipant
{
    private readonly YandexMusicService _yandexMusicService;
    private CancellationTokenSource? _cts;

    [ObservableProperty]
    private SvgImage? _qrCodeSvg;

    [ObservableProperty]
    private string _qrCodeUrl = "";

    [ObservableProperty]
    private string _statusMessage = "Загрузка QR-кода...";

    [ObservableProperty]
    private bool _isPolling;

    [ObservableProperty]
    private bool _isSuccess;

    [ObservableProperty]
    private bool _isCompleted;

    [ObservableProperty]
    private string _errorMessage = "";

    [ObservableProperty]
    private string _userDisplayName = "";

    [ObservableProperty]
    private string _userAvatarUrl = "";

    public bool WasSuccessful => IsSuccess;

    public YandexQrAuthViewModel(YandexMusicService yandexMusicService)
    {
        _yandexMusicService = yandexMusicService;
    }

    public async Task StartAuthAsync()
    {
        IsPolling = true;
        IsCompleted = false;
        ErrorMessage = "";
        StatusMessage = "Загрузка QR-кода...";

        try
        {
            var qrResult = await _yandexMusicService.GetAuthQRLinkAsync();
            if (qrResult.IsFailure)
            {
                ErrorMessage = qrResult.Error;
                StatusMessage = "Ошибка загрузки QR-кода";
                IsCompleted = true;
                return;
            }

            QrCodeUrl = qrResult.Value;
            await LoadQrCodeSvgAsync(qrResult.Value);
            
            StatusMessage = "Ожидание сканирования...";

            _cts = new CancellationTokenSource();

            await PollForAuthAsync(_cts.Token);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            StatusMessage = "Произошла ошибка";
        }
        finally
        {
            IsPolling = false;
            IsCompleted = true;
        }
    }

    private async Task LoadQrCodeSvgAsync(string url)
    {
        try
        {
            // using var httpClient = new HttpClient();
            // var response = await httpClient.GetAsync(url);
            // response.EnsureSuccessStatusCode();
            //
            // var svgContent = await response.Content.ReadAsStreamAsync();
            
            QrCodeSvg = new SvgImage
            {
                // Source = SvgSource.LoadFromStream(svgContent)
                Source = SvgSource.Load(url)
            };
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Ошибка загрузки QR-кода: {ex.Message}";
        }
    }

    private async Task PollForAuthAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var qrStatus = await _yandexMusicService.AuthorizeByQRAsync();

                    if (qrStatus.Status == Yandex.Music.Api.Models.Account.YAuthStatus.Ok)
                    {
                        StatusMessage = "Авторизация успешна!";
                        IsSuccess = true;

                        var tokenResult = await _yandexMusicService.GetAccessTokenAsync();
                        if (tokenResult.IsFailure)
                        {
                            ErrorMessage = tokenResult.Error;
                            StatusMessage = "Ошибка получения токена";
                            return;
                        }

                        var loginResult = await _yandexMusicService.GetLoginInfoAsync();
                        if (loginResult.IsFailure)
                        {
                            ErrorMessage = loginResult.Error;
                            StatusMessage = "Ошибка получения информации о пользователе";
                            return;
                        }

                        var loginInfo = loginResult.Value;
                        UserDisplayName = !string.IsNullOrWhiteSpace(loginInfo.DisplayName)
                            ? loginInfo.DisplayName
                            : loginInfo.Login ?? "Пользователь";

                        UserAvatarUrl = loginInfo.AvatarUrl;

                        return;
                    }

                    await Task.Delay(1000, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                StatusMessage = "Отменено пользователем";
                break;
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
                StatusMessage = "Произошла ошибка при авторизации";
            }
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        _cts?.Cancel();
    }
}
