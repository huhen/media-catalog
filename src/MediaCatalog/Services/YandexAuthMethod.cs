using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;
using Newtonsoft.Json;

namespace MediaCatalog.Services;

public enum YandexAuthMethod
{
    Password,
    MagicToken,
    MagicTokenWithPictures,
    MagicLink,
    Magic,
    Otp,
    Social,
    WebAuthN,
    SmsCode,
}

public enum YAuthStatus
{
    Ok,
    Error
}

public class YAuthBase
{
    public YAuthStatus Status { get; set; }

    [JsonProperty("redirect_url")]
    public string RedirectUrl { get; set; }

    public List<YAuthError> Errors { get; set; }
}

public class YAuthError
{
    public string Field { get; set; }
    public string Message { get; set; }
}

public class YAuthCaptcha
{
    public string CaptchaKey { get; set; }
    public string ImageUrl { get; set; }
    public string Url { get; set; }
}

public class YAuthQRStatus : YAuthBase
{
    [JsonProperty("default_uid")]
    public int DefaultUid { get; set; }

    public string RetPath { get; set; }

    [JsonProperty("track_id")]
    public string TrackId { get; set; }

    public string Id { get; set; }

    public string State { get; set; }

    public YAuthCaptcha Captcha { get; set; }
}

public class YAccessToken
{
    [JsonProperty("status")]
    public string Status { get; set; }

    [JsonProperty("access_token")]
    public string AccessToken { get; set; }

    [JsonProperty("expires_in")]
    public string Expires { get; set; }

    [JsonProperty("token_type")]
    public string TokenType { get; set; }

    public string Uid { get; set; }
}

public class YLoginInfo
{
    public string Id { get; set; }
    public string Login { get; set; }

    [JsonProperty("client_id")]
    public string ClientId { get; set; }

    [JsonProperty("display_name")]
    public string DisplayName { get; set; }

    [JsonProperty("real_name")]
    public string RealName { get; set; }

    [JsonProperty("first_name")]
    public string FirstName { get; set; }

    [JsonProperty("last_name")]
    public string LastName { get; set; }

    public string Sex { get; set; }

    [JsonProperty("default_email")]
    public string DefaultEmail { get; set; }

    public List<string> Emails { get; set; }

    public string Birthday { get; set; }

    [JsonProperty("default_avatar_id")]
    public string DefaultAvatarId { get; set; }

    [JsonProperty("is_avatar_empty")]
    public bool IsAvatarEmpty { get; set; }

    public string PsuId { get; set; }

    public string AvatarUrl => IsAvatarEmpty
        ? string.Empty
        : $"https://avatars.mds.yandex.net/get-yapic/{DefaultAvatarId}/islands-200";

    public string DisplayNameOrLogin => string.IsNullOrWhiteSpace(DisplayName) ? Login : DisplayName;
}

public static class YandexAuthMethodExtensions
{
    public static string GetDisplayName(this YandexAuthMethod method)
    {
        return method switch
        {
            YandexAuthMethod.Password => "Пароль",
            YandexAuthMethod.MagicToken => "Магический токен",
            YandexAuthMethod.MagicTokenWithPictures => "Магический (с картинками)",
            YandexAuthMethod.MagicLink => "Магическая ссылка",
            YandexAuthMethod.Magic => "Магия",
            YandexAuthMethod.Otp => "OTP",
            YandexAuthMethod.Social => "Соцсети",
            YandexAuthMethod.WebAuthN => "WebAuthN",
            YandexAuthMethod.SmsCode => "SMS код",
            _ => method.ToString()
        };
    }

    public static StreamGeometry GetIcon(this YandexAuthMethod method)
    {
        var iconKey = method switch
        {
            YandexAuthMethod.Password => "PhosphorIcons.LockKeyLight",
            YandexAuthMethod.MagicToken or 
            YandexAuthMethod.MagicTokenWithPictures or 
            YandexAuthMethod.Magic or 
            YandexAuthMethod.MagicLink => "PhosphorIcons.SparkleLight",
            YandexAuthMethod.Otp or 
            YandexAuthMethod.SmsCode => "PhosphorIcons.DeviceMobileCameraLight",
            YandexAuthMethod.Social => "PhosphorIcons.UsersThreeLight",
            YandexAuthMethod.WebAuthN => "PhosphorIcons.ShieldCheckLight",
            _ => "PhosphorIcons.QuestionLight"
        };

        if (Avalonia.Application.Current?.Resources[iconKey] is StreamGeometry geometry)
            return geometry;

        return StreamGeometry.Parse("M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm1 15h-2v-6h2v6zm0-8h-2V7h2v2z");
    }

    public static IEnumerable<YandexAuthMethod> FromApiMethods(IEnumerable<string> apiMethods)
    {
        var result = new List<YandexAuthMethod>();
        
        foreach (var method in apiMethods)
        {
            var normalized = method.ToLowerInvariant().Replace("magic_x_token", "magic_token")
                .Replace("magic_x_token_with_pictures", "magic_token_with_pictures")
                .Replace("magic_link", "magic_link")
                .Replace("social_gg", "social")
                .Replace("sms_code", "sms_code");
            
            if (Enum.TryParse<YandexAuthMethod>(normalized, true, out var parsed))
            {
                result.Add(parsed);
            }
        }
        
        return result;
    }
}

public class AuthSessionInfo
{
    public required IEnumerable<YandexAuthMethod> AvailableMethods { get; init; }
    public string? TrackId { get; init; }
}
