using System.Collections.Concurrent;
using System.Text;
using Microsoft.International.Converters.PinYinConverter;
using NPinyin;

namespace MEFrpLauncherX.Core;

public static class PinYinHelper
{
    private static Encoding _gb2312 = Encoding.GetEncoding("GB2312");

    private static readonly ConcurrentDictionary<string, string> _pinyinCache = new();

    /// <summary>
    ///     汉字转全拼
    /// </summary>
    /// <param name="strChinese"></param>
    /// <returns></returns>
    public static string ConvertToAllSpell(string strChinese)
    {
        try
        {
            if (strChinese.Length != 0)
            {
                var fullSpell = new StringBuilder();
                foreach (var chr in strChinese)
                {
                    fullSpell.Append(GetSpell(chr));
                }

                return fullSpell.ToString().ToUpper();
            }
        }
        catch (Exception e)
        {
            App.CurrentLogger.Log("全拼转化出错！" + e.Message);
            App.CurrentLogger.Error(e);
        }

        return string.Empty;
    }

    /// <summary>
    ///     汉字转首字母
    /// </summary>
    /// <param name="strChinese"></param>
    /// <returns></returns>
    public static string GetFirstSpell(string strChinese)
    {
        //NPinyin.Pinyin.GetInitials(strChinese)  有Bug  洺无法识别
        //return NPinyin.Pinyin.GetInitials(strChinese);

        try
        {
            if (strChinese.Length != 0)
            {
                var fullSpell = new StringBuilder();
                foreach (var chr in strChinese)
                {
                    fullSpell.Append(GetSpell(chr)[0]);
                }

                return fullSpell.ToString().ToUpper();
            }
        }
        catch (Exception e)
        {
            App.CurrentLogger.Log("首字母转化出错！" + e.Message);
            App.CurrentLogger.Error(e);
        }

        return string.Empty;
    }

    private static string GetSpell(char chr)
    {
        var coverchr = Pinyin.GetPinyin(chr);

        var isChineses = ChineseChar.IsValidChar(coverchr[0]);
        if (!isChineses)
        {
            return coverchr;
        }

        var chineseChar = new ChineseChar(coverchr[0]);
        foreach (var value in chineseChar.Pinyins)
        {
            if (!string.IsNullOrEmpty(value))
            {
                return value.Remove(value.Length - 1, 1);
            }
        }

        return coverchr;
    }

    /// <summary>
    ///     带缓存的汉字转全拼方法
    /// </summary>
    public static string ConvertToAllSpellWithCache(string strChinese)
    {
        if (string.IsNullOrEmpty(strChinese))
        {
            return string.Empty;
        }

        return _pinyinCache.GetOrAdd(strChinese, key => ConvertToAllSpell(key));
    }

    /// <summary>
    ///     清理拼音缓存
    /// </summary>
    public static void ClearPinyinCache() => _pinyinCache.Clear();
}