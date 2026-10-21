# 高级 Java 卡牌扩展 API v1

这是一条不局限于效果白名单的开发路径。v1.3 的 AI 创作默认使用自由设计，自动生成并编译 Java；也可以按本说明手工开发。普通效果模式仍输出声明式卡牌，由卡牌的 JAVA_EXTENSION 节点调用已安装的代码扩展。

实现 com.stseditor.runtime.CardExtension 的 onUse(CardExtensionContext context)。context 提供当前自定义卡、玩家、目标、升级后的 amount，以及 parameters。代码在游戏线程执行，可调用游戏动作系统，也可以创建自己的 AbstractPower 子类接入回合、抽牌、出牌等生命周期。

例如 sdk/examples/energy-echo 会根据打出时剩余能量计算结果，没有为这个公式新增内置效果节点。

## 构建和安装

需要 JDK 9+，编译目标为 Java 8。使用 AI 自由设计或手动代码扩展时需要 JDK，建议配置 JDK 17 与 JAVA_HOME。原版游戏 jar 从用户本地读取，不随 SDK 分发。

在 sdk 目录运行：

```powershell
.\build-extension.ps1 -SourceDir .\examples\energy-echo\src -Id example.energy_echo -ClassName user.extensions.EnergyEchoExtension -Name 能量回声 -GameDir "D:\Steam\steamapps\common\SlayTheSpire" -Jdk "C:\Java\jdk-17" -Install
```

脚本生成 JAR、SHA-256 和 *.extension.json，只有使用 -Install 才复制到游戏的 sts-card-editor/extensions。清单必须 enabled=true、apiVersion=1，JAR 哈希必须吻合。AI 自由设计在生成阶段只编译，点击“校验并保存到卡库”后才安装，游戏注册卡牌时加载、出牌时调用代码。源码与历次模型输出保存在编辑器 data/generated 中。

安装完成后从自定义卡库导入 examples/energy-echo/card.json，再加入本局。效果列表选择 JAVA_EXTENSION 时，在“卡牌/扩展 ID”输入清单 ID。parameters 可以通过导入 JSON 设置，修改其他效果字段时会保留。

首次增加新 ID 的扩展可在卡牌校验时加载；替换已加载的同 ID 代码需要重启游戏。加载及执行错误写入 runtime.log，运行时 capabilities.extensions 返回已加载扩展和错误信息。

## 能做什么、不能保证什么

扩展是实际运行的本地 Java 代码，使用游戏进程的权限。这里只运行用户明确安装并启用的编译产物，没有独立代码沙箱。请先测试自己的代码；扩展执行异常会记录，但已经造成的游戏状态变化不能保证自动撤销。

这个 API 能操作游戏对象、调用动作、安装自定义能力；需要拦截尚未暴露的方法时，仍须修改适配层的 ASM 补丁并重启游戏。一次 onUse 调用不等于可以热替换 JVM 任意已经加载的方法。涉及渲染、存档格式、事件顺序或多个系统的想法，仍需具体实现和验证，不能承诺任何一句描述都能自动实现。

请勿移除仍被存档引用的扩展 JAR，否则卡牌定义将无法完整加载。保存源代码、清单和编译产物，以便随游戏版本重新构建。
