using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using CactusReaderService.Services;

namespace CactusReaderService.Endpoints
{
    /// <summary>
    /// 便签端点 —— 单一职责：用户便签的上传 / 下载 / 列表 HTTP 映射。
    ///
    /// 协议（与旧 WCF 完全一致）：
    ///   POST /upload-cactus-notes            Header: UID, Serial
    ///   GET  /download-cactus-notes?uid={uid}&amp;serial={serial}
    ///   GET  /notes-list?uid={uid}           返回 JSON 数组
    /// </summary>
    public static class NotesEndpoints
    {
        public static void MapNotesEndpoints(this WebApplication app, FileStorageService storage)
        {
            app.MapPost("/upload-cactus-notes", (HttpContext context) =>
                UploadCactusNotesAsync(context, storage));

            app.MapGet("/download-cactus-notes", (string uid, string serial) =>
                DownloadCactusNotes(uid, serial, storage));

            app.MapGet("/notes-list", (string uid) =>
                GetNotesList(uid, storage));

            app.MapPost("/delete-cactus-notes", (HttpContext context) =>
                DeleteCactusNotes(context, storage));
        }

        /// <summary>
        /// POST /upload-cactus-notes —— 接收便签文件（Header 携带 UID 与 Serial）。
        /// </summary>
        private static async Task UploadCactusNotesAsync(HttpContext context, FileStorageService storage)
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

                await storage.SaveNoteAsync(uid, serial, context.Request.Body, context.RequestAborted);

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
        /// GET /download-cactus-notes?uid={uid}&amp;serial={serial} —— 下载便签文件。
        /// </summary>
        private static IResult DownloadCactusNotes(string uid, string serial, FileStorageService storage)
        {
            if (!PathGuard.IsValidPathPart(uid) || !PathGuard.IsValidPathPart(serial))
            {
                return Results.BadRequest();
            }

            if (!storage.TryReadNote(uid, serial, out byte[] bytes))
            {
                return Results.NotFound();
            }

            return Results.File(bytes, "application/octet-stream");
        }

        /// <summary>
        /// POST /delete-cactus-notes —— 删除服务端便签（Header 携带 UID 与 Serial，与上传协议一致）。
        /// </summary>
        private static IResult DeleteCactusNotes(HttpContext context, FileStorageService storage)
        {
            string uid = context.Request.Headers["UID"];
            string serial = context.Request.Headers["Serial"];
            if (!PathGuard.IsValidPathPart(uid) || !PathGuard.IsValidPathPart(serial))
            {
                return Results.BadRequest();
            }

            storage.DeleteNote(uid, serial);
            return Results.Ok(new { ok = true });
        }

        /// <summary>
        /// GET /notes-list?uid={uid} —— 返回指定用户的便签文件名清单（JSON 数组）。
        /// </summary>
        private static IResult GetNotesList(string uid, FileStorageService storage)
        {
            try
            {
                if (!PathGuard.IsValidPathPart(uid))
                {
                    return Results.BadRequest();
                }

                List<string> files = storage.ListNotes(uid);
                string json = JsonHelper.BuildJsonArray(files);
                return Results.Text(json, "application/json; charset=utf-8", Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return Results.StatusCode(StatusCodes.Status500InternalServerError);
            }
        }
    }
}
