<div align="center">    
  <img src="Cactus Reader/Sources/Images/catom technology.svg" width="30%" alt="catom technology"/>
</div>

# Cactus Reader

> @Robert Chen 更新日期：2026 年 8 月 13 日

- 阅读 Cactus Reader 的开发文档，请访问：[Cactus Reader 开发文档](https://www.notion.so/robertchen13/Cactus-Reader-93e5d471876f4bcbb78c0d7500a69631?source=copy_link) ，开发文档被托管在 Notion 上。
- 访问 Cactus Reader 的 [GitHub 开源库](https://github.com/Robert-RK-Chen/Cactus-Reader) ，代码被托管在 GitHub 上

---

# 🌵 关于 Cactus Reader

## 应用程序简介

Cactus Reader 是一款现代化风格的 UWP 文档阅读器，为 Microsoft Windows 打造，具有高效、美观、兼容性强等优点，兼容 EPUB、MOBI、TXT、PDF、Office Open XML 等格式的主流文档，并支持对文档进行标记、摘抄收藏等功能。

此外，软件提供 Cactus Note 功能，为用户提供快速记载备忘录服务：支持多主题便签、便签锁定、跨设备云同步，并支持使用个人密码或 Windows Hello 保护便签本。

## 安全架构

重大更新：客户端与服务器采用**零知识**加密设计：

| 层面 | 技术 | 说明 |
| --- | --- | --- |
| 便签内容加密 | AES-256-GCM 认证加密 | 每次加密使用随机 nonce，认证标签保证密文完整性 |
| 个人密码存储 | PBKDF2-SHA256 | 符合 OWASP 密码存储建议，恒定时间比较防计时攻击 |
| 跨设备密钥 | 密码包裹密钥 | 便签密钥 K 由个人密码派生密钥 KEK 加密后托管服务端；服务端仅持有盐与密文密钥，无法解密便签数据 |
| Windows Hello | 挑战-响应认证 | 服务端托管设备公钥，签名验证登录 |

## 应用程序运行要求

| 项目 | 最低要求 | 推荐 | 备注 |
| --- | --- | --- | --- |
| 受支持的操作系统 | Windows 10 20H1（19041） | Windows 11 22H2（22621） | 64 位或 ARM，不支持 32 位 |
| 处理器 | 速度为 1.0 GHz 处理器 | 速度 1.40 GHz 或更快的处理器 |  |
| 屏幕分辨率 | 最低显示分辨率 1280*768 | 1920*1080 及以上分辨率 |  |
| RAM | 可用内存 100MB | 可用内存大于等于 200MB |  |
| 硬盘 | 可用空间 100MB | 可用空间大于等于 200MB |  |

---

# 🛠️ 开发环境

## 解决方案结构

```
Cactus Reader.sln
├── Cactus Reader/          # 客户端（latest .NET UWP）
│   ├── Sources/AppPages/   # 页面（启动页 / 阅读器 / 便签 / 设置）
│   ├── Sources/ToolKits/   # 工具（AES-GCM 加密 / PBKDF2 哈希 / API 客户端 / 同步）
│   ├── Sources/StickyNotes/# 便签控件与样式
│   ├── Sources/WindowsHello# Windows Hello 认证
│   └── Entities/           # 数据实体
└── CactusReaderServer/     # 服务端（ASP.NET Core Minimal API）
    ├── Endpoints/          # HTTP 端点（Auth / Notes / Profile / Vault）
    ├── Services/           # 数据库 / 文件存储 / 邮件 / 口令哈希
    └── Entities/           # 数据实体
```

## 客户端（Cactus Reader）

- 应用程序类型：latest .NET UWP（由 .NET Native 迁移）
- 目标框架：`net10.0-windows10.0.26100.0`（最低 Windows 10 20H1 19041）
- 客户端不再直连数据库、不再携带任何数据库 / SMTP 凭据，所有数据操作经服务端 API

引用 NuGet 包：

| NuGet 包名 | 版本 |
| --- | --- |
| Microsoft.CognitiveServices.Speech | 1.51.1 |
| Microsoft.Toolkit.Uwp.Notifications | 7.1.3 |
| Microsoft.UI.Xaml | 2.8.7 |
| Microsoft.Web.WebView2 | 1.0.4129.50 |
| Microsoft.Xml.SgmlReader | 1.8.30 |
| Newtonsoft.Json | 13.0.4 |
| System.Drawing.Common | 8.0.30 |

## 服务端（Cactus Reader Server）

- 技术栈：ASP.NET Core Minimal API（`net10.0`），由旧版 WCF 服务迁移而来
- 监听地址：`http://127.0.0.1:9527`
- 职责：数据库访问（FreeSql / MySQL）、验证码邮件（Microsoft Graph）、用户文件存储（头像 / 便签）、Windows Hello 挑战-响应、便签 Vault（密码包裹密钥）托管
- 数据存储目录：默认 `{程序目录}/cactus-data`，可通过 `appsettings.json` 的 `DataRoot` 配置

引用 NuGet 包：

| NuGet 包名 | 版本 |
| --- | --- |
| FreeSql | 3.5.311 |
| FreeSql.Provider.MySql | 3.5.311 |
| Microsoft.Graph | 6.5.0 |
| Azure.Identity | 1.21.0 |

## 数据库

- 数据库：MySQL 8.0
- 字符集：utf8mb4（支持 emoji 与生僻字）
- 建表脚本：`cactus_reader.sql`（含 user / code / userkey / vaultkey）

---

# 🕹 部署与配置

## 服务端敏感配置

`appsettings.json` 中数据库密码与 Microsoft Graph 凭据已替换为占位符（`YOUR_*`）。本地运行请通过**环境变量**注入真实配置（优先级高于 appsettings.json）：

| 配置项 | 环境变量 |
| --- | --- |
| MySQL 连接串 | `ConnectionStrings__MySql` |
| Graph 租户 ID | `GraphMail__TenantId` |
| Graph 客户端 ID | `GraphMail__ClientId` |
| Graph 客户端密钥 | `GraphMail__ClientSecret` |
| 发件邮箱 | `GraphMail__FromAddress` |
| 数据存储目录 | `DataRoot` |

PowerShell 示例：

```powershell
$env:ConnectionStrings__MySql = "Server=localhost; Port=3306; User ID=root; Password=你的真实密码; Database=cactus_reader; Charset=utf8mb4; SslMode=None"
$env:GraphMail__ClientSecret = "你的真实密钥"
$env:DataRoot = "D:\cactus-data"
dotnet run --project CactusReaderServer
```

## 客户端签名证书

- `Cactus Reader_TemporaryKey.pfx`（MSIX 签名证书，含私钥）已被 `.gitignore` 排除，**不会提交**。
- 克隆项目后，如需本地打包 MSIX：请使用 Visual Studio 的"应用打包"生成你自己的测试证书，并将 `Cactus Reader.csproj` 中的 `PackageCertificateThumbprint` 更新为对应指纹。
- 未配置证书不影响 Debug 调试（依赖 `AllowLocalNetworkLoopback` 访问本机服务端）。

## 便签加密说明

- 便签内容使用 AES-256-GCM 加密；个人密码使用 PBKDF2-SHA256（迭代+随机盐）。
- 跨设备同步采用**零知识**设计：服务端仅托管"密码包裹的密钥"（Vault），无法解密任何便签数据。
- 更换设备后首次进入便签本需输入个人密码解锁；忘记密码将**永久丢失**便签数据（设计行为）。

---

# 🗒️ 补充信息

1. License：GPLV3

---

> Copyright © 2022-2026 Robert Chen, catom technology. 保留所有权利。
