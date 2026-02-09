using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using MEFrpLauncherX.Core.MEFIntergrated;
using Newtonsoft.Json;

namespace MEFrpLauncherX.Core.Storage;

internal static class SecureStorage
{
    private static readonly string StorageDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "PML2",
        "users"
    );

    private static readonly string KeyDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PML2",
        "keys"
    );

    // Cache for encryption keys per user
    private static readonly Dictionary<string, byte[]> KeyCache = new();

    public static void SaveUserInfo(InfoClasses.UserInfo info, TimeSpan expiry, string username = null)
    {
        if (info == null)
        {
            throw new ArgumentNullException(nameof(info));
        }

        if (string.IsNullOrEmpty(info.token))
        {
            throw new ArgumentException("Token cannot be null or empty");
        }

        if (expiry <= TimeSpan.Zero)
        {
            throw new ArgumentException("Expiry must be positive");
        }

        // Use username if provided, otherwise use a default key
        username = string.IsNullOrEmpty(username) ? "default" : username;
        var storagePath = GetUserStoragePath(username);

        try
        {
            // 1. Prepare data (with expiry)
            var data = new
            {
                Data = info,
                Expiry = DateTime.UtcNow.Add(expiry),
                Username = username
            };

            // 2. Serialize
            var json = JsonConvert.SerializeObject(data);

            // 3. Generate random IV
            var iv = GenerateRandomIV();

            // 4. Encrypt data
            byte[] encryptedData;
            using (var aes = Aes.Create())
            {
                aes.Key = GetOrCreateSecureKey(username);
                aes.IV = iv;

                using var ms = new MemoryStream();
                // Write IV first (16 bytes)
                ms.Write(iv, 0, iv.Length);

                // Write encrypted data
                using (var cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
                {
                    var plainBytes = Encoding.UTF8.GetBytes(json);
                    cs.Write(plainBytes, 0, plainBytes.Length);
                }
                encryptedData = ms.ToArray();
            }

            // 5. Save to file
            Directory.CreateDirectory(Path.GetDirectoryName(storagePath));
            File.WriteAllBytes(storagePath, encryptedData);
        }
        catch (Exception ex)
        {
            throw new SecurityException("Failed to encrypt and save user data", ex);
        }
    }

    public static InfoClasses.UserInfo LoadUserInfo(string username = null)
    {
        username = string.IsNullOrEmpty(username) ? "default" : username;
        var storagePath = GetUserStoragePath(username);

        if (!File.Exists(storagePath))
        {
            return null;
        }

        try
        {
            // 1. Read encrypted data
            var encryptedData = File.ReadAllBytes(storagePath);

            // 2. Separate IV (first 16 bytes) and actual encrypted data
            var iv = new byte[16];
            var cipherText = new byte[encryptedData.Length - 16];
            Buffer.BlockCopy(encryptedData, 0, iv, 0, 16);
            Buffer.BlockCopy(encryptedData, 16, cipherText, 0, cipherText.Length);

            // 3. Decrypt data
            string json;
            using (var aes = Aes.Create())
            {
                aes.Key = GetOrCreateSecureKey(username);
                aes.IV = iv;

                using var ms = new MemoryStream(cipherText);
                using var cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Read);
                using var sr = new StreamReader(cs);
                json = sr.ReadToEnd();
            }

            // 4. Deserialize and check expiry
            var result = JsonConvert.DeserializeObject<dynamic>(json);
            DateTime expiry = result.Expiry;

            if (expiry < DateTime.UtcNow)
            {
                ClearUserInfo(username); // Auto-clean expired data
                return null;
            }

            return result.Data.ToObject<InfoClasses.UserInfo>();
        }
        catch (CryptographicException ex)
        {
            // Possible key change or data corruption
            ClearUserInfo(username);
            throw new SecurityException("Decryption failed - possible key change or data corruption", ex);
        }
        catch (Exception ex)
        {
            throw new SecurityException("Failed to load user data", ex);
        }
    }

    public static void ClearUserInfo(string username = null)
    {
        username = string.IsNullOrEmpty(username) ? "default" : username;
        var storagePath = GetUserStoragePath(username);

        if (File.Exists(storagePath))
        {
            File.Delete(storagePath);
        }
    }

    public static List<string> GetStoredUsernames()
    {
        if (!Directory.Exists(StorageDirectory))
        {
            return [];
        }

        var files = Directory.GetFiles(StorageDirectory, "*.dat");
        var usernames = new List<string>();

        foreach (var file in files)
        {
            usernames.Add(Path.GetFileNameWithoutExtension(file));
        }

        return usernames;
    }

    private static byte[] GetOrCreateSecureKey(string username)
    {
        if (KeyCache.TryGetValue(username, out var cachedKey))
        {
            return cachedKey;
        }

        var keyPath = GetKeyPath(username);

        try
        {
            // Try to load existing key
            if (File.Exists(keyPath))
            {
                var keyBytes = File.ReadAllBytes(keyPath);
                
                // On Windows, use DPAPI for additional protection
                if (OperatingSystem.IsWindows())
                {
                    keyBytes = ProtectedData.Unprotect(keyBytes, null, DataProtectionScope.CurrentUser);
                }
                
                KeyCache[username] = keyBytes;
                return keyBytes;
            }

            // Generate new key (32 bytes = 256 bits)
            var newKey = new byte[32];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(newKey);
            }

            // On Windows, use DPAPI for additional protection
            if (OperatingSystem.IsWindows())
            {
                var protectedKey = ProtectedData.Protect(
                    newKey,
                    null,
                    DataProtectionScope.CurrentUser
                );
                Directory.CreateDirectory(Path.GetDirectoryName(keyPath));
                File.WriteAllBytes(keyPath, protectedKey);
            }
            else
            {
                // On Linux, just store the key with restricted permissions
                Directory.CreateDirectory(Path.GetDirectoryName(keyPath));
                File.WriteAllBytes(keyPath, newKey);
                File.SetAttributes(keyPath, FileAttributes.Normal);
                try
                {
                    // Try to restrict file permissions (Unix-only)
                    if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
                    {
                        File.SetUnixFileMode(keyPath, 
                            UnixFileMode.UserRead | UnixFileMode.UserWrite);
                    }
                }
                catch
                {
                    // If we can't set permissions, continue anyway
                }
            }

            KeyCache[username] = newKey;
            return newKey;
        }
        catch (Exception ex)
        {
            throw new SecurityException("Failed to create or retrieve encryption key", ex);
        }
    }

    private static byte[] GenerateRandomIV()
    {
        var iv = new byte[16]; // AES IV is always 16 bytes
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(iv);
        }
        return iv;
    }

    private static string GetUserStoragePath(string username)
    {
        Directory.CreateDirectory(StorageDirectory);
        return Path.Combine(StorageDirectory, $"{username}.dat");
    }

    private static string GetKeyPath(string username)
    {
        Directory.CreateDirectory(KeyDirectory);
        return Path.Combine(KeyDirectory, $"{username}.key");
    }
}

public static class UserCache
{
    // Simple in-process cache that is AOT/trimming-friendly (no MemoryCache/System.Runtime.Caching)
    // Keyed by normalized username.
    private static readonly ConcurrentDictionary<string, CacheEntry> _cache = new();

    private const string CacheKeyPrefix = "UserInfo_";
    private const string DefaultUsername = "default";

    // Inform linker/trimmer that UserInfo's public constructors/properties/fields are required
    // by serializers or other reflective consumers. Adjust the flags if your serializer
    // needs non-public members too (or prefer an XML linker descriptor).
    private const DynamicallyAccessedMemberTypes PreserveUserInfoMembers =
        DynamicallyAccessedMemberTypes.PublicConstructors
        | DynamicallyAccessedMemberTypes.PublicProperties
        | DynamicallyAccessedMemberTypes.PublicFields;

    private sealed class CacheEntry
    {
        public InfoClasses.UserInfo User { get; }
        public DateTimeOffset Expires { get; }

        public CacheEntry(InfoClasses.UserInfo user, DateTimeOffset expires)
        {
            User = user;
            Expires = expires;
        }
    }

    public static InfoClasses.UserInfo? CurrentUser
    {
        get => GetUserInfo();
        set => SetUserInfo(value);
    }

    [return: DynamicallyAccessedMembers(PreserveUserInfoMembers)]
    public static InfoClasses.UserInfo? GetUserInfo(string? username = null)
    {
        username = NormalizeUsername(username);
        var cacheKey = $"{CacheKeyPrefix}{username}";

        // 1. Try in-process cache
        if (_cache.TryGetValue(cacheKey, out var entry))
        {
            if (entry.Expires > DateTimeOffset.UtcNow)
            {
                return entry.User;
            }

            // expired -> remove
            _cache.TryRemove(cacheKey, out _);
        }

        // 2. Load from secure storage
        var storedUser = SecureStorage.LoadUserInfo(username);
        if (storedUser != null)
        {
            // 3. Put in cache with configured expiry
            var expireDays = Math.Max(0, ConfigManager.CurrentConfig?.ExpireDays ?? 0);
            var expiresAt = DateTimeOffset.UtcNow.AddDays(expireDays);
            var newEntry = new CacheEntry(storedUser, expiresAt);
            _cache[cacheKey] = newEntry;
        }

        return storedUser;
    }

    public static void SetUserInfo([DynamicallyAccessedMembers(PreserveUserInfoMembers)] InfoClasses.UserInfo? user, string? username = null)
    {
        if (user == null)
        {
            Logout(username);
            return;
        }

        username = NormalizeUsername(username);
        var cacheKey = $"{CacheKeyPrefix}{username}";

        var expireDays = Math.Max(0, ConfigManager.CurrentConfig?.ExpireDays ?? 0);
        var expiresAt = DateTimeOffset.UtcNow.AddDays(expireDays);

        // 1. Update in-process cache
        var entry = new CacheEntry(user, expiresAt);
        _cache[cacheKey] = entry;

        // 2. Update secure storage
        SecureStorage.SaveUserInfo(user, TimeSpan.FromDays(expireDays), username);
    }

    public static void Logout(string? username = null)
    {
        username = NormalizeUsername(username);
        var cacheKey = $"{CacheKeyPrefix}{username}";

        // 1. Clear in-process cache
        _cache.TryRemove(cacheKey, out _);

        // 2. Clear secure storage
        SecureStorage.ClearUserInfo(username);
    }

    public static bool IsLoggedIn(string? username = null)
    {
        return GetUserInfo(username) != null;
    }

    public static List<string> GetStoredUsernames()
    {
        return SecureStorage.GetStoredUsernames();
    }

    private static string NormalizeUsername(string? username)
    {
        return string.IsNullOrWhiteSpace(username) ? DefaultUsername : username!;
    }
}