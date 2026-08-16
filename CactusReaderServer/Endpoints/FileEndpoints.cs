using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using CactusReaderService.Services;

namespace CactusReaderService.Endpoints
{
    /// <summary>
    /// 通用文件端点 —— 阅读内容存档（library）与回收站（recycle）两区的 HTTP 映射。
    /// 与便签（NotesEndpoints）协议风格一致：上传 / 下载 / 列表 / 删除均以 UID 区分用户；
    /// 另提供跨区移动端点，供"删除进回收站 / 从回收站恢复"复用（服务端只移动文件，不复制）。
    ///
    /// 协议：
    ///   POST /upload-cactus-library     Header: UID, Serial   （正文为文件内容）
    ///   GET  /library-list?uid={uid}    返回 JSON 文件名数组
    ///   GET  /download-cactus-library?uid={uid}&amp;serial={serial}
    ///   POST /delete-cactus-library     Header: UID, Serial
    ///   POST /upload-cactus-recycle     Header: UID, Serial
    ///   GET  /recycle-list?uid={uid}
    ///   GET  /download-cactus-recycle?uid={uid}&amp;serial={serial}
    ///   POST /delete-cactus-recycle     Header: UID, Serial
    ///   POST /move-file                 Header: UID, Serial, From, To（notes/library/recycle）
    /// </summary>
    public static class FileEndpoints
    {
        public static void MapFileEndpoints(this WebApplication app, FileStorageService storage)
        {
            // 阅读内容存档区
            app.MapPost("/upload-cactus-library", (HttpContext context) =>
                UploadFileAsync(context, storage, "library"));
            app.MapGet("/library-list", (string uid) =>
                ListFiles(uid, storage, "library"));
            app.MapGet("/download-cactus-library", (string uid, string serial) =>
                DownloadFile(uid, serial, storage, "library"));
            app.MapPost("/delete-cactus-library", (HttpContext context) =>
                DeleteFile(context, storage, "library"));

            // 回收站区
            app.MapPost("/upload-cactus-recycle", (HttpContext context) =>
                UploadFileAsync(context, storage, "recycle"));
            app.MapGet("/recycle-list", (string uid) =>
                ListFiles(uid, storage, "recycle"));
            app.MapGet("/download-cactus-recycle", (string uid, string serial) =>
                DownloadFile(uid, serial, storage, "recycle"));
            app.MapPost("/delete-cactus-recycle", (HttpContext context) =>
                DeleteFile(context, storage, "recycle"));

            // 跨区移动（删除进回收站 / 恢复出回收站）
            app.MapPost("/move-file", (HttpContext context) =>
                MoveFile(context, storage));
        }

        /// <summary>
        /// POST /upload-cactus-library | /upload-cactus-recycle —— 接收文件（Header 携带 UID 与 Serial）。
        /// </summary>
        private static async Task UploadFileAsync(HttpContext context, FileStorageService storage, string section)
        {
            string uid = context.Request.Headers["UID"];
            string serial = context.Request.Headers["Serial"];
            try
            {
                if (!PathGuard.IsValidPathPart(uid) || !PathGuard.IsValidPathPart(serial))
                {
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    return;
                }

                await storage.SaveFileAsync(uid, serial, section, context.Request.Body, context.RequestAborted);

                Console.WriteLine(string.Format("在{0}成功接收{1}文件。", DateTime.Now.ToLongTimeString(), section));
                context.Response.StatusCode = StatusCodes.Status200OK;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            }
        }

        /// <summary>
        /// GET /download-cactus-library | /download-cactus-recycle —— 下载文件。
        /// </summary>
        private static IResult DownloadFile(string uid, string serial, FileStorageService storage, string section)
        {
            if (!PathGuard.IsValidPathPart(uid) || !PathGuard.IsValidPathPart(serial))
            {
                return Results.BadRequest();
            }

            if (!storage.TryReadFile(uid, serial, section, out byte[] bytes))
            {
                return Results.NotFound();
            }

            return Results.File(bytes, "application/octet-stream");
        }

        /// <summary>
        /// POST /delete-cactus-library | /delete-cactus-recycle —— 删除文件（Header 携带 UID 与 Serial）。
        /// </summary>
        private static IResult DeleteFile(HttpContext context, FileStorageService storage, string section)
        {
            string uid = context.Request.Headers["UID"];
            string serial = context.Request.Headers["Serial"];
            if (!PathGuard.IsValidPathPart(uid) || !PathGuard.IsValidPathPart(serial))
            {
                return Results.BadRequest();
            }

            storage.DeleteFile(uid, serial, section);
            return Results.Ok(new { ok = true });
        }

        /// <summary>
        /// GET /library-list | /recycle-list —— 返回指定用户的文件清单（JSON 数组）。
        /// </summary>
        private static IResult ListFiles(string uid, FileStorageService storage, string section)
        {
            try
            {
                if (!PathGuard.IsValidPathPart(uid))
                {
                    return Results.BadRequest();
                }

                List<string> files = storage.ListFiles(uid, section);
                string json = BuildJsonArray(files);
                return Results.Text(json, "application/json; charset=utf-8", Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return Results.StatusCode(StatusCodes.Status500InternalServerError);
            }
        }

        /// <summary>
        /// POST /move-file —— 跨区移动（Header 携带 UID、Serial、From、To）。
        /// From/To 取值为 notes / library / recycle；源文件不存在时按成功处理（幂等）。
        /// </summary>
        private static IResult MoveFile(HttpContext context, FileStorageService storage)
        {
            string uid = context.Request.Headers["UID"];
            string serial = context.Request.Headers["Serial"];
            string from = context.Request.Headers["From"];
            string to = context.Request.Headers["To"];
            if (!PathGuard.IsValidPathPart(uid) || !PathGuard.IsValidPathPart(serial) ||
                !IsValidSection(from) || !IsValidSection(to))
            {
                return Results.BadRequest();
            }

            storage.MoveFile(uid, serial, from, to);
            return Results.Ok(new { ok = true });
        }

        private static bool IsValidSection(string section)
        {
            return section == "notes" || section == "library" || section == "recycle";
        }

        /// <summary>
        /// 将字符串列表序列化为 JSON 数组（文件名由 Guid 生成，无需复杂转义）。
        /// </summary>
        private static string BuildJsonArray(List<string> values)
        {
            StringBuilder sb = new StringBuilder("[");
            for (int i = 0; i < values.Count; i++)
            {
                if (i > 0)
                {
                    sb.Append(',');
                }
                sb.Append('"')
                  .Append(values[i].Replace("\\", "\\\\").Replace("\"", "\\\""))
                  .Append('"');
            }
            sb.Append(']');
            return sb.ToString();
        }
    }
}
