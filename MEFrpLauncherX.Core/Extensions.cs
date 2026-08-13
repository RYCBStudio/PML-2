using Notify.NET.Abstractions;
using Notify.NET.Builder;

namespace MEFrpLauncherX.Core;

public static class NotificationServiceExtensions
{
    extension(INotificationService service)
    {
        public NotificationRequest? RequestNotification(string title, string message, string image = "",
            NotificationUrgency urgency = NotificationUrgency.Normal)
        {
            if (!service.IsSupported)
            {
                App.CurrentLogger.Error($"通知服务不支持当前平台: {Environment.OSVersion}");
                return null;
            }

            var request = NotificationBuilder.Create(title)
                .WithBody(message);
            if (!image.IsNullOrEmpty())
            {
                request.WithImage(image);
            }
            request.WithUrgency(urgency);
            
            return request.Build();
        }
    }
}

public static class StringExtensions
{
    /// <param name="str">待检查的字符串</param>
    extension(string str)
    {
        /// <summary>
        ///     判断字符串是否为空或null
        /// </summary>
        /// <returns>是否为空或null.</returns>
        public bool IsNullOrEmpty() => string.IsNullOrEmpty(str);

        /// <summary>
        ///     将字符串的指定部分大写
        /// </summary>
        /// <param name="startIndex">开始大写的索引</param>
        /// <param name="length">修改的长度</param>
        /// <returns>修改后的字符串</returns>
        public string ToUpper(int startIndex, int length = 1)
        {
            return string.Concat(str.AsSpan(0, startIndex), str.Substring(startIndex, length).ToUpper(),
                str.AsSpan(startIndex + length));
        }

        /// <summary>
        ///     将字符串的指定部分小写
        /// </summary>
        /// <param name="startIndex">开始小写的索引</param>
        /// <param name="length">修改的长度</param>
        /// <returns>修改后的字符串</returns>
        public string ToLower(int startIndex, int length = 1)
        {
            return string.Concat(str.AsSpan(0, startIndex), str.Substring(startIndex, length).ToLower(),
                str.AsSpan(startIndex + length));
        }

        /// <summary>
        ///     判断字符串是否以指定的后缀结尾
        /// </summary>
        /// <param name="suffixes">要判断的后缀, 多个后缀用<c>,</c>分隔</param>
        /// <returns></returns>
        public bool EndsWithEx(string suffixes)
        {
            var possibleSuffix = suffixes.Split(',');
            return possibleSuffix.Any(str.ToLower().EndsWith);
        }
    }
}