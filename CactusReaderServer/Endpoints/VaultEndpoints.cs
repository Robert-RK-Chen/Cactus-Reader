using System;
using CactusReaderService.Entities;
using CactusReaderService.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace CactusReaderService.Endpoints
{
    /// <summary>
    /// 便签保险箱（vault）端点 —— 存储"密码包裹的便签加密密钥"。
    /// 协议（POST JSON，返回统一 { ok: bool, ... }）：
    ///   POST /api/vault/get     { uid }            → { ok, salt, wrappedKey }（未设置时为空字符串）
    ///   POST /api/vault/set     { uid, salt, wrappedKey } → { ok }（upsert）
    ///   POST /api/vault/remove  { uid }            → { ok }（关闭个人密码时调用）
    ///
    /// 服务端零知识：只持久化盐与密文密钥，无法解密便签内容。
    /// </summary>
    public static class VaultEndpoints
    {
        public static WebApplication MapVaultEndpoints(this WebApplication app, DbService db)
        {
            var fs = db.FreeSql;

            // ---------------- 获取 ----------------
            app.MapPost("/api/vault/get", (VaultGetRequest req) =>
            {
                if (req == null || string.IsNullOrWhiteSpace(req.Uid))
                    return Results.Ok(new { ok = false, salt = "", wrappedKey = "" });

                var vault = fs.Select<VaultKey>().Where(v => v.UID == req.Uid).ToOne();
                if (vault is null)
                    return Results.Ok(new { ok = true, salt = "", wrappedKey = "" });

                return Results.Ok(new { ok = true, salt = vault.Salt, wrappedKey = vault.WrappedKey });
            });

            // ---------------- 设置 / 更新（upsert） ----------------
            app.MapPost("/api/vault/set", (VaultSetRequest req) =>
            {
                if (req == null || string.IsNullOrWhiteSpace(req.Uid) ||
                    string.IsNullOrWhiteSpace(req.Salt) || string.IsNullOrWhiteSpace(req.WrappedKey))
                {
                    return Results.Ok(new { ok = false });
                }

                var vault = fs.Select<VaultKey>().Where(v => v.UID == req.Uid).ToOne();
                if (vault is null)
                {
                    vault = new VaultKey
                    {
                        UID = req.Uid,
                        Salt = req.Salt,
                        WrappedKey = req.WrappedKey,
                        UpdateTime = DateTime.Now
                    };
                    fs.Insert(vault).ExecuteAffrows();
                }
                else
                {
                    vault.Salt = req.Salt;
                    vault.WrappedKey = req.WrappedKey;
                    vault.UpdateTime = DateTime.Now;
                    fs.Update<VaultKey>().SetSource(vault).ExecuteAffrows();
                }
                return Results.Ok(new { ok = true });
            });

            // ---------------- 删除 ----------------
            app.MapPost("/api/vault/remove", (VaultGetRequest req) =>
            {
                if (req == null || string.IsNullOrWhiteSpace(req.Uid))
                    return Results.Ok(new { ok = false });

                fs.Delete<VaultKey>().Where(v => v.UID == req.Uid).ExecuteAffrows();
                return Results.Ok(new { ok = true });
            });

            return app;
        }
    }

    public class VaultGetRequest
    {
        public string Uid { get; set; }
    }

    public class VaultSetRequest
    {
        public string Uid { get; set; }
        public string Salt { get; set; }
        public string WrappedKey { get; set; }
    }
}
