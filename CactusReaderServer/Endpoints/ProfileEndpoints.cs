using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;
using CactusReaderService.Services;

namespace CactusReaderService.Endpoints
{
    /// <summary>
    /// 头像端点 —— 单一职责：用户头像的上传 / 下载 HTTP 映射。
    ///
    /// 协议（与旧 WCF 完全一致）：
    ///   POST /upload-profile-image   Header: UID
    ///   GET  /download-profile-image?uid={uid}
    /// </summary>
    public static class ProfileEndpoints
    {
        public static void MapProfileEndpoints(this WebApplication app, FileStorageService storage)
        {
            app.MapPost("/upload-profile-image", (HttpContext context) =>
                UploadProfileImageAsync(context, storage));

            app.MapGet("/download-profile-image", (string uid) =>
                DownloadProfileImage(uid, storage));
        }

        /// <summary>
        /// POST /upload-profile-image —— 接收用户头像（Header 携带 UID）。
        /// </summary>
        private static async Task UploadProfileImageAsync(HttpContext context, FileStorageService storage)
        {
            string uid = context.Request.Headers["UID"];
            try
            {
                if (!PathGuard.IsValidPathPart(uid))
                {
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    return;
                }

                await storage.SaveProfileImageAsync(uid, context.Request.Body, context.RequestAborted);

                Console.WriteLine(string.Format("在{0}成功接收文件。", DateTime.Now.ToLongTimeString()));
                context.Response.StatusCode = StatusCodes.Status200OK;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            }
        }

        /// <summary>
        /// GET /download-profile-image?uid={uid} —— 下载用户头像。
        /// 基于文件大小+修改时间生成 ETag，客户端 If-None-Match 一致时返回 304，无需传输正文。
        /// </summary>
        private static IResult DownloadProfileImage(string uid, FileStorageService storage)
        {
            if (!PathGuard.IsValidPathPart(uid))
            {
                return Results.BadRequest();
            }

            if (!storage.TryGetProfileImagePath(uid, out string path))
            {
                return Results.NotFound();
            }

            FileInfo info = new FileInfo(path);
            return Results.File(path, "application/octet-stream",
                lastModified: info.LastWriteTimeUtc,
                entityTag: new EntityTagHeaderValue("\"" + info.Length + "-" + info.LastWriteTimeUtc.Ticks.ToString("x") + "\""));
        }
    }
}
