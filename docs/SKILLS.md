# StarBlog Publisher AI Skill

本文件用于指导 AI Agent 使用 StarBlog Publisher 的 CLI 和 MCP 能力，自动完成博客文章发布工作流。

## 目标

当用户提出以下类型的请求时，AI 应优先使用本技能：

- 发布一个 Markdown 文件到 StarBlog 博客
- 先生成标题、摘要、Slug，再发布文章
- 查询分类并选择分类后发布
- 缺少分类时自动创建分类
- 先保存草稿，再让用户确认是否正式发布

本技能关注 CLI 和 MCP，不使用 GUI。

## 优先级规则

默认按以下顺序选择执行方式：

1. 优先使用 MCP
2. MCP 不可用时使用 CLI
3. 两者都不可用时，向用户说明缺失的前置条件

原因：

- MCP 更适合 AI 自动编排，参数清晰，返回结果更结构化
- CLI 适合终端环境或未接入 MCP 的场景
- CLI 的 `post publish --auto` 带交互确认，自动化时通常需要额外加 `-y`

## 能力边界

### MCP 已提供的能力

- `auth_login`
- `auth_status`
- `auth_logout`
- `category_list`
- `category_create`
- `post_publish`
- `post_get`
- `ai_optimize_title`
- `ai_generate_summary`
- `ai_suggest_tags`
- `ai_generate_slug`
- `ai_generate_cover_prompt`

### CLI 已提供的能力

- `starblog auth login`
- `starblog auth status`
- `starblog auth logout`
- `starblog category list`
- `starblog category create`
- `starblog post publish`
- `starblog post get`
- `starblog ai generate-summary`
- `starblog ai optimize-title`
- `starblog ai suggest-tags`
- `starblog ai generate-slug`

### 关键差异

- MCP 没有“一键自动挡发布”工具；AI 需要自己调用多个 AI tool，再调用 `post_publish`
- CLI 有 `starblog post publish --auto`，但默认会进入确认交互；无人值守时应配合 `-y`
- MCP 的 `post_publish` 需要 Markdown 文件绝对路径

## 前置条件

在执行发布前，AI 应确认以下条件：

1. StarBlog Publisher CLI 已安装，或 MCP Server 已正确接入
2. 后端地址可用
3. 已具备登录凭据，或当前已经登录
4. 待发布的 Markdown 文件存在
5. 如果需要 AI 生成标题、摘要、Slug，StarBlog Publisher 的 AI 配置已经可用

如果任一条件不满足，先补齐条件，再继续发布。

## 标准工作流

### 工作流总则

- 优先走“校验后发布”，不要直接假设环境已经登录
- 优先复用已有分类，不要无条件创建新分类
- 涉及元数据生成时，先产出标题、摘要、Slug，再发布
- 对高风险发布优先保存为草稿
- 发布完成后必须做一次结果校验

### 工作流步骤

#### 1. 检查认证状态

MCP：

- 先调用 `auth_status`
- 如果未登录，再调用 `auth_login`
- 当用户明确给出后端地址时，将地址一并传入 `auth_login`

CLI：

```bash
starblog auth status
starblog auth login --username <username> --password <password> --url <backend-url>
```

说明：

- 如果已经登录，不要重复登录
- 如果登录失败，停止发布并向用户返回错误信息

#### 2. 检查文章文件

要求：

- 文件必须存在
- 优先使用 Markdown 文件
- MCP 场景下传入绝对路径

建议检查项：

- 文件名是否可作为默认标题
- 文章正文是否为空
- 图片路径是否为本地相对路径或可访问路径

#### 3. 获取分类并选择目标分类

MCP：

- 调用 `category_list`
- 在返回结果中查找最匹配的分类
- 若不存在且用户允许创建，调用 `category_create`

CLI：

```bash
starblog category list
starblog category create --name "技术笔记" --parent-id 0
```

分类策略：

- 用户明确指定分类时，优先使用指定分类
- 用户只给出分类名称时，先查找名称匹配项
- 无匹配分类时，先询问是否允许创建；若任务本身已授权自动处理，则直接创建

#### 4. 生成文章元数据

当用户要求“自动优化”“自动生成摘要”“自动补全 Slug”时执行本步骤。

MCP 推荐顺序：

1. `ai_optimize_title`
2. `ai_generate_summary`
3. `ai_generate_slug`
4. 需要标签时再调用 `ai_suggest_tags`
5. 需要封面提示词时调用 `ai_generate_cover_prompt`

MCP 调用要点：

- `ai_optimize_title` 输入原始标题和可选正文
- `ai_generate_summary` 输入标题和正文
- `ai_generate_slug` 输入最终标题
- `ai_suggest_tags` 与 `ai_generate_cover_prompt` 属于增强能力，不阻塞发布

CLI 对照命令：

```bash
starblog ai optimize-title "原始标题" --content "文章正文"
starblog ai generate-summary /absolute/path/to/post.md
starblog ai generate-slug "最终标题"
starblog ai suggest-tags /absolute/path/to/post.md
```

注意：

- 如果 AI 功能未启用，不要反复重试 AI 命令
- AI 不可用时，回退到手工标题、空摘要或用户给定摘要，以及可选空 Slug

#### 5. 执行发布

MCP：

- 调用 `post_publish`
- 必填参数：`filePath`、`categoryId`
- 可选参数：`title`、`summary`、`slug`
- `publish=true` 表示直接发布，`publish=false` 表示保存草稿

CLI：

```bash
starblog post publish /absolute/path/to/post.md --category 1
starblog post publish /absolute/path/to/post.md --category 1 --draft
starblog post publish /absolute/path/to/post.md --category 1 --title "标题" --summary "摘要" --slug "my-slug"
starblog post publish /absolute/path/to/post.md --category 1 --auto -y
```

发布策略：

- 用户说“发布”时，默认正式发布
- 用户说“先存草稿”“先别公开”时，使用草稿模式
- 在不确定元数据质量、分类是否正确、或用户表达谨慎时，优先草稿

#### 6. 校验发布结果

MCP：

- 从 `post_publish` 返回结果中提取文章 ID
- 再调用 `post_get` 校验标题、Slug、状态、摘要是否符合预期

CLI：

```bash
starblog post get <article-id>
```

校验重点：

- 状态是否为“已发布”或“草稿”
- 标题是否为最终版本
- Slug 是否正确
- 摘要是否已写入

## MCP 推荐执行模板

当 AI 使用 MCP 时，推荐遵循以下顺序：

1. `auth_status`
2. 必要时 `auth_login`
3. `category_list`
4. 必要时 `category_create`
5. 读取本地 Markdown 内容
6. 必要时调用 `ai_optimize_title`
7. 必要时调用 `ai_generate_summary`
8. 必要时调用 `ai_generate_slug`
9. 调用 `post_publish`
10. 调用 `post_get`

这是最稳妥的自动发布主链路。

## CLI 推荐执行模板

当 AI 只能通过终端调用 CLI 时，推荐顺序如下：

```bash
starblog auth status
starblog category list
starblog ai optimize-title "原始标题" --content "文章正文"
starblog ai generate-slug "最终标题"
starblog post publish /absolute/path/to/post.md --category 1 --title "最终标题" --summary "摘要" --slug "final-slug"
starblog post get <article-id>
```

如果用户明确要求“自动挡”，可使用：

```bash
starblog post publish /absolute/path/to/post.md --category 1 --auto -y
```

但要知道：

- `--auto` 依赖 AI 配置
- 不加 `-y` 会进入交互确认
- 自动化环境应避免卡在交互输入

## 失败处理规则

### 登录失败

- 返回明确错误
- 不继续执行分类、AI、发布步骤

### 分类不存在

- 若允许自动创建，则创建后重试发布
- 若不允许自动创建，则向用户说明并等待决策

### AI 功能不可用

- 回退到非 AI 发布流程
- 使用文件名或用户给定标题
- 摘要和 Slug 可留空或由用户提供

### 发布失败

- 直接返回工具原始错误信息
- 不要伪造“发布成功”
- 若已经创建草稿或文章 ID 已返回，再用 `post_get` 确认实际状态

## 输出要求

AI 在完成发布任务后，应向用户返回：

- 发布结果：成功 / 失败
- 文章 ID
- 文章标题
- 发布状态：已发布 / 草稿
- Slug
- 如能拼出访问地址，可附上文章 URL

如果是失败，还应返回：

- 失败步骤
- 原始错误信息
- 已完成到哪一步

## 不建议的做法

- 不要使用 GUI 完成自动化发布
- 不要在未检查认证状态时直接发布
- 不要在 MCP 场景中假设存在 `post_publish --auto` 这种单一工具
- 不要在无 `-y` 的 CLI 自动挡场景下假设命令会无交互完成
- 不要在 AI 不可用时持续重试同一 AI 命令

## 示例任务映射

### 用户请求

“把这篇 Markdown 发布到技术分类，标题帮我优化一下，没有分类就自动创建。”

### AI 应执行

1. `auth_status`
2. 必要时 `auth_login`
3. `category_list`
4. 如果没有“技术”或目标分类，调用 `category_create`
5. 读取 Markdown 内容
6. `ai_optimize_title`
7. `ai_generate_summary`
8. `ai_generate_slug`
9. `post_publish`
10. `post_get`

### 用户请求

“先把这篇文章存成草稿，我只要你帮我自动补摘要和 slug。”

### AI 应执行

1. `auth_status`
2. `category_list`
3. 读取 Markdown 内容
4. `ai_generate_summary`
5. `ai_generate_slug`
6. `post_publish`，并设置 `publish=false`
7. `post_get`

## 配置示例

### 启动 MCP Server

```bash
starblog mcp
```

### Claude Desktop / Cursor MCP 配置

```json
{
  "mcpServers": {
    "starblog": {
      "command": "starblog",
      "args": ["mcp"]
    }
  }
}
```

如果通过 dotnet tool 安装，则可使用：

```json
{
  "mcpServers": {
    "starblog": {
      "command": "dotnet",
      "args": ["tool", "run", "starblog", "mcp"]
    }
  }
}
```

## 一句话准则

对 AI 来说，StarBlog Publisher 的最佳自动发布路径是：先认证，再找分类，必要时生成标题/摘要/Slug，随后发布，最后校验结果；MCP 优先，CLI 兜底。