// CaptchaService.cs

using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;

namespace MEFrpLauncherX.Services
{
    [Obsolete("This class is obsolete and should not be used.")]
    public class PowCaptchaService
    {
        private const string ApiBaseUrl = "https://captcha.mefrp.com";
        private const string SiteId = "2bf50e050d"; // 从图片中获取的站点ID
        private readonly HttpClient _httpClient;

        public PowCaptchaService()
        {
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
        }

        // 生成多个挑战（根据 JavaScript 代码）
        public List<LocalChallenge> GenerateChallenges(string clientId = "RYCB.PML2", int challengeCount = 4)
        {
            var challenges = new List<LocalChallenge>();
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();

            for (var i = 1; i <= challengeCount; i++)
            {
                var salt = PowSolver.Prng($"{clientId}{timestamp}{i}", 16);
                var target = PowSolver.Prng($"{clientId}{timestamp}{i}d", 4);

                challenges.Add(new LocalChallenge
                {
                    Salt = salt,
                    Target = target,
                    ChallengeIndex = i,
                    ClientId = clientId,
                    Timestamp = timestamp
                });
            }

            return challenges;
        }

        // 解决多个挑战
        public async Task<List<string>> SolveChallengesAsync(List<LocalChallenge> challenges,
            IProgress<string> progress = null, CancellationToken cancellationToken = default)
        {
            var solutions = new List<string>();
            var completed = 0;

            foreach (var challenge in challenges)
            {
                cancellationToken.ThrowIfCancellationRequested();

                progress?.Report($"正在解决挑战 {challenge.ChallengeIndex}/{challenges.Count}...");

                var solution = await Task.Run(() =>
                        PowSolver.SolvePow(challenge.Salt, challenge.Target, 16, null, cancellationToken),
                    cancellationToken
                );

                solutions.Add(solution);
                completed++;

                progress?.Report($"已完成 {completed}/{challenges.Count} 个挑战");
            }

            return solutions;
        }

        // 获取最终令牌（根据图片中的格式）
        public async Task<CaptchaResult> RedeemSolutionsAsync(List<string> solutions, string clientId, string timestamp)
        {
            try
            {
                var payload = new
                {
                    frtoken = GenerateFrToken(clientId, timestamp), // 生成 frtoken
                    solutions // 解决方案数组
                };

                var content = new StringContent(
                    JsonConvert.SerializeObject(payload),
                    Encoding.UTF8,
                    "application/json"
                );

                var response = await _httpClient.PostAsync($"{ApiBaseUrl}/{SiteId}/redeem", content);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var result = JsonConvert.DeserializeObject<CaptchaResult>(responseContent);

                    if (result is { success: true })
                    {
                        return result;
                    }

                    throw new Exception($"验证失败: {result?.message ?? "未知错误"}");
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                throw new Exception($"HTTP错误 {response.StatusCode}: {errorContent}");
            }
            catch (Exception ex)
            {
                throw new Exception($"获取验证令牌失败: {ex.Message}", ex);
            }
        }

        // 生成 frtoken（根据图片中的格式）
        private string GenerateFrToken(string clientId, string timestamp)
        {
            // 根据图片中的格式生成 frtoken
            // 示例: "pb871a3d5987931de8040e9bfb6ao5e57f16ba6fpfc176f034f7"
            var random = new Random();
            var tokenBuilder = new StringBuilder();
            tokenBuilder.Append("pb"); // 前缀

            // 生成 40 个十六进制字符
            for (var i = 0; i < 40; i++)
            {
                tokenBuilder.Append(random.Next(16).ToString("x"));
            }

            tokenBuilder.Append("pfc"); // 中缀

            // 生成 10 个十六进制字符
            for (var i = 0; i < 10; i++)
            {
                tokenBuilder.Append(random.Next(16).ToString("x"));
            }

            return tokenBuilder.ToString();
        }
    }

    public class LocalChallenge
    {
        public string Salt
        {
            get;
            set;
        }

        public string Target
        {
            get;
            set;
        }

        public int ChallengeIndex
        {
            get;
            set;
        }

        public string ClientId
        {
            get;
            set;
        }

        public string Timestamp
        {
            get;
            set;
        }
    }

    public class CaptchaResult
    {
        public bool success
        {
            get;
            set;
        }

        public string token
        {
            get;
            set;
        }

        public string message
        {
            get;
            set;
        }

        public DateTime expires
        {
            get;
            set;
        }
    }

    public static class PowSolver
    {
        // 从 JavaScript 移植的 FNV1a 哈希算法
        private static uint Fnv1a(string str)
        {
            var hash = 0x811C9DC5;
            foreach (var c in str)
            {
                hash ^= c;
                hash += (hash << 1) + (hash << 4) + (hash << 7) + (hash << 8) + (hash << 24);
            }

            return hash;
        }

        // 从 JavaScript 移植的 PRNG（完全一致）
        public static string Prng(string seed, int length)
        {
            var state = Fnv1a(seed);
            var result = new StringBuilder();

            uint Next()
            {
                state ^= state << 13;
                state ^= state >> 17;
                state ^= state << 5;
                return state;
            }

            while (result.Length < length)
            {
                var rnd = Next();
                result.Append(rnd.ToString("X8"));
            }

            return result.ToString()[..length];
        }

        // SHA256 求解器（支持进度反馈和取消）
        public static string SolvePow(string salt, string target, int difficulty = 16,
            IProgress<string> progress = null, CancellationToken cancellationToken = default)
        {
            using (var sha256 = SHA256.Create())
            {
                var targetBytes = HexToBytes(target);
                ulong attempts = 0;
                var lastProgressUpdate = DateTime.Now;

                for (ulong nonce = 0; nonce < ulong.MaxValue; nonce++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    attempts++;

                    // 每 10000 次尝试更新一次进度
                    if (attempts % 10000 == 0 || DateTime.Now - lastProgressUpdate > TimeSpan.FromMilliseconds(200))
                    {
                        var hashRate = (int)(attempts / (DateTime.Now - lastProgressUpdate).TotalSeconds);
                        progress?.Report($"已尝试 {attempts} 次 ({hashRate}/s)...");
                        lastProgressUpdate = DateTime.Now;
                    }

                    var nonceStr = nonce.ToString();
                    var input = salt + nonceStr;
                    var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));

                    if (HashMatchesTarget(hashBytes, targetBytes, difficulty))
                    {
                        progress?.Report($"找到解决方案: {nonce}");
                        return nonceStr;
                    }
                }
            }

            throw new Exception("未能找到有效的 nonce");
        }

        private static bool HashMatchesTarget(byte[] hash, byte[] targetBytes, int targetBits)
        {
            var fullBytes = targetBits / 8;
            var remainingBits = targetBits % 8;

            // 检查完整字节
            for (var i = 0; i < fullBytes; i++)
            {
                if (hash[i] != targetBytes[i])
                {
                    return false;
                }
            }

            // 检查剩余位
            if (remainingBits > 0 && fullBytes < targetBytes.Length)
            {
                var mask = (byte)(0xFF << (8 - remainingBits));
                return (hash[fullBytes] & mask) == (targetBytes[fullBytes] & mask);
            }

            return true;
        }

        private static byte[] HexToBytes(string hex)
        {
            var length = hex.Length;
            var bytes = new byte[length / 2];
            for (var i = 0; i < length; i += 2)
            {
                bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
            }

            return bytes;
        }
    }
}