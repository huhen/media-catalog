using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using Yandex.Music.Api;
using Yandex.Music.Api.Common;
using Yandex.Music.Api.Common.Debug;
using Yandex.Music.Api.Common.Debug.Writer;
using Yandex.Music.Api.Models.Account;

namespace MediaCatalog.Services;

public class YandexMusicService
{
    private readonly YandexMusicApi _api;
    private readonly AuthStorage _storage;

    public YandexMusicService()
    {
        _api = new YandexMusicApi();
        _storage = new AuthStorage(new DebugSettings(new ConsoleDebugWriter()));
    }

    public async Task<Result<AuthSessionInfo, string>> CreateAuthSessionAsync(string userName)
    {
        try
        {
            var response = await _api.User.CreateAuthSessionAsync(_storage, userName);

            if (response.AuthMethods == null || !response.AuthMethods.Any())
            {
                return Result.Failure<AuthSessionInfo, string>("No authentication methods available");
            }

            var methods = response.AuthMethods.Select(m => MapToYandexAuthMethod(m));

            return Result.Success<AuthSessionInfo, string>(new AuthSessionInfo
            {
                AvailableMethods = methods,
                TrackId = response.TrackId
            });
        }
        catch (Exception ex)
        {
            return Result.Failure<AuthSessionInfo, string>(ex.Message);
        }
    }

    private static YandexAuthMethod MapToYandexAuthMethod(Yandex.Music.Api.Models.Account.YAuthMethod apiMethod)
    {
        return apiMethod switch
        {
            Yandex.Music.Api.Models.Account.YAuthMethod.Password => YandexAuthMethod.Password,
            Yandex.Music.Api.Models.Account.YAuthMethod.Magic => YandexAuthMethod.Magic,
            Yandex.Music.Api.Models.Account.YAuthMethod.MagicToken => YandexAuthMethod.MagicToken,
            Yandex.Music.Api.Models.Account.YAuthMethod.MagicTokenWithPictures => YandexAuthMethod.MagicTokenWithPictures,
            Yandex.Music.Api.Models.Account.YAuthMethod.MagicLink => YandexAuthMethod.MagicLink,
            Yandex.Music.Api.Models.Account.YAuthMethod.Otp => YandexAuthMethod.Otp,
            Yandex.Music.Api.Models.Account.YAuthMethod.Social => YandexAuthMethod.Social,
            Yandex.Music.Api.Models.Account.YAuthMethod.WebAuthN => YandexAuthMethod.WebAuthN,
            Yandex.Music.Api.Models.Account.YAuthMethod.SmsCode => YandexAuthMethod.SmsCode,
            _ => YandexAuthMethod.Password
        };
    }
}

internal class ConsoleDebugWriter : IDebugWriter
{
    public void Error(string requestId, Dictionary<string, List<string>> errors)
    {
        var msg = string.Join(Environment.NewLine,
            errors.Select(x => $"\t{x.Key}: {string.Join("; ", x.Value)}"));
        Debug.WriteLine($"{requestId}:{Environment.NewLine}{msg}");
    }

    public void Clear()
    {
    }

    public string SaveResponse(string url, string message)
    {
        var msg = $"{url}-{message}";
        Debug.WriteLine(msg);
        return msg;
    }
}