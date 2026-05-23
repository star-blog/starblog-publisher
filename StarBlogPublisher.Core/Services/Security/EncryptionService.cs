using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace StarBlogPublisher.Services.Security;

public static class EncryptionService {
    // 使用机器特定信息作为加密密钥的一部分，增加破解难度
    private static readonly byte[] EntropyBytes = GetMachineSpecificBytes();

    // 用于非Windows平台的加密密钥
    private static readonly byte[] EncryptionKey = GetEncryptionKey();

    public static string Encrypt(string plainText) {
        if (string.IsNullOrEmpty(plainText))
            return string.Empty;

        var plainBytes = Encoding.UTF8.GetBytes(plainText);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
            // Windows平台使用ProtectedData
            var encryptedBytes = ProtectedData.Protect(
                plainBytes, EntropyBytes, DataProtectionScope.CurrentUser
            );
            return Convert.ToBase64String(encryptedBytes);
        }
        else {
            // 非Windows平台使用AES加密
            return EncryptWithAes(plainBytes, EncryptionKey);
        }
    }

    public static string Decrypt(string encryptedText) {
        if (string.IsNullOrEmpty(encryptedText))
            return string.Empty;

        try {
            var encryptedBytes = Convert.FromBase64String(encryptedText);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                // Windows平台使用ProtectedData
                var plainBytes =
                    ProtectedData.Unprotect(
                        encryptedBytes,
                        EntropyBytes,
                        DataProtectionScope.CurrentUser
                    );
                return Encoding.UTF8.GetString(plainBytes);
            }
            else {
                // 非Windows平台使用AES解密
                return DecryptWithAes(encryptedText, EncryptionKey);
            }
        }
        catch {
            // 解密失败时返回空字符串
            return string.Empty;
        }
    }

    private static byte[] GetMachineSpecificBytes() {
        // 使用机器名和用户名的组合作为熵值
        var entropySource = Environment.MachineName + Environment.UserName;
        return SHA256.HashData(Encoding.UTF8.GetBytes(entropySource));
    }

    private static byte[] GetEncryptionKey() {
        // 为AES加密生成密钥，基于机器特定信息
        var keySource = Environment.MachineName + Environment.UserName +
                        Environment.OSVersion + Environment.ProcessorCount;
        return SHA256.HashData(Encoding.UTF8.GetBytes(keySource));
    }

    private static string EncryptWithAes(byte[] plainBytes, byte[] key) {
        using var aes = Aes.Create();
        aes.Key = key;
        // 生成随机IV并将其作为加密数据的一部分
        aes.GenerateIV();

        using var ms = new MemoryStream();
        // 先写入IV
        ms.Write(aes.IV, 0, aes.IV.Length);

        using (var cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write)) {
            cs.Write(plainBytes, 0, plainBytes.Length);
            cs.FlushFinalBlock();
        }

        return Convert.ToBase64String(ms.ToArray());
    }

    private static string DecryptWithAes(string encryptedText, byte[] key) {
        var encryptedBytes = Convert.FromBase64String(encryptedText);

        using var aes = Aes.Create();
        aes.Key = key;

        // 从加密数据中提取IV
        var iv = new byte[aes.BlockSize / 8];
        Array.Copy(encryptedBytes, 0, iv, 0, iv.Length);
        aes.IV = iv;

        using var ms = new MemoryStream();
        using (
            var cs = new CryptoStream(new MemoryStream(
                    encryptedBytes,
                    iv.Length,
                    encryptedBytes.Length - iv.Length),
                aes.CreateDecryptor(),
                CryptoStreamMode.Read)
        ) {
            var buffer = new byte[1024];
            int bytesRead;
            while ((bytesRead = cs.Read(buffer, 0, buffer.Length)) > 0) {
                ms.Write(buffer, 0, bytesRead);
            }
        }

        return Encoding.UTF8.GetString(ms.ToArray());
    }
}