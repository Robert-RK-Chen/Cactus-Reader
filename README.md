<div align="center">
  <img src="Cactus Reader/Sources/Images/catom technology.svg" width="30%" alt="catom technology"/>
</div>

<h1 align="center">🌵 Cactus Reader</h1>

<p align="center">
  <b>现代化 UWP 文档阅读器</b> —— 阅读 · 笔记 · 同步，一个应用全部搞定<br/>
  <sub>为 Microsoft Windows 打造</sub>
</p>

<p align="center">
  <img alt="Platform" src="https://img.shields.io/badge/Platform-Windows%2010%2B-0078D6"/>
  <img alt=".NET" src="https://img.shields.io/badge/.NET-10%20UWP-512BD4"/>
  <img alt="License" src="https://img.shields.io/badge/License-GPLv3-blue"/>
  <img alt="Updated" src="https://img.shields.io/badge/Updated-2026.08-2ea44f"/>
</p>

- 📖 多格式阅读：EPUB / PDF / TXT / 网页
- 🗒️ Cactus Note 便签本：多主题、可锁定、端到端加密
- ☁️ 跨设备双向同步：便签 / 阅读记录 / 回收站
- 🔊 讲述人：流式 TTS 边合成边播放

---

## 🔗 相关链接

| 链接 | 说明 |
| --- | --- |
| [📚 开发文档（Notion）](https://www.notion.so/robertchen13/Cactus-Reader-93e5d471876f4bcbb78c0d7500a69631?source=copy_link) | 架构设计、页面流转、踩坑记录 |
| [💻 GitHub 仓库](https://github.com/Robert-RK-Chen/Cactus-Reader) | 源码托管 |

---

# 🌵 产品与特性

## 📖 多格式阅读

兼容 EPUB、MOBI、TXT、PDF、Office Open XML 等主流文档格式：

- **EPUB**：WebView2 渲染，章节目录、字号 / 字体 / 文本列宽自适应，阅读进度精确到章节内位置；
- **PDF**：Windows Ink 手写批注；
- **TXT / 网页**：沉浸式阅读（焦点行跟随）、朗读；网页自动抓取转为沉浸式正文，支持离线缓存。

## 🗒️ Cactus Note 便签本

内置快速备忘录服务：

- 独立窗口编辑，富文本格式（加粗 / 斜体 / 下划线 / 删除线 / 高亮）；
- 6 种主题色，内容实时预览；
- **锁定 / 解锁**：个人密码或 Windows Hello 保护；
- 收藏夹统一展示便签与阅读记录。

## 📚 阅读痕迹管理

- 阅读记录（Library）自动保存每本书 / 每份文档的阅读痕迹与进度；
- 一键收藏（收藏夹 = 便签与阅读记录的混合视图）；
- 删除的便签与记录进入**回收站**，可随时恢复或彻底删除。

## 🔊 讲述人（TTS）

- 底层 **Xiaomi MiMo 语音合成服务**（MiMo-V2.5-TTS，流式接口）——边合成边播放，显著降低等待时间；
- 预置音色（冰糖 / 茉莉 / 苏打 / 白桦 / Mia / Chloe / Milo / Dean）、20 种发音风格、语速与音调调节；
- API Key 存入 Windows 凭据保险箱（`PasswordVault`），不明文落盘。

---

# 🖥️ 运行环境

| 项目 | 最低要求 | 推荐 |
| --- | --- | --- |
| 操作系统 | Windows 10 20H1（19041） | Windows 11 22H2（22621） |
| 处理器 | 1.0 GHz | 1.40 GHz 及以上 |
| 屏幕分辨率 | 1280 × 768 | 1920 × 1080 及以上 |
| 内存 | 100 MB | 200 MB 及以上 |
| 硬盘 | 100 MB | 200 MB 及以上 |

> 仅支持 64 位（x64 / ARM64），不支持 32 位。

---

# 🔒 安全设计

客户端与服务端采用**零知识**加密设计：

| 层面 | 技术 | 说明 |
| --- | --- | --- |
| 便签内容加密 | AES-256-GCM | 每次加密使用随机 nonce，认证标签保证完整性 |
| 个人密码 | PBKDF2-SHA256（600,000 迭代） | 存储格式 `Base64盐:迭代次数:Base64哈希`，恒定时间比较防计时攻击 |
| 跨设备密钥 | 密码包裹密钥 | 便签密钥 K 经个人密码派生密钥（KEK）加密后托管服务端；服务端仅持有盐与密文密钥，无法解密便签数据 |
| 登录认证 | 密码 / 验证码 / Windows Hello | Windows Hello 挑战-响应，挑战一次性、5 分钟过期、防重放 |
| 验证码 | CSPRNG 生成 | 6 位验证码不可预测，限频 1 分钟、5 分钟有效、校验即删防重放 |
| 重置密码 | 一次性令牌 | 验证码校验通过后签发 resetToken（5 分钟过期、校验即删），凭令牌 + UID 才可改密 |

密钥托管三态：

- **首次使用**：生成新密钥（AES-256）；
- **无密码模式**：明文备份密钥，免密恢复；
- **密码包裹**：密钥经个人密码派生密钥加密后托管服务端。

> ⚠️ 更换设备后首次进入便签本需输入个人密码解锁；忘记密码将**永久丢失**便签数据（设计行为）。
> ⚠️ 密码哈希已由双重 SHA256 升级为 PBKDF2-SHA256，旧格式不再兼容。

---

# ☁️ 跨设备同步

同步采用**云端权威的双向合并**策略：开启同步（或登录后）以云端为基准合并本地与云端数据，**绝不因本地缺失而删除云端数据**。

| 云端区域 | 内容 | 说明 |
| --- | --- | --- |
| Notes | 便签（`{serial}.ctsnote`，AES 加密） | 在用便签本体 |
| Library | 阅读记录（`{serial}.json`） | 阅读痕迹与进度 |
| Recycle | 回收站（便签 / 阅读记录） | 删除 = 移入回收站（跨区移动，不复制）；彻底删除 = 云端同步清除 |

- **删除语义**：删除 = 进回收站，可恢复；彻底删除 = 本地删除 + 云端回收站区删除。
- **换设备**：阅读记录标题与进度上云；原文件令牌失效时弹"资源不存在"对话框，默认保留记录。
- **回收站清单**（`recycle.json`）双向同步，换设备后可恢复被删除的便签与记录。
- **网络文档正文**仅缓存本地（`Cache/*.txt`），不同步云端，恢复后重新抓取。

---

# 🛠️ 技术架构

## 解决方案结构

```
Cactus Reader.sln
├── Cactus Reader/          # 客户端（latest .NET UWP）
│   ├── Sources/AppPages/   # 页面（启动页 / 阅读器 / 便签 / 设置）
│   ├── Sources/ToolKits/   # 工具（AES-GCM 加密 / PBKDF2 哈希 / API 客户端 / 同步 / TTS）
│   ├── Sources/ToolKits/ViewModels/ # MVVM 视图模型（讲述人音色 / 风格等设置）
│   ├── Sources/StickyNotes/# 便签控件与样式
│   ├── Sources/WindowsHello# Windows Hello 认证
│   └── Entities/           # 数据实体
└── CactusReaderServer/     # 服务端（ASP.NET Core Minimal API）
    ├── Endpoints/          # HTTP 端点（Auth / Notes / Profile / Vault / JsonHelper）
    ├── Services/           # 数据库 / 文件存储 / 邮件 / 口令哈希
    └── Entities/           # 数据实体
```

## 客户端（Cactus Reader）

- 应用程序类型：latest .NET UWP（由 .NET Native 迁移）
- 目标框架：`net10.0-windows10.0.26100.0`（最低 Windows 10 20H1 19041）
- 客户端**不直连数据库、不携带任何数据库 / SMTP 凭据**，所有数据操作经服务端 API

引用 NuGet 包：

| NuGet 包名 | 版本 |
| --- | --- |
| Microsoft.Toolkit.Uwp.Notifications | 7.1.3 |
| Microsoft.UI.Xaml | 2.8.7 |
| Microsoft.Web.WebView2 | 1.0.4129.50 |
| Microsoft.Xml.SgmlReader | 1.8.30 |
| Newtonsoft.Json | 13.0.4 |
| System.Drawing.Common | 8.0.30 |

## 服务端（Cactus Reader Server）

- 技术栈：ASP.NET Core Minimal API（`net10.0`），由旧版 WCF 服务迁移而来
- 监听地址：`http://127.0.0.1:9527`（客户端统一经 `ApiClient.BaseUrl` 单一常量访问）
- 职责：数据库访问（FreeSql / MySQL）、验证码签发与校验（CSPRNG 生成、限频、一次性校验）、验证码邮件（Microsoft Graph）、用户文件存储（头像 / 便签）、Windows Hello 挑战-响应、便签 Vault（密码包裹密钥）托管、重置密码一次性令牌签发
- 数据存储目录：默认 `{程序目录}/cactus-data`，可通过 `appsettings.json` 的 `DataRoot` 配置

引用 NuGet 包：

| NuGet 包名 | 版本 |
| --- | --- |
| FreeSql | 3.5.311 |
| FreeSql.Provider.MySql | 3.5.311 |
| Microsoft.Graph | 6.5.0 |
| Azure.Identity | 1.21.0 |

## 数据库

- 数据库：MySQL 8.0，字符集 utf8mb4（支持 emoji 与生僻字）
- 建表脚本：`cactus_reader.sql`（含 user / code / userkey / vaultkey 四表）

---

# 🕹️ 快速上手（本地开发）

## 1. 服务端敏感配置

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

## 2. 客户端签名证书

- `Cactus Reader_TemporaryKey.pfx`（MSIX 签名证书，含私钥）已被 `.gitignore` 排除，**不会提交**。
- 克隆项目后，如需本地打包 MSIX：请使用 Visual Studio 的"应用打包"生成你自己的测试证书，并将 `Cactus Reader.csproj` 中的 `PackageCertificateThumbprint` 更新为对应指纹。
- 未配置证书不影响 Debug 调试（依赖 `AllowLocalNetworkLoopback` 访问本机服务端）。

## 3. 讲述人（TTS）配置

- 讲述人语音合成由 **Xiaomi MiMo 语音合成服务**（MiMo-V2.5-TTS，OpenAI 兼容接口）提供，需要有效的 MiMo API Key。
- 在**设置页 → 讲述人 → MiMo API Key** 输入 Key 并点击「保存」即可；Key 使用 Windows 凭据保险箱（`PasswordVault`）加密存储，**不会**以明文写入本地设置或提交到仓库。
- API Key 可在 [platform.xiaomimimo.com](https://platform.xiaomimimo.com/console) 获取；未配置 Key 时，朗读功能会提示"未能生成语音"。

---

# 📃 许可证

本项目基于 **GPLv3** 开源，详见 [LICENSE](LICENSE)。

---

> Copyright © 2022-2026 Robert Chen, catom technology. 保留所有权利。
