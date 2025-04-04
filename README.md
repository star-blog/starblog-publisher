# StarBlog Publisher

![Avalonia](https://img.shields.io/badge/UI-Avalonia-blue)
![.NET](https://img.shields.io/badge/.NET-8.0-purple)
![License](https://img.shields.io/badge/License-MIT-green)

StarBlog Publisher 是一款专为 [StarBlog博客系统](https://github.com/Deali-Axy/StarBlog) 设计的专业文章发布工具，提供比传统打包上传更便捷的文章发布方式。

它支持Markdown格式文章的预览和发布，提供直观的用户界面，让您能够轻松管理和发布博客内容。

基于C#和.NET 8.0构建，充分利用Microsoft.Extensions.AI框架，集成了多种领先的AI大模型（包括OpenAI的ChatGPT、Anthropic的Claude和DeepSeek等），为内容创作提供智能辅助功能。

跨平台设计让您可以在Windows、macOS和Linux上享受一致的体验，展现了.NET生态系统对现代AI应用开发的强大支持。

## 界面预览

### 主界面（浅色模式）

![主界面-浅色模式](docs/images/主界面-浅色模式.jpg)

### 主界面（深色模式）

![主界面-深色模式](docs/images/主界面-深色模式.jpg)

### 设置界面

![设置界面](docs/images/设置界面.jpg)

## 解决Markdown写作的痛点

在使用Markdown进行博客写作时，我们经常会遇到以下痛点：

- **图片处理繁琐**：使用Typora等编辑器写作时，本地图片的管理和上传是一个常见的痛点。每次发布文章都需要单独处理图片资源
- **发布流程复杂**：传统的博客发布方式通常需要多个步骤，包括导出文章、处理图片、登录后台、填写信息等
- **多平台适配困难**：将同一篇文章发布到不同平台（如个人博客、知乎、公众号等）时，需要重复进行格式调整
- **版本管理不便**：文章的多次修改和更新难以追踪和管理

## StarBlog Publisher的优势

相比于传统的文章发布方式，StarBlog Publisher提供了以下优势：

- **一键发布**：直接从本地Markdown文件发布到博客系统，无需手动打包上传
- **自动图片处理**：自动识别并处理Markdown中的本地图片，上传至服务器并更新链接
- **实时预览**：所见即所得的编辑体验，确保发布效果符合预期
- **跨平台复制**：发布后可一键复制格式化内容，方便发表到知乎、公众号等其他平台
- **AI辅助创作**：集成多种AI大模型，提供标题润色、内容总结等智能辅助功能
- **本地与云端同步**：保持本地文章与已发布文章的同步，便于持续更新

虽然StarBlog已经支持文章打包上传（将Markdown和图片文件一并打包为ZIP格式上传），但StarBlog Publisher更进一步简化了这一流程，实现了真正的一站式发布体验，大幅提升了内容创作者的工作效率。

## 功能特点

- **Markdown支持**：完整支持Markdown格式，包括图片、链接、代码块等
- **图片上传**：自动处理Markdown中的本地图片，上传至博客服务器
- **文章预览**：实时预览Markdown渲染效果
- **文章管理**：支持文章的创建、编辑、发布和删除
- **分类管理**：支持对文章进行分类
- **AI辅助**：集成多种AI模型（OpenAI、Claude、DeepSeek等），提供基于大模型的文章标题润色、文章总结和简介自动生成功能，大幅提升内容创作效率
- **词云生成**：可视化展示博客内容关键词
- **主题切换**：支持亮色/暗色主题切换
- **代理设置**：支持配置HTTP代理，解决网络访问问题
- **自定义后端**：可配置自定义的博客后端API地址

## 技术栈

- **UI框架**：Avalonia 11.2.1
- **开发语言**：C# (.NET 8.0)
- **MVVM框架**：CommunityToolkit.Mvvm 8.2.1 + ReactiveUI 19.5.41
- **Markdown处理**：Markdig 0.40.0 + Markdown.Avalonia 11.0.3
- **HTTP客户端**：Refit 8.0.0
- **AI集成**：Microsoft.Extensions.AI 9.3.0
- **对话框**：MessageBox.Avalonia 3.1.5

## 安装与使用

### 系统要求

- Windows 10 或更高版本
- .NET 8.0 运行时

### 安装步骤

1. 从[Releases](https://github.com/star-blog/starblog-publisher/releases)页面下载最新版本
2. 解压缩下载的文件
3. 运行 `StarBlogPublisher.exe`

### 配置说明

1. 首次运行时，点击设置按钮配置博客后端API地址
2. 如需使用AI功能，请在设置中配置相应的AI提供商和API密钥
3. 如有网络访问问题，可配置HTTP代理

## 开发指南

### 环境准备

- Visual Studio 2022 或更高版本
- .NET 8.0 SDK

### 构建步骤

1. 克隆仓库
   ```
   git clone https://github.com/star-blog/starblog-publisher.git
   ```

2. 使用Visual Studio打开解决方案文件 `StarBlogPublisher.sln`

3. 还原NuGet包

4. 构建并运行项目

## 贡献指南

欢迎贡献代码、报告问题或提出新功能建议！请遵循以下步骤：

1. Fork 本仓库
2. 创建您的特性分支 (`git checkout -b feature/amazing-feature`)
3. 提交您的更改 (`git commit -m 'Add some amazing feature'`)
4. 推送到分支 (`git push origin feature/amazing-feature`)
5. 打开一个 Pull Request

## 许可证

本项目采用 MIT 许可证 - 详情请参阅 [LICENSE](LICENSE) 文件

## 联系方式

- 项目作者：[Deali-Axy](https://github.com/Deali-Axy)
- 电子邮件：dealiaxy@gmail.com
- 项目主页：[StarBlog Publisher](https://github.com/star-blog/starblog-publisher)
- 配套博客系统：[StarBlog](https://github.com/Deali-Axy/StarBlog)

## 更新记录

### 1.2

- 更新Avalonia相关依赖到11.2.6版本
- 预览窗口引入双栏布局，左侧显示原始Markdown内容，右侧显示HTML预览

### 1.1

- 优化对 AOT 的支持，目前在 AOT 下已经正常可用了

### 1.0

- 第一个发布的版本

---

**StarBlog Publisher** - 为StarBlog打造的专业发布工具，让博客发布变得简单高效！

