using System.Text;
using Microsoft.International.Converters.PinYinConverter;

namespace MEFrpLauncherX.Core;

public static class PinYinHelper
{
    private static Encoding _gb2312 = Encoding.GetEncoding("GB2312");

    /// <summary>
    /// 汉字转全拼
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
            Core.App.CurrentLogger.Log("全拼转化出错！" + e.Message);
            App.CurrentLogger.Error(e);
        }

        return string.Empty;
    }

    /// <summary>
    /// 汉字转首字母
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
            Core.App.CurrentLogger.Log("首字母转化出错！" + e.Message);
            App.CurrentLogger.Error(e);
        }

        return string.Empty;
    }

    private static string GetSpell(char chr)
    {
        var coverchr = NPinyin.Pinyin.GetPinyin(chr);

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
}