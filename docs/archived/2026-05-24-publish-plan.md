# StarBlogPublisher 发布计划调整：GUI / CLI 分流，GitHub Release 统一入口

## Context

当前仓库已经有 GUI 发布流程和现有的 Windows Scoop 分发：

- GUI 由 `.github/workflows/release.yml` 负责构建并上传到 GitHub Release
- 仓库内已有 GUI 的 Scoop manifest：`bucket/starblog-publisher.json`
- `StarBlogPublisher.Cli` 目前还没有独立的打包与分发机制

本次目标不是把 GUI 和 CLI 硬塞进一个大工作流，而是采用更稳妥的落地方式：

1. GUI 和 CLI 保持独立发布职责，分别构建各自产物
2. GitHub Release 只保留一个创建/更新入口，避免两个工作流互相覆盖 Release 元数据
3. CLI 产物也上传到同一个 GitHub Release 页面，供 Scoop / Homebrew / 手动下载复用

## 目标架构

### 发布职责划分

| 组件 | 负责内容 | 是否写 GitHub Release 元数据 |
|------|----------|------------------------------|
| Release 入口工作流 | 创建或更新版本 Release、生成 Release Notes、统一版本号 | 是 |
| GUI 发布工作流 | 构建 GUI 可执行文件并上传 GUI 资产 | 否 |
| CLI 发布工作流 | 构建 dotnet tool、CLI 自包含包并上传 CLI 资产 | 否 |
| 外部分发更新 | 更新 Scoop / Homebrew 引用的 CLI 包 URL 和 SHA256 | 否 |

### 分发渠道总览

| 渠道 | 命令 | 需要 .NET 运行时？ | 平台 | 备注 |
|------|------|---------------------|------|------|
| GUI GitHub Release | 直接下载 | 否 | win-x64, linux-x64, osx-x64 | 维持现状 |
| GUI Scoop | `scoop install starblog-publisher/starblog-publisher` | 否 | Windows x64 | 维持现状 |
| NuGet dotnet tool | `dotnet tool install --global StarBlogPublisher.Cli` | 是 | 全平台 | 新增 CLI 分发 |
| CLI GitHub Release | 直接下载 | 否 | win-x64, linux-x64, osx-x64, osx-arm64 | 供 Scoop / Homebrew 复用 |
| Homebrew | `brew install star-blog/tap/starblog` | 否 | macOS / Linux | 指向 CLI 资产 |
| CLI Scoop | `scoop install starblog` | 否 | Windows x64 | 指向 CLI 资产 |

## 实施步骤

### Step 1: 修改 CLI `.csproj`，补齐 dotnet tool 与发布元数据

**文件:** `StarBlogPublisher.Cli/StarBlogPublisher.Cli.csproj`

添加以下内容：

- `PackAsTool=true`, `ToolCommandName=starblog`
- NuGet 元数据：`PackageId`, `Authors`, `Description`, `PackageLicenseExpression`, `PackageReadmeFile`, `RepositoryUrl`
- `Version` / `PackageVersion` 由 CI 在打包时通过 tag 注入，不在项目文件写死
- 条件发布属性组：仅在 CLI 自包含发布时开启 `PublishTrimmed` / `PublishSingleFile` 或 `PublishAot`
- `<None Include="..\README.md" Pack="true" PackagePath="\" />`

说明：

- CLI 的 NuGet tool 和 CLI 二进制发布是两个目标，不要把 AOT 属性常驻到普通 `dotnet pack`
- 初始默认优先走“自包含单文件 + Trim”方案；AOT 作为第二阶段优化目标

### Step 2: 本地验证 CLI 打包路径

先在本地验证三件事：

```bash
dotnet pack ./StarBlogPublisher.Cli/StarBlogPublisher.Cli.csproj -c Release

dotnet tool install --global --add-source ./StarBlogPublisher.Cli/nupkg StarBlogPublisher.Cli

dotnet publish ./StarBlogPublisher.Cli/StarBlogPublisher.Cli.csproj \
  -c Release -r osx-arm64 --self-contained true \
  -p:PublishTrimmed=true -p:PublishSingleFile=true -o ./test-cli
```

本地验证必须覆盖：

1. `starblog --help`
2. `starblog auth status`
3. `starblog category list` 或一个不需要交互输入的只读命令

说明：

- 先验证运行时，再考虑切换到 `PublishAot=true`
- 如果后续 AOT 可行，再增加一轮 AOT 本地验证

### Step 3: 收口 GitHub Release 创建权

本次不让 GUI workflow 和 CLI workflow 都去创建/覆盖 Release。

新增一个统一入口工作流，例如：

- `.github/workflows/release-entry.yml`

职责：

1. 由 `v*.*.*` tag 触发
2. 解析版本号
3. 创建或更新 GitHub Release
4. 生成并写入统一的 Release Notes
5. 通过 outputs 或 artifact 将版本信息传递给后续 GUI / CLI 发布流程

这样做的目的：

- 避免两个工作流同时写 Release 标题、正文、资产
- 避免 GUI 和 CLI 对同一 tag 的竞态更新

### Step 4: 拆分 GUI 和 CLI 发布工作流

采用“两条发布流 + 一个统一入口”的结构。

#### 4.1 GUI 发布工作流

**建议文件:** `.github/workflows/release-gui.yml`

职责：

- 保留现有 GUI 构建矩阵
- 构建 GUI AOT 或现有桌面发布产物
- 将 GUI 产物上传到已经存在的同一个 GitHub Release
- 如需继续维护现有 GUI Scoop manifest，则在此工作流内完成

#### 4.2 CLI 发布工作流

**建议文件:** `.github/workflows/release-cli.yml`

职责：

1. `dotnet pack` 生成 NuGet tool 包
2. `dotnet nuget push` 发布到 NuGet.org
3. 构建 CLI 自包含二进制
4. 上传 CLI 二进制到已经存在的同一个 GitHub Release
5. 计算 Windows / macOS / Linux 包的 SHA256
6. 更新 CLI 的 Scoop manifest 和 Homebrew formula

说明：

- CLI workflow 可以上传 Release 资产，但不负责创建 Release 或改写 Release Notes
- 如果 Release 不存在，应直接失败，而不是隐式创建第二个 Release

### Step 5: 明确 CLI 二进制策略

CLI 二进制先按“稳定优先”拆成两个阶段：

#### Phase 1: 稳定可发布

- `PublishTrimmed=true`
- `PublishSingleFile=true`
- `SelfContained=true`

适用原因：

- `StarBlogPublisher.Core` 依赖 `Newtonsoft.Json`、`Refit`、`Microsoft.Extensions.AI.OpenAI`
- 这些依赖在 Native AOT 下更容易暴露 trim / reflection 问题
- 先把可安装、可运行、可回滚的分发链路打通更重要

#### Phase 2: AOT 优化

在 Phase 1 稳定后，再补充：

- `PublishAot=true`
- 针对具体告警补齐 trimming / dynamic access 配置
- 逐平台做运行时烟测，而不是只看编译通过

### Step 6: 处理 Scoop / Homebrew 的边界

当前仓库已存在 GUI 的 Scoop 分发，因此本次需要明确 CLI 和 GUI 不混用同一个 manifest。

建议：

1. 现有 `bucket/starblog-publisher.json` 继续服务 GUI
2. CLI 使用单独的分发入口，例如：
   - 独立 Scoop bucket：`star-blog/scoop-bucket`
   - 独立 Homebrew tap：`star-blog/homebrew-tap`
3. README 中同时说明：
   - GUI 的安装方式
   - CLI 的安装方式
   - 两者的命令名和用途不同

对应外部仓库文件：

- `star-blog/scoop-bucket/bucket/starblog.json`
- `star-blog/homebrew-tap/Formula/starblog.rb`

CLI 的外部公式只引用 CLI 在 GitHub Release 页面中的资产，不引用 GUI 产物。

### Step 7: 修复现有 release.yml 的 .NET 版本问题

现有 `.github/workflows/release.yml` 使用的是 `dotnet-version: 8.0.x`，而项目实际目标框架已经是 `net10.0`。

需要修正为：

- `dotnet-version: 10.0.x`

但这一步应放在 GUI 发布流重构时一起完成，避免中途修改后又继续沿用旧结构。

## 建议的工作流结构

| 文件 | 作用 |
|------|------|
| `.github/workflows/release-entry.yml` | 版本入口，创建/更新 GitHub Release，统一 Release Notes |
| `.github/workflows/release-gui.yml` | 构建 GUI 并上传 GUI 资产 |
| `.github/workflows/release-cli.yml` | 发布 NuGet tool、构建 CLI 二进制、上传 CLI 资产、更新 CLI 分发渠道 |
| `.github/workflows/release.yml` | 迁移或拆分后保留为 GUI 工作流，或被新结构替代 |

## 关键文件清单

| 文件 | 操作 |
|------|------|
| `StarBlogPublisher.Cli/StarBlogPublisher.Cli.csproj` | 修改：添加 PackAsTool 和 NuGet 元数据 |
| `.github/workflows/release-entry.yml` | 新建：单点创建/更新 Release |
| `.github/workflows/release-gui.yml` | 新建或由现有 release.yml 拆分而来 |
| `.github/workflows/release-cli.yml` | 新建：CLI 发布与分发更新 |
| `.github/workflows/release.yml` | 修改或迁移：不再独占全部发布职责 |
| `bucket/starblog-publisher.json` | 保留：继续服务 GUI |
| `star-blog/scoop-bucket/bucket/starblog.json` | 外部仓库：CLI Scoop manifest |
| `star-blog/homebrew-tap/Formula/starblog.rb` | 外部仓库：CLI Homebrew formula |
| `README.md` | 修改：区分 GUI 与 CLI 安装方式 |

## 验证方式

### 本地验证

1. `dotnet pack` 生成 CLI tool 包
2. 本地安装 tool 并运行 `starblog --help`
3. 本地发布 CLI 自包含单文件包
4. 运行 CLI 只读命令做烟测

### CI 验证

1. 使用 `workflow_dispatch` 先跑一次非正式验证，不直接依赖正式 tag
2. 验证 `release-entry` 只创建一次 Release
3. 验证 GUI 和 CLI 都能把各自产物上传到同一个 Release
4. 验证 CLI workflow 不会覆盖 Release Notes
5. 验证 Scoop / Homebrew 更新脚本拿到的是 CLI 资产 URL 和正确 SHA256

### 正式发布验证

1. 发布正式 tag
2. 检查同一个 GitHub Release 页面同时包含 GUI 和 CLI 资产
3. 验证 NuGet tool 安装
4. 验证 CLI Scoop 安装
5. 验证 Homebrew 安装

## 非目标

本次计划不处理以下事项：

- GUI 与 CLI 合并成单一可执行文件
- 在第一阶段强行要求 CLI 全平台 Native AOT
- 让两个 workflow 同时创建或覆盖同一个 GitHub Release
