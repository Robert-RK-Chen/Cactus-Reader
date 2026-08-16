using Cactus_Reader.Entities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Cactus_Reader.Sources.ToolKits
{
    /// <summary>
    /// CactusReaderServer API 客户端。
    /// 客户端不再直连 MySQL、不再携带任何数据库/SMTP 凭据，
    /// 用户、验证码、Windows Hello 密钥相关操作统一经由此处访问服务端（127.0.0.1:9527）。
    /// </summary>
    public static class ApiClient
    {
        private const string BaseUrl = "http://127.0.0.1:9527";
        private static readonly HttpClient client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

        /// <summary>POST JSON 并解析统一响应（{ ok: bool, ... }）。网络/服务异常时抛出。</summary>
        private static async Task<JObject> PostAsync(string path, object body)
        {
            string json = JsonConvert.SerializeObject(body ?? new { });
            using (var content = new StringContent(json, Encoding.UTF8, "application/json"))
            {
                HttpResponseMessage response = await client.PostAsync(BaseUrl + path, content).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                string text = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                return JObject.Parse(text);
            }
        }

        /// <summary>POST 携带 Header（UID/Serial/自定义）的请求，返回 JSON 响应。</summary>
        private static async Task<JObject> PostWithHeadersAsync(string path, System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<string, string>> headers)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Post, BaseUrl + path))
            {
                foreach (System.Collections.Generic.KeyValuePair<string, string> pair in headers)
                {
                    request.Headers.Add(pair.Key, pair.Value);
                }
                using (HttpResponseMessage response = await client.SendAsync(request).ConfigureAwait(false))
                {
                    response.EnsureSuccessStatusCode();
                    string text = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    return JObject.Parse(text);
                }
            }
        }

        /// <summary>以原始字节流上传文件（application/octet-stream），Header 携带 UID/Serial。返回 ok。</summary>
        private static async Task<bool> UploadBytesAsync(string path, string uid, string serial, byte[] bytes)
        {
            using (var content = new ByteArrayContent(bytes))
            {
                content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
                using (var request = new HttpRequestMessage(HttpMethod.Post, BaseUrl + path))
                {
                    request.Content = content;
                    request.Headers.Add("UID", uid);
                    if (!string.IsNullOrEmpty(serial))
                    {
                        request.Headers.Add("Serial", serial);
                    }
                    using (HttpResponseMessage response = await client.SendAsync(request).ConfigureAwait(false))
                    {
                        response.EnsureSuccessStatusCode();
                        string text = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                        return (bool)(JObject.Parse(text)["ok"] ?? false);
                    }
                }
            }
        }

        /// <summary>GET 下载文件字节；404（服务端无文件）返回空字节数组。</summary>
        private static async Task<byte[]> DownloadBytesAsync(string path)
        {
            using (HttpResponseMessage response = await client.GetAsync(BaseUrl + path).ConfigureAwait(false))
            {
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return Array.Empty<byte>();
                }
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
            }
        }

        // ---------------- 用户查询（返回脱敏数据，不含 Password） ----------------

        public static async Task<User> GetUserByEmailAsync(string email)
        {
            JObject result = await PostAsync("/api/auth/query-by-email", new { email });
            return result["user"]?.ToObject<User>();
        }

        public static async Task<User> GetUserByUidAsync(string uid)
        {
            JObject result = await PostAsync("/api/auth/query-by-uid", new { uid });
            return result["user"]?.ToObject<User>();
        }

        public static async Task<User> GetUserByNameAsync(string name)
        {
            JObject result = await PostAsync("/api/auth/query-by-name", new { name });
            return result["user"]?.ToObject<User>();
        }

        // ---------------- 可用性检查 ----------------

        public static async Task<bool> CheckEmailAvailableAsync(string email)
        {
            JObject result = await PostAsync("/api/auth/check-email", new { email });
            return (bool)(result["available"] ?? false);
        }

        public static async Task<bool> CheckNameAvailableAsync(string name)
        {
            JObject result = await PostAsync("/api/auth/check-name", new { name });
            return (bool)(result["available"] ?? false);
        }

        // ---------------- 注册 / 密码 ----------------

        /// <summary>注册新用户（服务端生成带盐哈希；UID 由客户端生成保证页面间一致）。返回 (是否成功, 错误码, UID)。</summary>
        public static async Task<(bool ok, string error, string uid)> SignUpAsync(string email, string name, string password, string mobile, string uid)
        {
            JObject result = await PostAsync("/api/auth/signup",
                new { email, name, password, mobile = mobile ?? "", uid });
            return ((bool)(result["ok"] ?? false), (string)result["error"], (string)result["uid"]);
        }

        public static async Task<bool> VerifyPasswordAsync(string uid, string password)
        {
            JObject result = await PostAsync("/api/auth/verify-password", new { uid, password });
            return (bool)(result["valid"] ?? false);
        }

        public static async Task<bool> ResetPasswordAsync(string uid, string password)
        {
            JObject result = await PostAsync("/api/auth/reset-password", new { uid, password });
            return (bool)(result["ok"] ?? false);
        }

        // ---------------- 验证码 ----------------

        /// <summary>请求发送验证码。ok=false 时 error 为 TOO_FREQUENT / SEND_FAILED / INVALID_INPUT。</summary>
        public static async Task<(bool ok, string error)> SendCodeAsync(string email, string codeType)
        {
            JObject result = await PostAsync("/api/auth/send-code", new { email, codeType });
            return ((bool)(result["ok"] ?? false), (string)result["error"]);
        }

        /// <summary>校验验证码（服务端校验即删，防重放）。</summary>
        public static async Task<bool> VerifyCodeAsync(string email, string codeType, string code)
        {
            JObject result = await PostAsync("/api/auth/verify-code", new { email, codeType, code });
            return (bool)(result["valid"] ?? false);
        }

        // ---------------- Windows Hello 密钥注册 ----------------

        public static async Task<bool> UpdateUserKeyAsync(string uid, string deviceId, string publicKeyBase64, string attestationBase64)
        {
            JObject result = await PostAsync("/api/userkey/update",
                new { uid, deviceId, publicKey = publicKeyBase64, attestation = attestationBase64 ?? "" });
            return (bool)(result["ok"] ?? false);
        }

        public static async Task<bool> RemoveUserKeyAsync(string uid)
        {
            JObject result = await PostAsync("/api/userkey/remove-user", new { uid });
            return (bool)(result["ok"] ?? false);
        }

        public static async Task<bool> RemoveDeviceKeyAsync(string uid, string deviceId)
        {
            JObject result = await PostAsync("/api/userkey/remove-device", new { uid, deviceId });
            return (bool)(result["ok"] ?? false);
        }

        // ---------------- Windows Hello 挑战-响应 ----------------

        /// <summary>向服务端申请一次性随机挑战（Base64）。</summary>
        public static async Task<string> GetChallengeAsync()
        {
            JObject result = await PostAsync("/api/passport/challenge", new { });
            return (string)result["challenge"];
        }

        /// <summary>提交签名，服务端用注册公钥验证。</summary>
        public static async Task<bool> VerifySignatureAsync(string uid, string deviceId, string challengeBase64, string signatureBase64)
        {
            JObject result = await PostAsync("/api/passport/verify",
                new { uid, deviceId, challenge = challengeBase64, signature = signatureBase64 });
            return (bool)(result["valid"] ?? false);
        }

        // ---------------- 便签保险箱（密码包裹密钥，零知识） ----------------

        /// <summary>获取用户的保险箱（盐 + 密码包裹的便签密钥）。未设置时 salt/wrappedKey 为空字符串。</summary>
        public static async Task<(bool ok, string salt, string wrappedKey)> GetVaultKeyAsync(string uid)
        {
            JObject result = await PostAsync("/api/vault/get", new { uid });
            return ((bool)(result["ok"] ?? false), (string)result["salt"] ?? "", (string)result["wrappedKey"] ?? "");
        }

        /// <summary>设置/更新保险箱（用新密码重新包裹密钥时调用）。</summary>
        public static async Task<bool> SetVaultKeyAsync(string uid, string salt, string wrappedKey)
        {
            JObject result = await PostAsync("/api/vault/set", new { uid, salt, wrappedKey });
            return (bool)(result["ok"] ?? false);
        }

        /// <summary>删除保险箱（关闭个人密码时调用，失去跨设备恢复能力）。</summary>
        public static async Task<bool> RemoveVaultKeyAsync(string uid)
        {
            JObject result = await PostAsync("/api/vault/remove", new { uid });
            return (bool)(result["ok"] ?? false);
        }
        // ---------------- 便签删除 ----------------

        /// <summary>删除服务端便签存档（Header 携带 UID/Serial，与上传协议一致）。</summary>
        public static async Task<bool> DeleteNoteAsync(string uid, string serial)
        {
            JObject result = await PostWithHeadersAsync("/delete-cactus-notes",
                new[] { new System.Collections.Generic.KeyValuePair<string, string>("UID", uid),
                        new System.Collections.Generic.KeyValuePair<string, string>("Serial", serial) });
            return (bool)(result["ok"] ?? false);
        }

        // ---------------- 阅读内容存档（Library 区） ----------------

        /// <summary>上传阅读记录存档到服务端 Library 区（文件名 {serial}.json）。</summary>
        public static async Task<bool> UploadLibraryFileAsync(string uid, string serial, byte[] bytes)
        {
            return await UploadBytesAsync("/upload-cactus-library", uid, serial, bytes);
        }

        /// <summary>下载阅读记录存档（不存在返回空字节数组）。</summary>
        public static async Task<byte[]> DownloadLibraryFileAsync(string uid, string serial)
        {
            return await DownloadBytesAsync($"/download-cactus-library?uid={uid}&serial={serial}");
        }

        /// <summary>删除服务端 Library 区存档。</summary>
        public static async Task<bool> DeleteLibraryFileAsync(string uid, string serial)
        {
            JObject result = await PostWithHeadersAsync("/delete-cactus-library",
                new[] { new System.Collections.Generic.KeyValuePair<string, string>("UID", uid),
                        new System.Collections.Generic.KeyValuePair<string, string>("Serial", serial) });
            return (bool)(result["ok"] ?? false);
        }

        /// <summary>获取服务端 Library 区文件名清单；网络异常时返回空列表。</summary>
        public static async Task<List<string>> ListLibraryFilesAsync(string uid)
        {
            return await ListFilesAsync($"/library-list?uid={uid}");
        }

        // ---------------- 回收站（Recycle 区） ----------------

        /// <summary>上传文件到服务端 Recycle 区（删除进回收站时同步云端）。</summary>
        public static async Task<bool> UploadRecycleFileAsync(string uid, string serial, byte[] bytes)
        {
            return await UploadBytesAsync("/upload-cactus-recycle", uid, serial, bytes);
        }

        /// <summary>下载 Recycle 区文件（不存在返回空字节数组）。</summary>
        public static async Task<byte[]> DownloadRecycleFileAsync(string uid, string serial)
        {
            return await DownloadBytesAsync($"/download-cactus-recycle?uid={uid}&serial={serial}");
        }

        /// <summary>删除服务端 Recycle 区文件（彻底删除时调用）。</summary>
        public static async Task<bool> DeleteRecycleFileAsync(string uid, string serial)
        {
            JObject result = await PostWithHeadersAsync("/delete-cactus-recycle",
                new[] { new System.Collections.Generic.KeyValuePair<string, string>("UID", uid),
                        new System.Collections.Generic.KeyValuePair<string, string>("Serial", serial) });
            return (bool)(result["ok"] ?? false);
        }

        /// <summary>获取服务端 Recycle 区文件名清单；网络异常时返回空列表。</summary>
        public static async Task<List<string>> ListRecycleFilesAsync(string uid)
        {
            return await ListFilesAsync($"/recycle-list?uid={uid}");
        }

        /// <summary>
        /// 跨区移动云端文件（notes / library / recycle 之间）。
        /// 删除进回收站 = MoveFile(serial, from, "recycle")；恢复 = MoveFile(serial, "recycle", to)。
        /// 源文件不存在时服务端按幂等成功处理。
        /// </summary>
        public static async Task<bool> MoveFileAsync(string uid, string serial, string fromSection, string toSection)
        {
            JObject result = await PostWithHeadersAsync("/move-file",
                new[] { new System.Collections.Generic.KeyValuePair<string, string>("UID", uid),
                        new System.Collections.Generic.KeyValuePair<string, string>("Serial", serial),
                        new System.Collections.Generic.KeyValuePair<string, string>("From", fromSection),
                        new System.Collections.Generic.KeyValuePair<string, string>("To", toSection) });
            return (bool)(result["ok"] ?? false);
        }

        /// <summary>GET 拉取文件名清单（JSON 数组）；网络异常返回空列表。</summary>
        private static async Task<List<string>> ListFilesAsync(string path)
        {
            try
            {
                using (HttpResponseMessage response = await client.GetAsync(BaseUrl + path).ConfigureAwait(false))
                {
                    response.EnsureSuccessStatusCode();
                    string text = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    return JsonConvert.DeserializeObject<List<string>>(text) ?? new List<string>();
                }
            }
            catch (Exception)
            {
                // 网络异常：按无远端数据处理
                return new List<string>();
            }
        }
    }
}
