# CI/CD 指南

## 发布架构

发布流程由三个工作流协作完成，均由 `v*.*.*` 标签触发：

| 工作流 | 文件 | 职责 |
|--------|------|------|
| Release 入口 | `release-entry.yml` | 创建 GitHub Release，生成 Release Notes |
| GUI 发布 | `release-gui.yml` | 构建 GUI AOT 产物，上传到 Release |
| CLI 发布 | `release-cli.yml` | 发布 NuGet 包、构建 CLI 二进制、更新 Scoop/Homebrew |

三个工作流并行运行，`release-entry.yml` 负责创建 Release，GUI 和 CLI 工作流只上传资产，不写 Release 元数据。

## 需要配置的 Secrets

### 1. NUGET_GALLERY_TOKEN

用于将 CLI 作为 .NET Global Tool 发布到 NuGet.org。

**获取方式：**

1. 登录 [nuget.org](https://www.nuget.org/)，进入 Account Settings → API Keys
2. 点击 Create，选择 Push new packages and package versions
3. 设置 Glob pattern 为 `*`（或限制为 `StarBlogPublisher.Cli`）
4. 复制生成的 Key

**配置位置：**

`Settings → Secrets and variables → Actions → New repository secret`

- Name: `NUGET_GALLERY_TOKEN`
- Value: 上面复制的 Key

### 2. GH_PAT

用于 CLI 工作流向外部仓库推送 Scoop manifest 和 Homebrew formula 更新。默认的 `GITHUB_TOKEN` 只有当前仓库的权限，无法推送外部仓库。

**获取方式：**

1. 进入 GitHub Settings → Developer settings → Personal access tokens → Fine-grained tokens
2. 点击 Generate new token
3. 设置 Token name，如 `starblog-cicd`
4. Repository access 选择 Only select repositories，添加：
   - `star-blog/scoop-bucket`
   - `star-blog/homebrew-tap`
5. Permissions → Repository permissions，设置：
   - Contents: Read and write
6. 点击 Generate token，复制生成的 token

**配置位置：**

`Settings → Secrets and variables → Actions → New repository secret`

- Name: `GH_PAT`
- Value: 上面复制的 token

### 3. GITHUB_TOKEN（自动）

`GITHUB_TOKEN` 由 GitHub Actions 自动注入，无需手动配置。当前工作流已通过 `permissions: contents: write` 声明了所需权限，用于：

- 创建和更新 GitHub Release
- 上传 Release 资产

## 外部仓库

CLI 分发依赖两个外部仓库，已在 `star-blog` 组织下创建：

| 仓库 | 用途 | 地址 |
|------|------|------|
| `scoop-bucket` | Scoop 包管理器 manifest | https://github.com/star-blog/scoop-bucket |
| `homebrew-tap` | Homebrew formula | https://github.com/star-blog/homebrew-tap |

这两个仓库由 CLI 发布工作流自动更新，首次发布后会自动填充正确的版本号和 SHA256 哈希。

## 发布流程

### 正式发布

```bash
# 1. 确保版本号与代码一致（build.py 等处）
# 2. 打标签并推送
git tag v2.1.0
git push origin v2.1.0
```

推送后三个工作流自动运行：

1. `release-entry.yml` 创建 Release 并生成 Release Notes
2. `release-gui.yml` 构建 GUI AOT 产物并上传
3. `release-cli.yml` 依次完成：
   - `dotnet pack` + `dotnet nuget push` 发布到 NuGet.org
   - 构建 4 个平台的 CLI 自包含二进制（win-x64, linux-x64, osx-x64, osx-arm64）
   - 上传 CLI 二进制到 Release
   - 更新 `scoop-bucket` 的 `bucket/starblog.json`
   - 更新 `homebrew-tap` 的 `Formula/starblog.rb`

### 手动验证

可在 Actions 页面手动触发 `workflow_dispatch` 进行验证（需在工作流文件中添加该触发器）。

## 分发渠道速查

发布完成后，用户可以通过以下方式安装：

| 渠道 | 命令 | 需要 .NET 运行时 |
|------|------|-------------------|
| NuGet | `dotnet tool install --global StarBlogPublisher.Cli` | 是 |
| Homebrew | `brew tap star-blog/tap && brew install starblog` | 否 |
| Scoop | `scoop bucket add starblog https://github.com/star-blog/scoop-bucket.git && scoop install starblog` | 否 |
| GitHub Release | 直接下载 | 否 |

## 旧工作流处理

原有的 `release.yml` 职责已被 `release-gui.yml` 取代。确认新流程稳定后可删除：

```bash
git rm .github/workflows/release.yml
git commit -m "chore: remove legacy release.yml, replaced by release-gui.yml"
```
