namespace MEFrpLauncherX.Tools;

using System;
using System.Text.RegularExpressions;

public static partial class EmailValidator
{
    private static readonly Regex EmailProviderRegexFull = MyRegex();

    public static bool IsValidCommonEmail(string email)
    {
        if (string.IsNullOrEmpty(email))
            return false;

        return EmailProviderRegexFull.IsMatch(email);
    }

    [GeneratedRegex(@"@(163\.com|qq\.com|126\.com|vip\.163\.com|vip\.126\.com|vip\.qq\.com|sina\.(com|cn)|sina\.com\.cn|vip\.sina\.com|sohu\.com|gmail\.com|googlemail\.com|hotmail\.com|outlook\.com|live\.(com|cn|hk)|msn\.com|yahoo\.(com|cn|com\.cn|co\.jp)|foxmail\.com|aliyun\.com|yeah\.net|netease\.com|tom\.com|21cn\.com|139\.com|189\.cn|wo\.cn|10086\.cn|icloud\.com|me\.com|mac\.com|mail\.com|aol\.com|zoho\.com|yandex\.com|protonmail\.com|gmx\.com|naver\.com|daum\.net|hanmail\.net|rediffmail\.com|163\.com\.hk|263\.net|chinaren\.com|mefrp\.com)$", RegexOptions.IgnoreCase)]
    private static partial Regex MyRegex();
}