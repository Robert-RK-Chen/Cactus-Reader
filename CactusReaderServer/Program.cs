using System;
using System.IO;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using CactusReaderService.Endpoints;
using CactusReaderService.Services;

namespace CactusReaderService
{
    /// <summary>
    /// CactusReaderServer —— ASP.NET Core Minimal API 版。
    /// 由 WCF WebServiceHost 迁移而来，URL / Header / 返回格式与旧版完全一致，
    /// 客户端（BackgroundUploader / BackgroundDownloader）无需任何改动。
    ///
    /// 2026-08 扩展：承担数据库访问（用户/验证码/Windows Hello 密钥）与验证码邮件发送，
    /// 客户端不再直连 MySQL、不再携带 SMTP 凭据。
    ///
    /// 本文件为组合根（Composition Root），只负责：
    ///   1. 加载配置
    ///   2. 构建 WebApplication
    ///   3. 装配服务（FileStorageService / DbService / MailService / PassportService）
    ///   4. 注册端点（ProfileEndpoints / NotesEndpoints / AuthEndpoints）
    ///   5. 启动
    /// </summary>
    public class Program
    {
        public static void Main(string[] args)
        {
            // 显式以程序所在目录为配置基准，避免依赖启动时的工作目录（双击 exe / 服务方式启动均稳定）
            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
                // 本地开发凭据：dotnet user-secrets 存储于用户目录（%APPDATA%\Microsoft\UserSecrets），不入库
                .AddUserSecrets<Program>()
                // 环境变量可覆盖 appsettings.json（占位符）与 user-secrets，例如：
                //   ConnectionStrings__MySql、GraphMail__ClientSecret、DataRoot
                .AddEnvironmentVariables()
                .Build();

            var builder = WebApplication.CreateBuilder(args);
            builder.Configuration.AddConfiguration(configuration);
            // 与服务装配读取的是同一份 configuration（含 user-secrets / 环境变量）

            // 与旧版一致：仅监听本机回环地址 9527 端口
            builder.WebHost.UseUrls("http://127.0.0.1:9527");

            var app = builder.Build();

            // 解析数据存储根目录（默认回退到程序目录下 cactus-data）
            string configured = configuration["DataRoot"] ?? "";
            string dataRoot = string.IsNullOrWhiteSpace(configured)
                ? Path.Combine(AppContext.BaseDirectory, "cactus-data")
                : Path.IsPathRooted(configured)
                    ? configured
                    : Path.Combine(AppContext.BaseDirectory, configured);

            // 文件存储服务（单一职责：文件存储原子操作）
            FileStorageService storage = new FileStorageService(dataRoot);
            storage.EnsureRoot();

            // 数据库服务（连接串来自 appsettings.json ConnectionStrings:MySql）
            DbService db = new DbService(configuration.GetConnectionString("MySql") ?? "");

            // 验证码邮件服务（Microsoft Graph 凭据仅存于服务端配置）
            MailService mail = new MailService(configuration);

            // Windows Hello 挑战-响应验证服务
            PassportService passport = new PassportService();

            // 端点映射（单一职责：HTTP 层）
            app.MapProfileEndpoints(storage);
            app.MapNotesEndpoints(storage);
            app.MapFileEndpoints(storage);
            app.MapAuthEndpoints(db, mail, passport, app.Logger);
            app.MapVaultEndpoints(db);

            app.Logger.LogInformation("服务已启动: http://127.0.0.1:9527");
            app.Logger.LogInformation("数据存储目录: {DataRoot}", dataRoot);
            app.Logger.LogInformation("邮件发送 (Microsoft Graph): {MailStatus}", mail.IsConfigured ? "已配置" : "未配置（请在 appsettings.json 的 GraphMail 节点填写凭据）");
            app.Run();
        }

    }
}
