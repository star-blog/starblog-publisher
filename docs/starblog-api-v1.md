# StarBlog API v1 适配

2026-11-04，Publisher 的 StarBlog 调用统一切换到已上线的新版 `apps/api`。GUI、CLI、MCP 共用 Core 应用服务。后端地址仍填写站点根地址，例如 `https://blog.deali.cn/`，不要追加 `/api/v1`。

契约依据：[重构分支](https://github.com/Deali-Axy/StarBlog/tree/ca1223404862ce46e5e96605fcd8afd05b39f93a/apps/api/src/StarBlog.Api)。本次迁移现有客户端功能，不增加后台管理功能。

## 请求与响应

| 功能 | 新接口 | 成功响应 |
| --- | --- | --- |
| 登录 | `POST /api/v1/auth/tokens` | `{ token, expiration }` |
| 文章列表 | `GET /api/v1/posts` | `{ items, page, pageSize, totalCount, totalPages }` |
| 文章详情 | `GET /api/v1/posts/{id}` | 文章对象 |
| 创建文章 | `POST /api/v1/posts` | 文章对象 |
| 更新文章 | `PUT /api/v1/posts/{id}` | 文章对象 |
| 删除文章 | `DELETE /api/v1/posts/{id}` | 204，无响应体 |
| 上传正文图片 | `POST /api/v1/posts/{id}/images` | `{ url }`，请求为 multipart |
| 分类树 | `GET /api/v1/categories/tree` | 节点数组；空树允许无响应体 |
| 分类列表 | `GET /api/v1/categories` | 分页对象，包含隐藏分类 |
| 分类词云 | `GET /api/v1/categories/word-cloud` | `[{ id, name, count }]` |
| 创建分类 | `POST /api/v1/categories` | 分类对象 |
| 更新分类 | `PUT /api/v1/categories/{id}` | 分类对象 |
| 删除分类 | `DELETE /api/v1/categories/{id}` | 204，无响应体 |
| 随机封面来源 | `GET /api/v1/site/photos/random` | 摄影对象，原图为 `/media/photography/{filePath}` |

成功响应不再读取 `ApiResponse<T>.Data`。失败使用 HTTP 状态码和 `ProblemDetails.detail/title`。认证方式仍为 `Authorization: Bearer <token>`。

## 业务行为

- 首次发文先创建草稿，上传并替换本地图片，再更新正文和最终发布状态。图片地址读取 `url`。
- 文章列表不含全文；编辑和 pull 继续请求单篇详情。新版 `categoryId/categoryName` 映射到客户端分类展示和本地 sidecar。
- 排序使用独立的 `sortBy`、`sortType`，不再发送 `-LastUpdateTime`。保留分页、搜索、分类、发布状态筛选，并支持 `includeDescendants` 查询字段。
- 分类管理逐页读取完整列表；显隐通过 `PUT` 更新，并保留分类名称和父级。新版没有独立的 `SetVisible/SetInvisible` 或按 ID 获取分类接口。
- 删除成功接受 204。没有等价端点且没有调用方的旧 `GetImages` 接口已移除。
- 文章链接使用 `/p/{slug}`；没有 slug 时使用 `/blog/{id}`。CLI 直接使用应用服务返回的链接。
- 公众号和封面工作室的 StarBlog 随机图来源改为当前配置站点的新版摄影接口；其他图源继续使用原有 URL 模板。
- GUI 与 CLI 的 AOT / 裁剪配置同步保留新版分页、请求、响应模型及接口。
- MCP 工具注册显式使用 SDK 的 `toolTypes` 重载，避免把类型数组当作单个工具实例，导致握手后没有可调用工具。

## 验证

`StarBlogApiContractTests` 使用真实 Refit 客户端与本地模拟 HTTP 验证路由、请求字段、分页、裸对象/数组、204、ProblemDetails、multipart 和完整发布流程，以及随机摄影地址和取消传播。

全量测试需将 `NavigationMenuIconConverterTests` 放入独立宿主运行，避免其 Win32 初始化与其他 Avalonia 测试争用 UI 线程。其余测试可串行运行：

```sh
dotnet test StarBlogPublisher.Tests --filter 'FullyQualifiedName!~NavigationMenuIconConverterTests' -- xUnit.ParallelizeTestCollections=false xUnit.MaxParallelThreads=1
dotnet test StarBlogPublisher.Tests --filter 'FullyQualifiedName~NavigationMenuIconConverterTests'
```

线上验证仅调用匿名只读接口，已核对文章列表/详情、分类分页/树、词云和随机摄影原图下载。线上账号登录和内容增删改未使用真实凭据执行；写操作由本地 HTTP 契约测试覆盖。

Windows Native AOT CLI 也已发布并连接本机模拟 API 验证：虚拟账号登录、Bearer 鉴权、分类增改删查、文章查询/带图片发布/删除。该产物的 MCP stdio 握手以及 `auth_login`、`category_list`、`post_list/get/publish/delete` 调用通过。
