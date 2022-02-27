# Cactus Reader README

> @Robert Chen 更新日期：2022年2月27日

- 阅读 Cactus Reader 的开发文档，请访问：[Cactus Reader 开发文档](https://www.notion.so/Cactus-Reader-93e5d471876f4bcbb78c0d7500a69631) ，开发文档被托管在 Notion 上。
- 访问 Cactus Reader 的 [GitHub 开源库](https://github.com/Robert-RK-Chen/Cactus-Reader) ，代码被托管在 GitHub 上

---

# 🌵 关于 Cactus Reader

## 应用程序简介

Cactus Reader 是一款 WinUI 3 风格$^1$的，为 Microsoft Windows 打造的现代化风格的 UWP 文档阅读器，具有高效、美观、兼容性强等优点，兼容 EPUB、MOBI、TXT、PDF、Office Open XML等格式的主流文档，并支持对文档进行标记、摘抄收藏等功能，此外，软件提供 Cactus Note 功能，为用户提供快速记载备忘录功能。

## 应用程序运行要求

|  | 最低要求 | 推荐 | 备注 |
| --- | --- | --- | --- |
| 受支持的操作系统 | Windows 10 20H1（19041） | Windows 11 21H2（22000） | 64 位或 ARM，不支持 32 位 |
| 处理器 | 速度为 1.0 GHz 处理器 | 速度 1.40 GHz 或更快的处理器 |  |
| 屏幕分辨率 | 最低显示分辨率 1280*768 | 1920*1080 及以上分辨率 |  |
| RAM | 可用内存 100MB | 可用内存大于等于 200MB |  |
| 硬盘 | 可用空间 100MB | 可用空间大于等于 200MB |  |

# 🛠️ 开发环境

开发工具：Visual Studio 2022

应用程序类型：UWP 应用程序

应用程序目标版本：Windows 11 22000，最低版本：Windows 10 20H1 19041

数据库：MySQL 8.0，字符集 GBK，排序规则：GBK_CHINESE_CI

引用 NuGET 包：

| NuGET 包名 | 版本 |
| --- | --- |
| Microsoft.UI.Xaml | 2.7.0 |
| Microsoft.NETCore.UniversalWindowsPlatform | 6.2.13 |
| MailKit | 3.3.1 |
| FreeSql | 3.0.100 |
| FreeSql.Provider.MySql | 3.0.100 |

# 🗒️ 补充信息

1. Lisense： GPLV3

---

1. 指在 UWP 应用中使用 Microsoft.UI.XAML 风格的控件与样式；

---

> Copyright © 2022, Robert Chen. R.C. Software Studio. 