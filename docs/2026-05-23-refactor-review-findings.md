# 2026-05-23 重构评审问题记录

## 评审范围

评审对象：提交 `69d4743 feat: 提取 Core 共享库，新增 CLI 和 MCP Server 支持`

本记录聚焦以下内容：

1. 重构后 GUI / CLI / MCP 的行为一致性
2. 认证、发布、AI 调用等关键流程的语义正确性
3. 当前代码中已暴露的问题与建议修复方案

---

## 问题 1：CLI / MCP 的 logout 和 status 语义不正确

### 严重级别

高

### 现象

当前 `auth logout` 只清理了进程内登录状态，但没有清理持久化凭据。

这会导致：

1. 当前进程执行 `auth logout` 后看起来已登出
2. 下一次 CLI 新进程启动时，只要本地还保存着用户名和密码，就会被 `EnsureLoggedInAsync()` 自动重新登录
3. `auth status` 返回的状态无法稳定表达“用户显式登出”这个意图
4. MCP Tool 的 `auth_logout` 与 `auth_status` 也存在同样问题

### 代码位置

1. `StarBlogPublisher.Core/Services/Application/AuthApplicationService.cs`
2. `StarBlogPublisher.Cli/Commands/AuthCommand.cs`
3. `StarBlogPublisher.Cli/Tools/AuthTools.cs`

### 根因

当前认证模型混合了两种语义：

1. “是否保存凭据”
2. “是否处于已登录状态”

但 `Logout()` 只影响内存态 `GlobalState`，而不影响保存的凭据；随后 `EnsureLoggedInAsync()` 又会在检测到凭据存在时自动登录，导致“登出”无法跨进程成立。

### 影响

1. 用户无法可靠地通过 CLI 退出登录
2. 自动化脚本中的认证状态不可预测
3. MCP 工具层的 auth 控制语义失真
4. 后续如果加入 token 持久化，会进一步放大状态混乱问题

### 建议修复方案

推荐二选一，并在文档中明确约定：

#### 方案 A：将 logout 定义为“退出当前会话，但保留凭据”

如果采用这个方案，则需要同步调整：

1. `auth status` 明确区分“已保存凭据但当前未登录”
2. `EnsureLoggedInAsync()` 不应在所有场景下无条件自动登录
3. 自动登录应只用于显式允许的命令路径，例如 `post publish`
4. `auth status` 不应触发隐式登录

这是对当前设计侵入较小的修法。

#### 方案 B：将 logout 定义为“注销并清除本地凭据”

如果采用这个方案，则需要在 `Logout()` 或 CLI/MCP 的 logout 调用链中同时清理：

1. `AppSettings.Username`
2. `AppSettings.Password`
3. 可能的 token 持久化信息
4. `GlobalState`

这个方案语义更直接，但会改变用户使用体验。

### 推荐方向

优先建议采用方案 A，但要把“自动登录”收口到明确的业务命令中，不能让 `status` 和通用读操作都隐式触发认证副作用。

---

## 问题 2：`post publish --draft` 在部分场景下不会真正保存为草稿

### 严重级别

高

### 现象

CLI 已提供 `post publish --draft` 能力，但当前实现只有在 Markdown 图片处理后内容发生变化时，才会发送 `Update` 请求并写入 `IsPublish = publish`。

这意味着：

1. 如果文章没有本地图片
2. 或者图片处理后正文未发生变化

则草稿状态可能不会被持久化到后端，最终行为取决于服务端创建接口默认值，而不是 CLI 参数。

### 代码位置

1. `StarBlogPublisher.Cli/Commands/PostCommand.cs`
2. `StarBlogPublisher.Core/Services/Application/ArticlePublishApplicationService.cs`

### 根因

当前 `PublishAsync()` 将“是否更新正文内容”与“是否写入最终发布状态”耦合在一起。

实际业务上这两个决策应当分离：

1. 是否需要更新正文
2. 是否需要设置草稿/发布状态

即使正文没有变化，`publish` 参数本身仍然可能要求执行一次更新，或者在创建时就明确写入状态。

### 影响

1. CLI 对外暴露的 `--draft` 参数语义不可靠
2. MCP 后续如果复用同一服务，也会继承同样问题
3. 用户会误以为文章已保存为草稿，实际却可能已发布或处于服务端默认状态

### 建议修复方案

推荐以下任一实现：

#### 方案 A：创建文章时就显式传递状态

如果后端创建接口支持，优先在 `PostCreationDto` 中明确传入：

1. `Status`
2. 或与发布状态等价的字段

这样可以从根上避免“创建后再修正状态”。

#### 方案 B：只要 `publish` 影响结果，就执行 update

将更新条件从：

1. `processedContent != content`

改为类似：

1. `processedContent != content || 需要修正发布状态`

其中“需要修正发布状态”应根据服务端创建后的实际状态和目标状态比较得出。

### 推荐方向

优先建议方案 A。如果后端接口约束不允许，再采用方案 B。

---

## 问题 3：GUI 的 Slug 生成流程引入了重复 AI 请求和同步阻塞

### 严重级别

中

### 现象

当前 GUI 的 `GenerateSlug()` 会先流式消费一次 AI 结果，然后再次调用 `GenerateSlugAsync(...).Result`。

这会带来：

1. 同一动作触发两次 AI 请求
2. 重复消耗 token 和时间
3. 第二次调用使用 `.Result` 阻塞 UI 线程
4. 在网络或模型响应较慢时，界面体验变差

### 代码位置

1. `StarBlogPublisher/ViewModels/MainWindowViewModel.cs`

### 根因

原来的 GUI 逻辑是在第一次流式生成完成后本地执行清洗；重构后改成又调用了一次应用服务的一次性方法，导致重复请求。

### 影响

1. AI 成本增加
2. 用户等待时间变长
3. UI 线程存在阻塞风险
4. 行为与重构前相比发生了退化

### 建议修复方案

直接保留第一次流式结果，并在本地完成最终清洗，不要再次调用 AI。

建议改为：

1. 流式拼接 `ArticleSlug`
2. 流结束后调用一个纯本地的 slug 清洗方法
3. 避免 `.Result`
4. 保持整个流程纯异步

如果希望清洗逻辑复用，可以将“清理 slug”提取为纯函数工具方法，而不是再次调用会触发网络请求的方法。

---

## 其他观察

### 1. 当前构建已通过，但缺少行为级回归验证

本次评审期间已执行解决方案构建，构建通过，但现有验证仍主要停留在编译层。

建议为以下路径增加至少一条自动化或半自动冒烟验证：

1. CLI `auth login -> auth status -> auth logout -> auth status`
2. CLI `post publish --draft`
3. GUI 生成 Slug
4. MCP `auth_status` 和 `post_publish`

### 2. MCP 的 stdout 协议污染问题已基本改善

本次重构已经将多处服务输出从 `Console.WriteLine` 改为日志或 Trace，这一方向是正确的。

但仍建议继续检查：

1. GUI 层是否仍有共享代码路径会写 stdout
2. CLI/MCP 共用代码路径是否存在未替换的控制台输出
3. Tool 返回值是否足够结构化

---

## 建议处理顺序

建议按以下优先级修复：

1. 修复 CLI / MCP 的认证状态语义
2. 修复 `post publish --draft` 的状态持久化问题
3. 修复 GUI `GenerateSlug()` 的重复 AI 请求
4. 补充针对以上问题的回归验证

---

## 完成标准

当以下条件满足时，可认为本轮重构的关键行为问题已收敛：

1. `auth logout` 的行为在 CLI 和 MCP 中语义明确且可跨进程验证
2. `auth status` 不再依赖隐式登录副作用
3. `post publish --draft` 能稳定生成草稿，无论正文是否被图片处理改写
4. GUI 的 Slug 生成功能只发起一次 AI 请求，且不阻塞 UI 线程
5. 至少补充一组覆盖上述行为的验证步骤或测试用例
