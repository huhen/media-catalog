using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;

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
