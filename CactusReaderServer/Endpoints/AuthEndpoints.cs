using System;
using CactusReaderService.Entities;
using CactusReaderService.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace CactusReaderService.Endpoints
{
    /// <summary>
    /// 认证/用户/验证码/Windows Hello 端点。
    /// 客户端（UWP）不再直连 MySQL，全部数据库操作经由此处。
    /// 所有端点返回统一 JSON：{ ok: bool, ... }。
    /// </summary>
    public static class AuthEndpoints
    {
        // 验证码限频间隔与有效期（分钟）
        private const double ResendIntervalMinutes = 1;
        private const double CodeValidMinutes = 5;

        public static WebApplication MapAuthEndpoints(this WebApplication app,
            DbService db, MailService mail, PassportService passport, ILogger logger)
        {
            var fs = db.FreeSql;

            // ---------------- 用户查询（返回脱敏数据，不含 Password） ----------------
            app.MapPost("/api/auth/query-by-email", (QueryByEmailRequest req) =>
            {
                if (req == null || string.IsNullOrWhiteSpace(req.Email))
                    return Results.Ok(new { ok = false, user = (object)null });
                var user = fs.Select<User>().Where(u => u.Email == req.Email).ToOne();
                return Results.Ok(new { ok = true, user = Sanitize(user) });
            });

            app.MapPost("/api/auth/query-by-uid", (QueryByUidRequest req) =>
            {
                if (req == null || string.IsNullOrWhiteSpace(req.Uid))
                    return Results.Ok(new { ok = false, user = (object)null });
                var user = fs.Select<User>().Where(u => u.UID == req.Uid).ToOne();
                return Results.Ok(new { ok = true, user = Sanitize(user) });
            });

            app.MapPost("/api/auth/query-by-name", (QueryByNameRequest req) =>
            {
                if (req == null || string.IsNullOrWhiteSpace(req.Name))
                    return Results.Ok(new { ok = false, user = (object)null });
                var user = fs.Select<User>().Where(u => u.Name == req.Name).ToOne();
                return Results.Ok(new { ok = true, user = Sanitize(user) });
            });

            // ---------------- 可用性检查 ----------------
            app.MapPost("/api/auth/check-email", (CheckEmailRequest req) =>
            {
                bool available = req == null || string.IsNullOrWhiteSpace(req.Email) ||
                    fs.Select<User>().Where(u => u.Email == req.Email).ToOne() is null;
                return Results.Ok(new { ok = true, available });
            });

            app.MapPost("/api/auth/check-name", (CheckNameRequest req) =>
            {
                bool available = req == null || string.IsNullOrWhiteSpace(req.Name) ||
                    fs.Select<User>().Where(u => u.Name == req.Name).ToOne() is null;
                return Results.Ok(new { ok = true, available });
            });

            // ---------------- 注册（SignUp） ----------------
            app.MapPost("/api/auth/signup", (SignUpRequest req) =>
            {
                if (req == null || string.IsNullOrWhiteSpace(req.Email) ||
                    string.IsNullOrWhiteSpace(req.Name) || string.IsNullOrWhiteSpace(req.Password))
                {
                    return Results.Ok(new { ok = false, error = "INVALID_INPUT", uid = "" });
                }

                // 客户端可自选 UID（页面间以 User 对象传参，需保持同一 UID），否则服务端生成
                string uid = string.IsNullOrWhiteSpace(req.Uid)
                    ? Guid.NewGuid().ToString("D").ToUpper()
                    : req.Uid.Trim();

                var user = new User
                {
                    UID = uid,
                    Email = req.Email.Trim(),
                    Name = req.Name.Trim(),
                    Mobile = string.IsNullOrWhiteSpace(req.Mobile) ? null : req.Mobile.Trim(),
                    Password = new PasswordHashService().CreateHash(req.Password),
                    RegistDate = DateTime.Now
                };

                try
                {
                    fs.Insert(user).ExecuteAffrows();
                    return Results.Ok(new { ok = true, error = "", uid });
                }
                catch (Exception ex)
                {
                    // 唯一索引冲突（Email/Name 重复）或外键等数据库错误
                    logger.LogError(ex, "注册失败: {Email}", req.Email);
                    return Results.Ok(new { ok = false, error = "DUPLICATE", uid = "" });
                }
            });

            // ---------------- 密码校验 / 重置 ----------------
            app.MapPost("/api/auth/verify-password", (VerifyPasswordRequest req) =>
            {
                if (req == null || string.IsNullOrWhiteSpace(req.Uid) || string.IsNullOrWhiteSpace(req.Password))
                    return Results.Ok(new { ok = true, valid = false });
                var user = fs.Select<User>().Where(u => u.UID == req.Uid).ToOne();
                bool valid = user != null && new PasswordHashService().Verify(req.Password, user.Password);
                return Results.Ok(new { ok = true, valid });
            });

            app.MapPost("/api/auth/reset-password", (ResetPasswordRequest req) =>
            {
                if (req == null || string.IsNullOrWhiteSpace(req.Uid) || string.IsNullOrWhiteSpace(req.Password))
                    return Results.Ok(new { ok = false });
                var user = fs.Select<User>().Where(u => u.UID == req.Uid).ToOne();
                if (user is null) return Results.Ok(new { ok = false });
                user.Password = new PasswordHashService().CreateHash(req.Password);
                fs.Update<User>().SetSource(user).ExecuteAffrows();
                return Results.Ok(new { ok = true });
            });

            // ---------------- 验证码 ----------------
            app.MapPost("/api/auth/send-code", async (SendCodeRequest req) =>
            {
                if (req == null || string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.CodeType))
                    return Results.Ok(new { ok = false, error = "INVALID_INPUT" });

                // 1. 限频：同邮箱同类型 1 分钟内不重复发送
                var recent = fs.Select<Code>()
                    .Where(c => c.Email == req.Email && c.CodeType == req.CodeType).ToOne();
                if (recent != null && recent.CreateTime.AddMinutes(ResendIntervalMinutes) > DateTime.Now)
                    return Results.Ok(new { ok = false, error = "TOO_FREQUENT" });

                // 2. 生成 6 位验证码
                string verifyCode = new Random().Next(100000, 1000000).ToString();

                // 3. 先写库（复合主键 Email+CodeType），邮件失败时回滚删除，
                //    避免"验证码已入库但邮件未发出"的状态不一致
                fs.InsertOrUpdate<Code>().SetSource(new Code
                {
                    Email = req.Email,
                    CodeType = req.CodeType,
                    VerifyCode = verifyCode,
                    CreateTime = DateTime.Now
                }).ExecuteAffrows();

                // 4. 发送邮件（Microsoft Graph，异步）
                if (!await mail.SendVerifyCodeMailAsync(req.Email, verifyCode))
                {
                    fs.Delete<Code>().Where(c => c.Email == req.Email && c.CodeType == req.CodeType).ExecuteAffrows();
                    return Results.Ok(new { ok = false, error = "SEND_FAILED" });
                }
                return Results.Ok(new { ok = true, error = "" });
            });

            app.MapPost("/api/auth/verify-code", (VerifyCodeRequest req) =>
            {
                if (req == null || string.IsNullOrWhiteSpace(req.Email) ||
                    string.IsNullOrWhiteSpace(req.CodeType) || string.IsNullOrWhiteSpace(req.Code))
                {
                    return Results.Ok(new { ok = true, valid = false });
                }

                var code = fs.Select<Code>()
                    .Where(c => c.Email == req.Email && c.CodeType == req.CodeType).ToOne();
                if (code is null)
                    return Results.Ok(new { ok = true, valid = false });

                bool valid = code.VerifyCode == req.Code &&
                    code.CreateTime.AddMinutes(CodeValidMinutes) > DateTime.Now;

                // 校验即删除：防重放，过期记录一并清理
                fs.Delete<Code>().Where(c => c.Email == req.Email && c.CodeType == req.CodeType).ExecuteAffrows();
                return Results.Ok(new { ok = true, valid });
            });

            // ---------------- Windows Hello：密钥注册记录 ----------------
            app.MapPost("/api/userkey/update", (UserkeyUpdateRequest req) =>
            {
                if (req == null || string.IsNullOrWhiteSpace(req.Uid) ||
                    string.IsNullOrWhiteSpace(req.DeviceId) || string.IsNullOrWhiteSpace(req.PublicKey))
                {
                    return Results.Ok(new { ok = false });
                }

                var existing = fs.Select<Userkey>()
                    .Where(k => k.UID == req.Uid && k.DeviceID == req.DeviceId).ToOne();
                var key = existing ?? new Userkey { ID = Guid.NewGuid().ToString("D").ToUpper() };
                key.UID = req.Uid;
                key.DeviceID = req.DeviceId;
                key.PublicKey = req.PublicKey;      // Base64
                key.Attestation = req.Attestation;  // Base64（可能为空）
                key.LastSignInTime = DateTime.Now;
                fs.InsertOrUpdate<Userkey>().SetSource(key).ExecuteAffrows();
                return Results.Ok(new { ok = true });
            });

            app.MapPost("/api/userkey/remove-user", (UserkeyRemoveUserRequest req) =>
            {
                if (req == null || string.IsNullOrWhiteSpace(req.Uid))
                    return Results.Ok(new { ok = false });
                fs.Delete<Userkey>().Where(k => k.UID == req.Uid).ExecuteAffrows();
                return Results.Ok(new { ok = true });
            });

            app.MapPost("/api/userkey/remove-device", (UserkeyRemoveDeviceRequest req) =>
            {
                if (req == null || string.IsNullOrWhiteSpace(req.Uid) || string.IsNullOrWhiteSpace(req.DeviceId))
                    return Results.Ok(new { ok = false });
                fs.Delete<Userkey>().Where(k => k.UID == req.Uid && k.DeviceID == req.DeviceId).ExecuteAffrows();
                return Results.Ok(new { ok = true });
            });

            // ---------------- Windows Hello：挑战-响应验证 ----------------
            app.MapPost("/api/passport/challenge", () =>
            {
                return Results.Ok(new { ok = true, challenge = passport.CreateChallenge() });
            });

            app.MapPost("/api/passport/verify", (PassportVerifyRequest req) =>
            {
                if (req == null || string.IsNullOrWhiteSpace(req.Uid) ||
                    string.IsNullOrWhiteSpace(req.DeviceId) || string.IsNullOrWhiteSpace(req.Challenge))
                {
                    return Results.Ok(new { ok = true, valid = false });
                }

                var key = fs.Select<Userkey>()
                    .Where(k => k.UID == req.Uid && k.DeviceID == req.DeviceId).ToOne();
                if (key is null)
                    return Results.Ok(new { ok = true, valid = false });

                bool valid = passport.VerifySignature(req.Challenge, req.Signature, key.PublicKey);
                return Results.Ok(new { ok = true, valid });
            });

            return app;
        }

        /// <summary>脱敏：不向客户端返回 Password 哈希。</summary>
        private static object Sanitize(User user)
        {
            if (user is null) return null;
            return new { user.UID, user.Email, user.Name, user.Mobile, user.RegistDate };
        }

        // ---------------- 请求 DTO ----------------
        public record QueryByEmailRequest(string Email);
        public record QueryByUidRequest(string Uid);
        public record QueryByNameRequest(string Name);
        public record CheckEmailRequest(string Email);
        public record CheckNameRequest(string Name);
        public record SignUpRequest(string Email, string Name, string Password, string Mobile, string Uid);
        public record VerifyPasswordRequest(string Uid, string Password);
        public record ResetPasswordRequest(string Uid, string Password);
        public record SendCodeRequest(string Email, string CodeType);
        public record VerifyCodeRequest(string Email, string CodeType, string Code);
        public record UserkeyUpdateRequest(string Uid, string DeviceId, string PublicKey, string Attestation);
        public record UserkeyRemoveUserRequest(string Uid);
        public record UserkeyRemoveDeviceRequest(string Uid, string DeviceId);
        public record PassportVerifyRequest(string Uid, string DeviceId, string Challenge, string Signature);
    }
}
