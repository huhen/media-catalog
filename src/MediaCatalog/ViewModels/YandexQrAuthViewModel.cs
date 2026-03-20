using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Mime;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediaCatalog.Services;
using MediaCatalog.Shared.Services;
using Avalonia.Svg.Skia;
using CSharpFunctionalExtensions;
using ShimSkiaSharp;

namespace MediaCatalog.ViewModels;

public partial class YandexQrAuthViewModel : ViewModelBase, IDialogParticipant
{
    private readonly YandexMusicService _yandexMusicService;
    private CancellationTokenSource? _cts;

    [ObservableProperty] private SvgImage? _qrCodeSvg;

    [ObservableProperty] private string _qrCodeUrl = "";

    [ObservableProperty] private string _statusMessage = "Загрузка QR-кода...";

    [ObservableProperty] private bool _isPolling;

    [ObservableProperty] private bool _isSuccess;

    [ObservableProperty] private bool _isCompleted;

    [ObservableProperty] private string _errorMessage = "";

    [ObservableProperty] private string _userDisplayName = "";

    [ObservableProperty] private string _userAvatarUrl = "";

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
            var qrUriResult = await _yandexMusicService.GetAuthQRLinkAsync();
            if (qrUriResult.IsFailure)
            {
                ErrorMessage = qrUriResult.Error;
                StatusMessage = "Ошибка получения QR-кода";
                IsCompleted = true;
                return;
            }

            QrCodeUrl = qrUriResult.Value;

            var qrResult = await LoadQrCodeSvgAsync(qrUriResult.Value);
            if (qrResult.IsFailure)
            {
                ErrorMessage = qrUriResult.Error;
                StatusMessage = "Ошибка загрузки QR-кода";
                IsCompleted = true;
                return;
            }

            QrCodeSvg = qrResult.Value;

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

            _cts?.Dispose();
            _cts = null;
        }
    }

    private async Task<Result<SvgImage, string>> LoadQrCodeSvgAsync(string url)
    {
        try
        {
            // using var httpClient = new HttpClient();
            // var response = await httpClient.GetAsync(url);
            // response.EnsureSuccessStatusCode();
            //
            // var svgContent = await response.Content.ReadAsStringAsync();

            var svgSource = SvgSource.Load(url);

            var isDarkTheme = Application.Current?.ActualThemeVariant == Avalonia.Styling.ThemeVariant.Dark;
            if (isDarkTheme)
            {
                foreach (var cmd in svgSource.Svg?.Model?.Commands?.OfType<DrawPathCanvasCommand>() ?? [])
                {
                    if (cmd.Paint?.Color is not null)
                    {
                        cmd.Paint.Color = new SKColor(255, 255, 255, 255);
                    }
                }

                svgSource.RebuildFromModel();
            }

            return new SvgImage { Source = svgSource };
        }
        catch (Exception ex)
        {
            return Result.Failure<SvgImage, string>(ex.Message);
        }
    }

    private async Task PollForAuthAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(1000, cancellationToken);

                var qrStatus = await _yandexMusicService.AuthorizeByQRAsync();

                if (qrStatus.Status != Yandex.Music.Api.Models.Account.YAuthStatus.Ok)
                    continue;

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