# EpinelPS

---

<div align="center">

[![GitHub issues](https://img.shields.io/github/issues/MishaProductions/nikke-server?style=flat-square)](https://github.com/MishaProductions/nikke-server/issues)
[![GitHub pr](https://img.shields.io/github/issues-pr/MishaProductions/nikke-server?style=flat-square)](https://github.com/MishaProductions/nikke-server/pulls)
[![GitHub](https://img.shields.io/github/license/MishaProductions/nikke-server?style=flat-square)](https://github.com/MishaProductions/nikke-server/blob/main/LICENSE)
![GitHub release (with filter)](https://img.shields.io/github/downloads-pre/MishaProductions/nikke-server/latest/total?style=flat-square)
![GitHub Repo stars](https://img.shields.io/github/stars/MishaProductions/nikke-server?style=flat-square)
[![Discord](https://img.shields.io/discord/1261717212448952450?style=flat-square)](https://discord.gg/Ztt6Y9vQjF)

</div>
Private/local server for a 2d anime rpg game. The goal of this project is to replicate the functionality of the official server.

Discord server: https://discord.gg/Ztt6Y9vQjF

> [!CAUTION]
> Please note this GitHub repository (https://github.com/EpinelPS/EpinelPS/) is the only official source for EpinelPS. **If you bought it from someone, you got scammed. Do not download EpinelPS from other sources.** Download link: https://nightly.link/EpinelPS/EpinelPS/workflows/dotnet-desktop/main/Server%20and%20Server%20selector.zip

> [!WARNING]
> This project is in an early state so many functions in the game do not work. It is recommended to download the latest build from GitHub actions.

## Usage
Download the [GitHub actions build](https://nightly.link/EpinelPS/EpinelPS/workflows/dotnet-desktop/main/Server%20and%20Server%20selector.zip), and run ServerSelector.Desktop.exe as administrator (to modify DNS hosts file and install a CA cert). Make sure to close the game and launcher first. Select Local server, and then click save. After that, start EpinelPS.exe to start the actual server.
<br>
<img src="https://github.com/MishaProductions/nikke-server/assets/106913236/b01194ef-aec5-4de9-b982-1253757655f8" width="192" height="108">
<br>
You should be able to register an new account in the launcher (you can enter any email verification code).

## Build & Deployment
### GitHub Actions (官方打包流程)
工作流文件：`.github/workflows/dotnet-desktop.yml`

- Windows 打包：
  - `dotnet publish ServerSelector.Desktop`
  - `dotnet publish EpinelPS`
  - 将 `ServerSelector.Desktop` 与 `EpinelPS` 的 publish 输出复制到 `out/`，并拷贝 `sodium.dll`
  - 上传构建产物：`Server and Server selector`
- Linux 服务器版：
  - `dotnet publish EpinelPS`
  - 复制 `EpinelPS/bin/Release/net10.0/linux-x64/publish/` 到 `out/`
  - 上传构建产物：`EpinelPS_linux_x64`

### 本地运行（推荐）
1. 下载 GitHub Actions 构建产物 `Server and Server selector.zip`
2. 以管理员运行 `ServerSelector.Desktop.exe`（修改 hosts + 安装 CA 证书）
3. 选择 `Local server` 并保存
4. 运行 `EpinelPS.exe` 启动服务端


To access the admin panel, go to https://127.0.0.1/admin/ and sign in. Note that IsAdmin needs to be true for the user account. You can skip stages and add all characters using that link for example.


> [!Note]
> Before updating the game, make sure to switch back to the official server to ensure that the game is properly patched.

## What is implemented or missing?
See the todo list at https://github.com/orgs/EpinelPS/projects/1 and https://github.com/EpinelPS/EpinelPS/issues
