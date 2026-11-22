using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BatLint.Core
{
    /// <summary> 用中文解释一行 bat 命令的作用 / Explains what a batch line does in plain Chinese. </summary>
    internal static class CommandDescriber
    {
        /// <summary> 逐行生成作用说明，跳过空行与注释 / Describe every executable line, skipping blanks and comments. </summary>
        public static Dictionary<int, string> DescribeAll(IReadOnlyList<string> lines)
        {
            var map = new Dictionary<int, string>();

            for (var i = 0; i < lines.Count; i++)
            {
                var text = Describe(lines[i]);
                if (text != null) map[i + 1] = text;
            }

            return map;
        }

        /// <summary> 生成「命令作用」汇总文本 / Build the command summary block. </summary>
        public static string BuildSummary(IReadOnlyList<string> lines)
        {
            var map = DescribeAll(lines);
            if (map.Count == 0) return string.Empty;

            var sb = new StringBuilder();
            sb.AppendLine("──────── 命令作用 ────────");
            foreach (var pair in map) sb.AppendLine($"{pair.Key,4}  {lines[pair.Key - 1].Trim()}  →  {pair.Value}");
            sb.AppendLine();
            return sb.ToString();
        }

        /// <summary> 解释单行命令，可传入变量展开函数 / Describe one line, optionally expanding %VAR%. </summary>
        public static string Describe(string rawLine, Func<string, string> expand = null)
        {
            if (string.IsNullOrWhiteSpace(rawLine)) return null;

            var line = rawLine.Trim();
            if (line.StartsWith("@")) line = line.Substring(1).TrimStart();
            if (line.Length == 0 || line.StartsWith("::")) return null;
            if (line.StartsWith("rem", StringComparison.OrdinalIgnoreCase) &&
                (line.Length == 3 || char.IsWhiteSpace(line[3]))) return null;

            if (line.StartsWith("(") || line.StartsWith(")")) return "括号块的分支或结束";

            var space = line.IndexOfAny(new[] { ' ', '\t', '/' });
            var name = (space < 0 ? line : line.Substring(0, space)).ToLowerInvariant();
            var rest = space < 0 ? string.Empty : line.Substring(space).Trim();

            // 路径类命令把 %VAR% 展开成实际值，让注解直接看出会创建/操作哪个路径
            var path = expand == null ? rest : expand(rest);

            switch (name)
            {
                case "echo": return DescribeEcho(rest);
                case "cd":
                case "chdir": return DescribeCd(path);
                case "set": return DescribeSet(rest);
                case "setlocal": return "开始局部环境变量作用域（脚本结束自动恢复）";
                case "endlocal": return "结束局部环境变量作用域";
                case "if": return "条件判断，条件成立才执行后面的命令";
                case "for": return "循环：对文件、目录、文本行或数字区间逐个执行命令";
                case "goto": return $"跳转到标签 {rest}";
                case "call":
                    return rest.StartsWith(":") ? $"调用本脚本内的子过程 {rest}" : $"调用另一个脚本 {rest}，执行完返回";
                case "start": return $"启动程序或打开文件：{path}";
                case "pause": return "暂停，等待用户按任意键继续";
                case "exit": return string.IsNullOrEmpty(rest) ? "退出脚本" : $"退出并返回退出码 {rest}";
                case "cls": return "清空控制台内容";
                case "title": return $"设置窗口标题为「{rest.Trim('"')}」";
                case "color": return $"设置控制台配色（{rest}：首位背景，次位前景）";
                case "chcp": return rest.StartsWith("65001") ? "切换为 UTF-8 代码页" : rest.StartsWith("936") ? "切换为 GBK 代码页" : $"切换代码页为 {rest}";
                case "dir": return string.IsNullOrEmpty(path) ? "列出当前目录的文件和子目录" : $"列出 {path} 的文件和子目录";
                case "type": return $"显示文件内容：{path}";
                case "copy": return $"复制文件：{path}";
                case "xcopy": return $"复制目录树：{path}";
                case "robocopy": return $"用 Robocopy 同步目录：{path}";
                case "move": return $"移动或重命名：{path}";
                case "ren":
                case "rename": return $"重命名：{path}";
                case "del":
                case "erase": return $"删除文件：{path}（带 /f 强删，/q 不确认）";
                case "md":
                case "mkdir": return $"创建目录：{path}";
                case "rd":
                case "rmdir": return $"删除目录：{path}（/s 递归，/q 不确认）";
                case "attrib": return $"查看或修改文件属性：{path}";
                case "pushd": return $"切换目录并压栈：{path}";
                case "popd": return "返回上一次 pushd 的目录";
                case "shift": return "批处理参数整体左移一位（%1 变成原来的 %2）";
                case "find": return $"在文件中查找文本：{path}";
                case "findstr": return $"用关键字或正则过滤文本：{path}";
                case "tasklist": return "列出当前运行的进程";
                case "taskkill": return $"结束进程：{path}";
                case "timeout": return $"等待指定秒数：{rest}";
                case "choice": return $"让用户按键选择：{rest}";
                case "shutdown": return $"关机或重启：{rest}";
                case "sc": return $"管理 Windows 服务：{path}";
                case "net": return $"网络与账户管理（用户 / 服务 / 共享）：{path}";
                case "reg": return $"读写注册表：{path}";
                case "where": return $"查找可执行文件的位置：{rest}";
                case "whoami": return "显示当前登录用户";
                case "ver": return "显示 Windows 版本";
                case "path": return string.IsNullOrEmpty(rest) ? "显示可执行文件搜索路径" : $"设置可执行文件搜索路径为 {rest}";
                case "assoc":
                case "ftype": return $"查看或设置文件关联：{rest}";
                default: return null;
            }
        }

        /// <summary> 解释 echo 的各种写法 / Explain the echo variants. </summary>
        private static string DescribeEcho(string rest)
        {
            if (rest.Length == 0) return "显示当前命令回显状态（on / off）";
            if (rest == ".") return "输出一个空行";
            if (rest.Equals("on", StringComparison.OrdinalIgnoreCase)) return "打开命令回显：之后每执行一条命令都会先打印命令本身";
            if (rest.Equals("off", StringComparison.OrdinalIgnoreCase)) return "关闭命令回显，只显示命令的输出";
            if (rest.StartsWith(">")) return "重定向：把内容写入文件";

            var text = rest.TrimStart();
            var idx = text.IndexOf('>');
            if (idx > 0)
            {
                var content = text.Substring(0, idx).Trim();
                var append = text.IndexOf(">>", System.StringComparison.Ordinal) >= 0;
                return $"打印「{content}」并{(append ? "追加" : "写入")}到文件 {text.Substring(idx).TrimStart('>', ' ')}";
            }

            return $"打印：{text}";
        }

        /// <summary> 解释 cd 的跨盘与普通写法 / Explain cd with and without /d. </summary>
        private static string DescribeCd(string rest)
        {
            if (rest.Length == 0) return "显示当前目录";
            if (rest.StartsWith("/d", StringComparison.OrdinalIgnoreCase))
            {
                var target = rest.Substring(2).Trim();
                return $"切换到驱动器与目录：{target}";
            }

            return $"切换当前目录：{rest}（只换目录不换盘，跨盘要加 /d）";
        }

        /// <summary> 解释 set 的赋值、计算、输入三种用法 / Explain set assignment, arithmetic and prompt. </summary>
        private static string DescribeSet(string rest)
        {
            if (rest.Length == 0) return "列出全部环境变量";
            if (rest.StartsWith("/a", StringComparison.OrdinalIgnoreCase)) return $"计算表达式并把结果赋给变量：{rest.Substring(2).Trim()}";
            if (rest.StartsWith("/p", StringComparison.OrdinalIgnoreCase)) return $"等待用户输入并写入变量：{rest.Substring(2).Trim()}";

            var body = rest.Trim('"');
            var eq = body.IndexOf('=');
            return eq > 0
                ? $"设置变量 {body.Substring(0, eq)} = 「{body.Substring(eq + 1)}」"
                : $"显示以「{body}」开头的变量";
        }
    }
}
