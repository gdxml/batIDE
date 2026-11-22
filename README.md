# batIDE — 批处理脚本编辑器 + 静态检查器

> Windows 批处理（.bat / .cmd）专用 IDE：123 条静态规则、增量检查（打字即检）、
> 插桩式交互调试、行尾当前目录推算、习惯学习补全。**绿色免安装**，解压即用。

**运行要求**：Windows 10 1903+ / Windows 11（系统自带 .NET Framework 4.8，无需另装运行时）

**许可证**：MIT（编辑器控件 [Scintilla.NET](https://github.com/desjar/Scintilla.NET) 随包分发）

---

## 功能总览

| 类别 | 内容 |
|---|---|
| **编辑器** | Scintilla.NET 5.x，语法高亮、括号配对、查找替换、Tab 缩进、拖放打开 |
| **检查** | 123 条规则（9 类定性），红波浪线 + 状态栏说明 + 悬停卡片（`ⓧ kind` + Prompt） |
| **补全** | 107 条命令、121 条片段、命令开关、变量（`%VAR%` / `!VAR!`，含脚本内 `set`）、路径（含变量展开） |
| **提示** | 命令 + 空格弹语法速查（56 条）；`!Var` / `%Var` 输入中途的变量提醒（唯一候选行内灰字，Tab 接受） |
| **快速修复** | `Ctrl+.` 弹修复菜单，11 类自动修复 |
| **交互调试** | `F2` 断点 → `F9` 运行 → `F10` 单步 / `F5` 继续 / `Shift+F5` 停止，变量面板实时快照 |
| **行尾目录注解** | 逐行推算 `cd /d` / `pushd` / `..` / 变量代入后的运行时目录，常驻显示 `--{D:\Test\}` |
| **习惯学习** | 只从「无诊断」的行学习路径 / 命令组合 / 命名前缀，补全排序越用越顺 |
| **CLI** | `batlint.exe` 可独立用于 CI / pre-commit（非零退出码即拦截），`--json` / `--rules` / `--fixes` / `--bench` / `--cwd` / `--habits` |

## 快捷键

| 键 | 作用 |
|---|---|
| `F5` | 运行脚本（调试中 = 继续到下一断点） |
| `F9` | 交互式调试 |
| `F10` | 单步（调试中） |
| `Shift+F5` | 停止调试 |
| `F2` | 切换断点（也可点行号右侧断点栏） |
| `Ctrl+.` | 快速修复当前问题 |
| `F8` / `Shift+F8` | 下一处 / 上一处问题 |
| `Ctrl+F` / `Ctrl+H` | 查找 / 查找替换 |
| `Ctrl+Space` | 强制补全列表 |
| 拖放 | 把 `.bat` 拖进窗口即打开 |

命令行启动：`batIDE.exe "D:\Test\build.bat"`（也支持拖到 exe 上、文件关联）。

## 检查器（123 条规则）

定性按「错在哪一层」划分，诊断只说「期望什么、实际找到什么」，不给相似度分：

| 类别 | 条数 | 代表规则 |
|---|---|---|
| syntaxError | 13 | `for` 缺 `do`；悬空 `if`（cmd 实测 exit 255）；全角 `！％`；`start` 首个引号参数是标题 |
| logicError | 22 | 块内 `%VAR%` 取不到新值；`!VAR!` 未开延迟展开；`if 两侧都是字面量`；`goto` 到下一行标签（空转） |
| typeError | 4 | `mkdir` 当建文件；`set /a` 除以 0 |
| nameError | 6 | `goto` 目标不存在（含拼写建议）；变量未定义；命令不存在 |
| flowError | 7 | 裸调用另一个 bat；`pushd/popd` 数量不一致；子过程结尾缺 `exit /b` |
| pathError | 10 | 跨盘 `cd` 缺 `/d`；`%~dp0` 未加引号 |
| ioError | 9 | `xcopy` 目录复制缺 `/i`（会停下提问）；循环块内 `>` 反复清空同一文件 |
| styleWarning | 47 | 首行缺 `@echo off`；Tab 与空格混用；变量赋值后从未引用 |
| dependencyWarning | 1 | 调用 powershell / python / curl（破坏零依赖前提） |

完整目录：`batlint --rules`（每条含触发样例与定性说明）。

### 正确性方法

- **所有可实测的行为都以 cmd.exe 的 exit code 为真值**，先验证再写规则：
  `if 1==1 ( …` → exit 255（真错误，报 R10）；`echo a )` → exit 0（静默忽略，**不报**）
- 悬空 `else`、`for` 缺 `do` 等结构性错误由**递归下降解析器**（Roslyn 式缺失 token 恢复）定位，
  而不是正则猜结构
- 拼写建议（Damerau–Levenshtein + 长度预算 + 唯一最优）只出现在建议通道，不参与语法判定

### 性能

| 规模 | 全量检查 | 增量（改动 3 行） |
|---|---|---|
| 301 行 | 9.9 ms | ~1.5 ms |
| 3004 行 | 107.9 ms | ~1.5 ms |
| 5002 行 | 172.4 ms | ~1.5 ms |

- Aho–Corasick 单遍扫描出「特征位掩码」，规则按掩码分桶，命中才跑正则
- 双通道：按键走行内增量，停止输入 700 ms 后跑全量补跨行规则
- 超过 60 KB 的文件全量检查进后台线程（latest-wins），打字永不卡顿
- 复现：`batlint --bench <文件>`；对照开关 `BATLINT_NOFAST=1`

## 交互式调试

cmd.exe 没有调试接口，采用「插桩 + stdin 驱动」：

```
1. F2 下断点
2. F9 生成插桩副本：每个可执行语句前插入 call :__batdbg <行号>
   探针打印 @__BDBG__ <行号>，再从 stdin 读控制字符
3. IDE 持有 stdin：断点发 d（变量快照）、F10 发 n、F5 发 c、Shift+F5 发 q
4. 同一 cmd 进程贯穿全程，setlocal / 延迟展开 / %ERRORLEVEL% 原样保持
```

已知限制（不隐藏）：括号块内不能单步；断点在块内 / 标签行 / 续行上不生效；
调试期间脚本自身的 `set /p` 会与调试器抢 stdin。

## 行尾「当前目录」注解

```
cd /d "D:\"          → 其后各行右侧 --{D:\}
cd /d "..\"          → 从 D:\Test\ 回到 --{D:\}
pushd / popd         → 入栈 / 出栈同步
set "T=D:\download\" → cd /d "!T!" 求得 --{D:\download\}（变量代入推算）
```

- 变量代入：脚本内 `set` 优先，未定义再查真实环境变量；`setlocal/endlocal` 成对还原
- 求不出来的行不显示（不会出现 `--{?}` 噪音）；注解常驻，不随光标移动消失
- 命令行预览：`batlint --cwd <文件>`

## 习惯学习

全量检查完成后，只从「无诊断」的行学习（避免把错写法沉淀成习惯）：

| 学什么 | 存放 | 上限 |
|---|---|---|
| 绝对路径（截到目录） | `%LOCALAPPDATA%\batIDE\habits.txt` | 500 条 / 100 KB |
| 命令 + 开关组合 | 同上 | 按使用次数 + 最近使用淘汰 |
| 变量命名前缀、标签 | 同上 | 只影响补全排序，不改变检查结果 |

## 从源码构建

```
git clone https://github.com/<你的用户名>/batIDE.git
cd batIDE
build.bat        → 产物在 release\，绿色免安装
```

需要 .NET SDK（`dotnet build`）与 NuGet 还原（联网拉一次 Scintilla.NET 5.3.2.9，之后可离线）。
也可用 Visual Studio 2022 打开 `src\batIDE.slnx`。

## 目录结构

```
├─ build.bat              # 一键构建 → release\
├─ src\
│  ├─ batIDE.slnx         # VS 解决方案
│  ├─ batIDE48\           # WinForms 编辑器（Scintilla.NET + net48/x86）
│  ├─ BatLint.Core\       # 规则引擎（编辑器与 CLI 共用）
│  └─ BatLint.Cli\        # batlint.exe 命令行检查器
└─ docs\                  # 截图
```

## CLI 用法

```
batlint <文件.bat> [--json] [--quiet]     检查单个脚本
batlint --rules                           列出全部规则与触发样例
batlint --fixes <文件>                    列出可用的自动修复（含精确偏移）
batlint --bench <文件>                     性能基准（20 次取平均）
batlint --cwd <文件>                       逐行打印运行时目录推算
batlint --habits <文件>                    只看习惯学习结果
```

退出码：`0` 无问题 / `1` 有问题 / `2` 用法错误 —— 可直接挂 CI 或 pre-commit。

## 已知限制

1. 「断点」是注入式快照，不是真正的断点 —— cmd.exe 没有调试接口
2. net48 + x86，不支持 ARM64
3. 灰字预测与行尾注解属于显示层，保存前强制清除，不会写进文件
4. 括号块内不能单步（见上文调试限制）
