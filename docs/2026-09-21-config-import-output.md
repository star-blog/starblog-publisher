# StarBlog Publisher 配置导出 / 导入

## Goal
在设置页新增独立「备份」分区，支持把当前 `AppSettings`（含密码、API Key、微信密钥等）导出为可迁移的 JSON，并在二次确认后导入、立即写回生效。不包含登录 JWT，也不包含文章工作区历史。

## Decisions (confirmed)
| 项 | 选择 |
|---|---|
| 敏感字段 | 完整导出（含明文密钥） |
| 入口 | 设置页左侧导航新增「备份」分区 |
| 导入生效 | 选文件 → 二次确认 → 立即写入 `settings.json` 并刷新 |
| 范围 | 仅 `AppSettings` |

## Why a portable format (not raw settings.json)
本机 `settings.json` 里密码 / 微信密钥等使用 `EncryptionService`（Windows DPAPI + 机器/用户熵，或跨平台 AES + 机器密钥）。直接拷贝 `Encrypted*` 字段到另一台机器会解密失败。

因此导出文件必须是**可移植文档**：
- 密钥以明文写出（用户已确认接受）
- 带格式标识与版本号，避免误导入任意 JSON
- 导入时在本机重新加密后走现有 `TryUpdate` 写回路径

`AIProfile.Key` 在现有磁盘格式里本就是明文；可移植文档对所有密钥统一用明文，避免半加密半明文的歧义。

## Portable document shape
建议文件扩展名：`.json`（建议默认名 `starblog-settings-yyyyMMdd.json`）。

```json
{
  "format": "starblog-publisher-settings",
  "version": 1,
  "exportedAt": "2026-09-21T12:00:00+08:00",
  "settings": {
    "useProxy": false,
    "proxyType": "http",
    "proxyHost": "",
    "proxyPort": 0,
    "proxyTimeout": 30,
    "useCustomBackend": false,
    "backendUrl": "",
    "username": "...",
    "password": "...",
    "backendTimeout": 30,
    "enableAI": true,
    "aiProvider": "openai",
    "aiKey": "...",
    "aiModel": "...",
    "aiApiBase": "",
    "currentAIProfile": "默认",
    "aiProfiles": [
      { "name": "默认", "enableAI": true, "provider": "openai", "key": "...", "model": "...", "apiBase": "" }
    ],
    "weChatDefaultTheme": "newspaper",
    "currentWeChatAccountId": "...",
    "weChatAccounts": [
      {
        "id": "...",
        "name": "默认公众号",
        "appId": "...",
        "appSecret": "...",
        "apiBaseUrl": "https://api.weixin.qq.com/",
        "apiAuthorization": "",
        "author": ""
      }
    ],
    "isDarkTheme": false,
    "enableRegexImageParsing": false,
    "editorFontSize": 14,
    "editorWordWrap": true,
    "editorShowLineNumbers": false
  }
}
```

约定：
- `format` 必须为 `starblog-publisher-settings`
- `version` 当前仅接受 `1`；更高版本给出可读错误
- 属性名可用 camelCase（Source Generator / `JsonSerializerOptions` 与现有 Core 序列化风格对齐即可）
- **不**写出本机 `Encrypted*` 字段

兼容策略（可选增强，首版可不做）：若用户误选了本机 `settings.json`（无 wrapper），可尝试用现有 `AppSettings.DeserializeSnapshot` 解析并提示「这是本机配置文件，密钥可能无法跨机器迁移」。首版建议：**只接受 portable wrapper**，错误信息写清楚，降低误导。

## Architecture
```
SettingsView (备份分区)
  └─ SettingsViewModel.Export/Import commands
       └─ AppSettingsPortableTransfer (Core, 纯逻辑)
            ├─ CreateDocument(AppSettings) → JSON
            ├─ ParseDocument(json) → PortableAppSettingsDocument
            └─ ApplyTo(AppSettings via TryUpdate)
```

业务逻辑放在 `StarBlogPublisher.Core`，GUI 只负责文件选择、确认对话框、Toast / Reload。符合「ViewModel 不编排核心业务」的现有原则。

### Core
新增文件建议：
- `StarBlogPublisher.Core/Services/AppSettingsPortableTransfer.cs`
- 同文件或旁侧 DTO：`PortableAppSettingsDocument` / `PortableAppSettingsPayload` / `PortableWeChatAccount`
- `AppSettingsJsonContext` 注册上述类型（AOT / source gen 友好）

API 草图：

```csharp
public static class AppSettingsPortableTransfer {
    public const string FormatId = "starblog-publisher-settings";
    public const int CurrentVersion = 1;

    public static string ExportJson(AppSettings settings);
    public static bool TryParse(string json, out PortableAppSettingsDocument? document, out string? error);
    public static bool TryApply(AppSettings target, PortableAppSettingsDocument document, out string? error);
}
```

`TryApply` 行为：
1. 校验 format / version
2. 把 payload 映射为内存候选（明文 → 赋值给 `Password` / `AIKey` / `AppSecret` 等属性，触发本机加密）
3. 做与设置页接近的轻量校验（URL、代理端口、微信账号名非空、超时范围等）；失败则不写盘
4. 调用 `target.TryUpdate(...)` 整体替换字段；尊重现有 `HasLoadError` 保护（加载失败时禁止写回）
5. 成功后 `SettingsChanged` 已由 `TryUpdate` 触发

`ExportJson`：从当前 `AppSettings` 解密读出明文密钥写入 document；`WriteIndented = true`。

### GUI — Settings「备份」分区
现有导航索引：`0 常规 / 1 博客连接 / 2 微信公众号 / 3 AI 创作 / 4 网络代理`。

新增：
- `ListBoxItem`：「备份」
- `SelectedSection == 5` → `IsBackupSection`
- 分区内容：
  - 标题 + 说明：「导出文件包含账号密码、API Key、微信密钥等敏感信息，请妥善保管。」
  - 「导出配置」按钮
  - 「导入配置」按钮
  - 次要提示：导入会覆盖当前全部已保存设置；登录会话与文章工作区不受影响

底栏「保存 / 取消」对备份分区仍显示即可（备份操作不走草稿 dirty 状态）。

#### 导出流程
1. `StorageProvider.SaveFilePickerAsync`（参考 `PublishViewModel.Document`）
2. 默认文件名 `starblog-settings-yyyyMMdd.json`
3. `AppSettingsPortableTransfer.ExportJson(AppSettings.Instance)` → 原子写文件
4. Toast 成功；失败 Toast 错误

#### 导入流程
1. 若设置页 `HasChanges`：确认文案中明确「未保存的编辑将被丢弃」
2. `OpenFilePickerAsync` 选 `.json`
3. 读文件 → `TryParse`；失败则 Toast / 分区内错误提示
4. `FAContentDialog` 二次确认：
   - 标题：「导入配置？」
   - 内容：将覆盖当前已保存配置（含密钥）；此操作立即生效
   - 主按钮：「导入并应用」 / 关闭：「取消」
5. `TryApply` → 成功则 `Reload()`，主题等经现有 `SettingsChanged` 链路生效
6. Toast：「配置已导入并应用」

导入**不**进入草稿再保存；与用户选择的「确认后立即生效」一致。

### 不在范围内
- CLI `config export/import`（可后续加，Core API 已可复用）
- `GlobalState` JWT / 登录态
- 文章工作区 / `workspace.json`
- 导出时可选脱敏（本轮固定含密钥）
- 导入合并策略（整表替换，不做字段级 merge）

## File touch list
| 区域 | 文件 |
|---|---|
| Core | 新增 `AppSettingsPortableTransfer.cs`（+ DTO）；更新 `AppSettingsJsonContext.cs` |
| GUI VM | `SettingsViewModel.cs` / `SettingsViewModel.Editing.cs`：section 标志、Export/Import 命令 |
| GUI View | `SettingsView.axaml`：导航项 + 备份分区 UI |
| 单元测试 | 新增 `AppSettingsPortableTransferTests.cs`（round-trip、坏 format/version、校验失败不写盘） |
| 桌面测试 | `WorkspaceScenarios.SettingsLayout`：section 循环 `0..5` → `0..6`；可选截图 `settings-5` |
| 文档（可选） | `docs/testing.md` 补一句设置导入导出覆盖点 |

尽量不改 `AppSettings` 磁盘格式与 `TryUpdate` 语义；只新增旁路 portable 层。

## Validation / edge cases
- 空文件、非 JSON、缺 `settings`、错误 `format`、不支持的 `version`
- `HasLoadError == true` 时导入失败并提示先修复本机配置文件
- 导入文件中微信账号列表为空：沿用 `EnsureWeChatAccounts` 补默认账号
- AI profiles 为空：导入后补默认方案（与 `MigrateToProfiles` 一致）
- 文件选择取消：静默返回
- 写导出文件权限失败：Toast 错误，不改内存状态
- 导入成功后：设置草稿与磁盘一致（`Reload` + `AcceptChanges`）

## Tests
1. **Round-trip**：构造带代理 / 后端凭据 / 多 AI 方案 / 多微信账号 / 主题的 `AppSettings` → Export → 新实例 Apply → 明文字段等价
2. **Reject**：错误 format、version=2、缺 settings
3. **No write on invalid**：校验失败时目标 `settings.json` 不变（用 `STARBLOGPUBLISHER_SETTINGS_PATH` 隔离，与现有测试一致）
4. **GUI smoke**：备份分区可切换；桌面测试导航项数量更新

## Implementation order
1. Core DTO + `AppSettingsPortableTransfer` + source-gen 注册 + 单元测试
2. SettingsViewModel 命令与 section 绑定
3. SettingsView.axaml 备份分区 UI（说明 + 两按钮）
4. 更新 DesktopTests section 范围
5. 手动冒烟：导出 → 改几项并保存 → 导入原文件 → 确认恢复

## Risks
- **明文密钥落盘**：UI 必须醒目提示；文件由用户保管
- **整表覆盖**：无撤销栈；确认对话框是主要防护（可选后续：导入前自动备份一份到同目录 `.bak`，首版可不做）
- **与未保存草稿冲突**：确认文案需写明将丢弃未保存编辑
