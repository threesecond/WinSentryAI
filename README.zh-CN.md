# WinSentryAI

**WinSentryAI** 是一款可携式 Windows 桌面诊断工具，用于查看 Windows 事件日志，并通过 AI 协助 IT 操作人员理解系统异常。

当前版本：**v0.5**  
状态：**开发里程碑，尚未正式 release**

语言：[English](README.md) | [繁體中文](README.zh-TW.md) | [简体中文](README.zh-CN.md)

## 软件介绍

WinSentryAI 面向实际 Windows 故障排查流程设计，会整合事件日志、系统信息快照、相关上下文事件，以及可选的 AI 分析，提供桌面化诊断工作流程。

它的目标是帮助 IT 人员更快理解问题，但实际修复决策仍由操作人员自行判断。

## 主要功能

- Windows 事件日志回溯加载与本地监控
- 事件筛选、搜索、详细信息与上下文事件采集
- 针对选中事件执行 AI 分析与 follow-up chat
- 支持 Gemini、OpenAI、Claude、Ollama 的 Bring Your Own Key 模式
- 云端 AI prompt 隐私遮蔽
- 系统信息快照
- 远程 Windows 事件日志回溯查询
- 系统托盘行为与 single-instance 启动处理
- English、繁體中文、简体中文 UI
- Light、Dark、System 主题选项

## 截图

事件日志页面可让操作人员筛选 Windows 事件、查看事件详细内容，并从选中的事件启动 AI 分析。

![Event Log view](Docs/Images/event-log-light.png)

AI 分析页面提供聚焦于事件的说明，并可通过 follow-up chat 追问实际修复方向。

![AI analysis follow-up chat](Docs/Images/ai-analyze-light.png)

设置页可使用已配置的 API key 获取可用 Gemini 模型，并选择分析时使用的模型。

![Gemini model selection](Docs/Images/gemini-api-light.png)

AI provider 设置支持 Gemini、OpenAI、Claude、Ollama，并提供本地隐私遮蔽选项。

![AI provider settings](Docs/Images/settings-ai-provider.light.png)

## 文档

操作细节集中在用户手册：

- [User Manual](Docs/user-manual.md)
- [使用手冊（繁體中文）](Docs/user-manual.zh-TW.md)
- [用户手册（简体中文）](Docs/user-manual.zh-CN.md)

用户手册包含安装需求、build 与运行方式、API key 设置、AI provider 免费/付费限制、远程连接设置、系统托盘行为、主题设置、已知限制与故障排查注意事项。

## 使用范围

WinSentryAI 只提供诊断辅助。它不会自动修复系统、不会修改事件日志，也不会根据 AI 输出自动执行修复命令。

采取任何处置前，AI 分析结果都应由合格 IT 人员自行判断。

## 授权

本项目预计采用 MIT License。
