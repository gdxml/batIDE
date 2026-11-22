using System;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using BatLint.Core;
using ScintillaNET;

namespace batIDE.UI
{
    /// <summary> 主窗体逻辑：编辑器装配与脚本运行 / Main form logic: editor setup and script running. </summary>
    public partial class MainForm : Form
    {
        private readonly Scintilla _editor = new Scintilla();
        private readonly BreakpointManager _breakpoints;
        private readonly GhostText _ghost;
        private readonly LineNote _note;
        private readonly CwdNote _cwdNote;
        private readonly BraceMatcher _braces;
        private readonly IssueTip _issueTip;
        private readonly HashSet<string> _snippetCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly Timer _lintTimer = new Timer { Interval = 700 };   // 全量复查（打字停止后）
        private List<LintIssue> _issues = new List<LintIssue>();
        private string _lastLintText = string.Empty;      // 上一次检查时的文本，用于算改动行
        private double _lastQuickMs;                      // 快速通道耗时
        private int _lastQuickLines;                      // 快速通道覆盖行数
        private int _lastNoteLine = -2;
        private ScriptVars _scriptVars;
        private FindForm _find;
        private readonly HabitProfile _habits = new HabitProfile();   // 用户习惯（路径/命令组合/变量前缀）
        private string _path;

        // ── 交互式调试会话（插桩 + stdin 驱动）──
        private Process _dbgProc;
        private StreamWriter _dbgIn;
        private string _dbgTempFile;
        private bool _dbgPaused;
        private bool _dbgStepMode;
        private bool _dbgDumping;
        private int _tipLine = -1;

        /// <summary> 输入停顿后补一次变量提醒（IME 组合输入、粘贴都不会触发 CharAdded）/ Debounced variable hint. </summary>
        private readonly System.Windows.Forms.Timer _acTimer = new System.Windows.Forms.Timer();

        private int _dbgLine;
        private readonly List<string> _dbgDump = new List<string>();
        private HashSet<int> _dbgBreaks = new HashSet<int>();
        private HashSet<string> _dbgWatched = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary> 构造窗体并装配编辑器 / Build the form and attach the editor. </summary>
        public MainForm()
        {
            _breakpoints = new BreakpointManager(_editor);
            _ghost = new GhostText(_editor);
            _cwdNote = new CwdNote(_editor);
            _note = new LineNote(_cwdNote);        // 两条注解共用一条 EOL 通道，由 CwdNote 统一渲染
            _braces = new BraceMatcher(_editor);
            _issueTip = new IssueTip(_editor, () => _issues);
            InitializeComponent();
            SetupEditor();
            _ghost.Setup();
            _note.Setup();
            _cwdNote.Setup();
            _braces.Setup();
            _issueTip.Setup();

            _acTimer.Interval = 120;
            _acTimer.Tick += (s2, e2) => { _acTimer.Stop(); AutoHintVariables(); };
            _habits.Load();               // 载入历史习惯（最多 500 条 / 100 KB）
            RefreshTitle();
            AcceptDrop();

            // 自测：BATIDE_DBGTEST=<脚本> 打开文件、在第 4 行下断点、跑一次交互调试，
            // 2.5 秒后自动继续，并把暂停变量与最终输出写日志（验证调试通道）
            var dbgTest = Environment.GetEnvironmentVariable("BATIDE_DBGTEST");
            if (!string.IsNullOrEmpty(dbgTest) && File.Exists(dbgTest))
            {
                Shown += (s2, e2) =>
                {
                    LoadFile(dbgTest);
                    _breakpoints.Toggle(8);                     // 第 8 行 echo RESULT（A/B 已赋值）
                    _dbgLog = Path.Combine(Path.GetTempPath(), "batide-dbgtest.log");
                    File.WriteAllText(_dbgLog, "start" + Environment.NewLine);

                    OnDebugRun(this, EventArgs.Empty);

                    var timer = new Timer { Interval = 2500 };
                    timer.Tick += (s3, e3) =>
                    {
                        timer.Stop();
                        LogDebug($"pausedAtLine={_dbgLine} outputLen={outputBox.TextLength}");
                        LogDebug("---- output while paused ----");
                        LogDebug(outputBox.Text);
                        DebugContinue();

                        var timer2 = new Timer { Interval = 2500 };
                        timer2.Tick += (s4, e4) =>
                        {
                            timer2.Stop();
                            LogDebug("---- output after continue ----");
                            LogDebug(outputBox.Text);
                            Close();
                        };
                        timer2.Start();
                    };
                    timer.Start();
                };
            }
        }

        private string _dbgLog;

        private void LogDebug(string text)
        {
            try { File.AppendAllText(_dbgLog, text + Environment.NewLine); } catch { }
        }

        /// <summary> 初始化 Scintilla：bat 词法、行号、字体 / Initialize Scintilla for batch files. </summary>
        private void SetupEditor()
        {
            _editor.Dock = DockStyle.Fill;
            _editor.BorderStyle = ScintillaNET.BorderStyle.None;
            _editor.WrapMode = WrapMode.None;
            _editor.LexerName = "batch";

            _editor.Margins[0].Type = MarginType.Number;
            _editor.Margins[0].Width = 52;
            _breakpoints.Setup();

            _editor.StyleResetDefault();
            _editor.Styles[Style.Default].Font = "Consolas";
            _editor.Styles[Style.Default].Size = 11;
            _editor.StyleClearAll();

            // LexBatch 样式表：0 默认 / 1 注释 / 2 关键字 / 3 标签 / 4 @ / 5 命令 / 6 标识符 / 7 运算符 / 8 标签后注释
            _editor.Styles[Style.LineNumber].Font = "Consolas";
            _editor.Styles[Style.LineNumber].Size = 9;
            SyntaxColors.Apply(_editor);

            _editor.TextChanged += (s, e) =>
            {
                _scriptVars = null;
                _braces.Reset();
                UpdateStatus();
                ScheduleLint();
                UpdateLineNote(true);
                _acTimer.Stop();
                _acTimer.Start();
            };
            _editor.UpdateUI += (s, e) => { GuardGhost(); GuardNote(); UpdateLineNote(false); RefreshCallTip(); _braces.Update(); UpdateStatus(); };
            _editor.SavePointLeft += (s, e) => RefreshTitle();
            _editor.SavePointReached += (s, e) => RefreshTitle();
            _editor.KeyDown += OnEditorKeyDown;

            // WinForms 默认把 Tab 当「切换焦点」吃掉，编辑器拿不到：
            // 这里声明 Tab 属于输入键，Tab 才能用来缩进 / 接受补全候选
            _editor.PreviewKeyDown += (s2, e2) =>
            {
                if (e2.KeyCode == Keys.Tab) e2.IsInputKey = true;
            };

            // Ctrl+. 也可能被 Scintilla 直接吃掉，这里再兜一层
            _editor.KeyDown += (s2, e2) =>
            {
                if (!e2.Control) return;
                if (e2.KeyCode != Keys.OemPeriod && e2.KeyCode != Keys.Decimal) return;
                e2.SuppressKeyPress = true;
                ShowQuickFixes();
            };
            _editor.Indicators[0].Style = IndicatorStyle.Squiggle;
            _editor.Indicators[0].ForeColor = Color.FromArgb(0xD1, 0x34, 0x38);
            _lintTimer.Tick += (s, e) => { _lintTimer.Stop(); RunLint(); };
            _editor.AutoCAutoHide = true;
            _editor.AutoCMaxHeight = 12;
            _editor.AutoCSeparator = '\n';
            _editor.AutoCIgnoreCase = true;          // 否则 C:\u 匹配不到 C:\Users\
            _editor.CharAdded += OnCharAdded;
            _editor.AutoCSelection += OnSnippetAccepted;
            lblPlaceholder.Visible = false;
            editorHost.Controls.Add(_editor);
            outputBox.MouseDoubleClick += OnOutputDoubleClick;
            outputBox.Cursor = Cursors.Hand;
            UpdateStatus();
        }

        /// <summary> 刷新状态栏：行列、规模与当前行问题 / Refresh status bar with caret, size and current-line issue. </summary>
        private void UpdateStatus()
        {
            var pos = _editor.CurrentPosition;
            var line = _editor.LineFromPosition(pos);
            var col = _editor.GetColumn(pos);
            lblStatus.Text = $"{line + 1}:{col + 1} | {_editor.Lines.Count} 行 | {_editor.TextLength} 字符 | GBK";

            var current = _issues.FirstOrDefault(x => x.Line == line + 1);
            lblHint.Text = current != null
                ? $"⚠ 第 {current.Line} 行：{current.Message}"
                : _issues.Count > 0 ? $"⚠ {_issues.Count} 处语法问题" : string.Empty;
            lblHint.ForeColor = _issues.Count > 0 ? Color.FromArgb(0xC0, 0x39, 0x2B) : Color.Gray;
        }

        /// <summary> 延迟触发语法检查 / Schedule a lint pass after typing settles. </summary>
        private void ScheduleLint()
        {
            // 快速通道：只重查改动行，立刻出波浪线
            QuickLint();

            // 慢速通道：停止输入 700ms 后做全量（跨行规则、标签表、解析器）
            _lintTimer.Stop();
            _lintTimer.Start();
        }

        /// <summary>
        /// 增量检查：只对“改动行”跑行内规则，其它行沿用上次结果；
        /// 行数变化（粘贴/删除多行）时退回全量，避免行号漂移。
        /// </summary>
        private void QuickLint()
        {
            var text = _editor.Text;
            var length = text.Length;

            if (_lastLintText.Length == 0 || text == _lastLintText)
            {
                _lastLintText = text;
                return;
            }

            // 求公共前缀 / 后缀，定位改动区间
            var maxPrefix = Math.Min(_lastLintText.Length, length);
            var prefix = 0;
            while (prefix < maxPrefix && _lastLintText[prefix] == text[prefix]) prefix++;

            var maxSuffix = Math.Min(_lastLintText.Length, length) - prefix;
            var suffix = 0;
            while (suffix < maxSuffix &&
                   _lastLintText[_lastLintText.Length - 1 - suffix] == text[length - 1 - suffix]) suffix++;

            var changedEnd = Math.Max(prefix, length - suffix);

            var oldLineCount = _lastLintText.Split('\n').Length;
            var newLineCount = text.Split('\n').Length;

            _lastLintText = text;

            if (oldLineCount != newLineCount)
            {
                RunLint();                                  // 行数变了：直接全量
                return;
            }

            var fromLine = _editor.LineFromPosition(Math.Min(prefix, length));
            var toLine = _editor.LineFromPosition(Math.Min(changedEnd, length));

            fromLine = Math.Max(0, fromLine - 1);
            toLine = Math.Min(_editor.Lines.Count - 1, toLine + 1);

            var sw = Stopwatch.StartNew();
            var quick = BatLinter.CheckLines(text, fromLine, toLine);
            sw.Stop();

            _lastQuickMs = sw.Elapsed.TotalMilliseconds;
            _lastQuickLines = toLine - fromLine + 1;

            // 保留区间外的问题，替换区间内的
            var merged = _issues.Where(i => i.Line - 1 < fromLine || i.Line - 1 > toLine).ToList();
            merged.AddRange(quick);
            _issues = merged.OrderBy(i => i.Start).ToList();

            // 只重画受影响的行
            var startOffset = _editor.Lines[fromLine].Position;
            var endOffset = _editor.Lines[toLine].Position + _editor.Lines[toLine].Length;
            _editor.IndicatorCurrent = 0;
            _editor.IndicatorClearRange(startOffset, Math.Max(1, endOffset - startOffset));

            foreach (var issue in _issues.Where(i => i.Start >= startOffset && i.Start <= endOffset))
                _editor.IndicatorFillRange(issue.Start, Math.Max(1, issue.Length));

            UpdateStatus();
        }

        /// <summary> 执行语法检查并绘制波浪线（大文件放到后台线程，UI 不阻塞）/ Run checks off the UI thread. </summary>
        private void RunLint()
        {
            var text = _editor.Text;
            var version = ++_lintVersion;

            // 小文件：直接同步检查，省线程切换
            if (text.Length <= 60000)
            {
                ApplyLintResult(text, version, BatLinter.Check(text), 0);
                return;
            }

            // 大文件：后台跑，结果回来时若版本已过期就丢弃（latest-wins）
            var sw = Stopwatch.StartNew();
            Task.Run(() =>
            {
                var result = text.Length > 400000 ? new List<LintIssue>() : BatLinter.Check(text);
                return (Issues: result, Elapsed: sw.Elapsed.TotalMilliseconds);
            }).ContinueWith(t =>
            {
                if (t.Status != TaskStatus.RanToCompletion) return;
                ApplyLintResult(text, version, t.Result.Issues, t.Result.Elapsed);
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        private int _lintVersion;

        /// <summary> 把检查结果画到编辑器（必须在 UI 线程）/ Apply results on the UI thread. </summary>
        private void ApplyLintResult(string text, int version, List<LintIssue> issues, double elapsedMs)
        {
            if (version != _lintVersion) return;                 // 有更新的检查在跑，丢弃旧结果

            var length = _editor.TextLength;
            _editor.IndicatorCurrent = 0;
            _editor.IndicatorClearRange(0, length);

            _issues = issues;

            // 只学「没有诊断」的行，避免把错写法记成习惯
            _habits.Learn(text, _issues);

            _lastLintText = text;
            _lastQuickMs = elapsedMs > 0 ? elapsedMs : _lastQuickMs;
            _lastQuickLines = _editor.Lines.Count;

            foreach (var issue in _issues.Where(x => x.Start >= 0 && x.Start + x.Length <= length))
                _editor.IndicatorFillRange(issue.Start, issue.Length);

            RefreshCwdNotes(text);

            _issueTip.Invalidate();
            UpdateStatus();
        }

        /// <summary> 键入字符时统一调度补全；无候选时给出灰字预测 / Dispatch completion, fall back to ghost prediction. </summary>
        private void OnCharAdded(object sender, CharAddedEventArgs e)
        {
            var ch = (char)e.Char;

            // 空格：弹命令语法提示（if/for/set ...），但不弹补全下拉框
            if (ch == ' ')
            {
                SkipCompletionOnce();
                ShowCallTip();
                return;
            }

            var trigger = ch == '=' || char.IsLetter(ch) || ch == '%' || ch == '!' || ch == '％' || ch == '！'
                          || ch == '\\' || ch == ':' || ch == '"' || ch == '/' || ch == '.';
            if (!trigger) return;

            if (!OfferCompletion()) PredictGhost();
        }

        /// <summary> 空格等分隔符后一次性抑制补全 / One-shot suppression after a separator key. </summary>
        private void SkipCompletionOnce()
        {
            _editor.AutoCCancel();
            _ghost.Clear();
        }

        /// <summary> 按上下文依次尝试变量、路径、参数、命令补全 / Try variable, path, argument then command completion. </summary>
        private bool OfferCompletion()
        {
            var pos = _editor.CurrentPosition;
            var line = _editor.Lines[_editor.LineFromPosition(pos)];
            var before = line.Text.Substring(0, Math.Max(0, pos - line.Position));

            if (TryOfferVariable(before)) return true;
            if (TryOfferDelayVariable(before)) return true;
            if (TryOfferPath(before)) return true;
            if (TryOfferArgs(before)) return true;
            return OfferCommands();
        }

        /// <summary> 推断并显示灰字 / Predict and display the ghost text. </summary>
        private void PredictGhost()
        {
            var pos = _editor.CurrentPosition;
            var line = _editor.Lines[_editor.LineFromPosition(pos)];
            var before = line.Text.Substring(0, Math.Max(0, pos - line.Position));

            var ghost = GhostRules.Predict(before);
            if (!string.IsNullOrEmpty(ghost)) _ghost.Show(ghost);
        }

        /// <summary> 光标离开灰字位置时清除，否则重染样式 / Drop the ghost when the caret moves, otherwise repaint it. </summary>
        private void GuardGhost()
        {
            if (!_ghost.HasGhost) return;

            if (_editor.CurrentPosition != _ghost.Start) { _ghost.Clear(); return; }
            _ghost.Repaint();
        }

        /// <summary> 实时在光标行末显示「这行做什么」，行未变则跳过 / Describe the caret line, skipping unchanged lines. </summary>
        private void UpdateLineNote(bool force)
        {
            var line = _editor.LineFromPosition(_editor.CurrentPosition);
            if (!force && line == _lastNoteLine) return;

            _lastNoteLine = line;
            _note.Show(line, CommandDescriber.Describe(_editor.Lines[line].Text, Vars().Expand));
        }

        /// <summary> 光标离开注解行或进入注解区时清除 / Drop the note when the caret leaves the line or enters the note. </summary>
        private void GuardNote()
        {
            if (!_note.HasNote) return;

            if (_editor.LineFromPosition(_editor.CurrentPosition) != _note.Line)
            {
                _note.Clear();
                _lastNoteLine = -2;
                return;
            }

            _note.Guard();
            _note.Repaint();
        }

        /// <summary> 任何按键先撤销灰字；注解只在插入模式下需要撤掉 / Drop the ghost before typing; the note only in fallback mode. </summary>
        private void OnEditorKeyDown(object sender, KeyEventArgs e)
        {
            if (_ghost.HasGhost) _ghost.Clear();
            if (_note.InsertMode) _note.Clear();
        }

        /// <summary> 取脚本变量（按文本变化惰性重建）/ Script variables, rebuilt lazily after edits. </summary>
        private ScriptVars Vars() => _scriptVars ?? (_scriptVars = ScriptVars.Parse(_editor.Text));

        /// <summary> 统一弹出：自过滤到「每一项都以 prefix 开头」，否则 AutoCAutoHide 会关掉整表 / Filter every candidate by prefix before showing. </summary>
        /// <returns> 是否真的弹出了列表 / Whether a list was shown. </returns>
        private bool ShowAutoC(int typedLength, IEnumerable<string> candidates, string prefix)
        {
            var list = candidates
                .Where(c => c.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (list.Count == 0) return false;
            if (list.Count == 1 && string.Equals(list[0], prefix, StringComparison.OrdinalIgnoreCase)) return false;

            _editor.AutoCShow(typedLength, Join(list));
            return true;
        }

        /// <summary>
        /// 光标停在已输入一半的 !VAR / %VAR 里时给出候选。
        /// 走 TextChanged 而不是 CharAdded：中文输入法的组合输入、粘贴都不会触发 CharAdded。
        /// </summary>
        private void AutoHintVariables()
        {
            if (_editor.IsDisposed || _editor.AutoCActive) return;

            var pos = _editor.CurrentPosition;
            if (pos <= 0) return;

            var line = _editor.Lines[_editor.LineFromPosition(pos)];
            var before = line.Text.Substring(0, Math.Max(0, pos - line.Position));

            if (before.Length == 0) return;

            // 只在「刚打到变量名中间」时提醒，且不跨空白/引号
            var lastBang = Math.Max(before.LastIndexOf('!'), before.LastIndexOf('！'));
            var lastPercent = Math.Max(before.LastIndexOf('%'), before.LastIndexOf('％'));
            var lastQuote = before.LastIndexOf('"');
            var lastSpace = before.LastIndexOf(' ');

            var open = Math.Max(lastBang, lastPercent);
            if (open < 0 || open < lastQuote || open < lastSpace) return;

            var terminator = (before[open] == '!' || before[open] == '！') ? '!' : '%';
            var typed = before.Substring(open + 1);
            if (typed.IndexOf(terminator) >= 0) return;        // 已经闭合，不再打扰
            if (typed.Length == 0) return;
            if (typed.Any(c => !char.IsLetterOrDigit(c) && c != '_')) return;

            // 只有一个候选时用行内灰字（不会抢焦点，Tab 直接接受）；
            // 多个候选才弹下拉列表
            var names = Vars().Names
                .Concat(CompletionSource.Variables())
                .Where(v => v.StartsWith(typed, StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (names.Count == 1 && names[0].Length > typed.Length)
            {
                _ghost.Show(names[0].Substring(typed.Length) + terminator);
                return;
            }

            if (terminator == '!') TryOfferDelayVariable(before);
            else TryOfferVariable(before);
        }

        /// <summary> % 之后的变量补全：脚本变量在前，环境变量在后 / Complete %NAME%: script vars first. </summary>
        private bool TryOfferVariable(string before)
        {
            // 中文输入法常把 % 打成全角 ％，这里同等对待
            before = before.Replace('％', '%');

            if (before.Count(c => c == '%') % 2 == 0) return false;   // % 已配对，光标不在变量内

            var open = before.LastIndexOf('%');
            var typed = before.Substring(open + 1);
            if (typed.IndexOf(' ') >= 0) return false;

            if (typed.StartsWith("~"))
                return ShowAutoC(typed.Length + 1, CompletionSource.Macros, "%" + typed);

            var all = Vars().Names
                .Concat(CompletionSource.Variables())
                .Select(v => $"%{v}%");

            return ShowAutoC(typed.Length + 1, all, "%" + typed);
        }

        /// <summary> ! 之后的延迟展开变量补全 / Complete !NAME! variable names after a bang. </summary>
        private bool TryOfferDelayVariable(string before)
        {
            // 中文输入法常把 ! 打成全角 ！，这里同等对待
            before = before.Replace('！', '!');

            if (before.Count(c => c == '!') % 2 == 0) return false;

            var open = before.LastIndexOf('!');
            var typed = before.Substring(open + 1);
            if (typed.IndexOf(' ') >= 0) return false;

            var all = Vars().Names
                .Concat(CompletionSource.Variables())
                .Select(v => $"!{v}!");

            return ShowAutoC(typed.Length + 1, all, "!" + typed);
        }

        /// <summary> 路径 token 的边界：空白、引号、赋值、重定向、括号、逗号 / Characters that bound a path token. </summary>
        private static readonly char[] PathSeparators = { ' ', '\t', '"', '=', '>', '<', '(', ',', ';' };

        /// <summary> 路径与文件名补全：绝对路径、%VAR% 展开、重定向目标、脚本目录内的相对文件名 / Complete absolute paths, expanded variables, redirect targets and plain file names. </summary>
        private bool TryOfferPath(string before)
        {
            var cut = before.LastIndexOfAny(PathSeparators) + 1;
            var token = before.Substring(cut);
            if (token.Length < 2) return false;

            var expanded = Vars().Expand(token);
            var isDrive = expanded.Length == 2 && expanded[1] == ':';
            var hasSeparator = expanded.IndexOf('\\') >= 0 || expanded.IndexOf('/') >= 0;
            var afterRedirect = cut > 0 && (before[cut - 1] == '>' || before[cut - 1] == '<');
            var looksLikeFile = expanded.IndexOf('.') > 0;

            if (!isDrive && !hasSeparator && !looksLikeFile && !afterRedirect) return false;

            if (token.Length >= 2)
            {
                var habitPaths = _habits.Suggest(token, HabitProfile.Kind.Path, 6).ToList();
                if (habitPaths.Count > 0)
                {
                    var typed2 = _editor.CurrentPosition - _editor.WordStartPosition(_editor.CurrentPosition, true);
                    _editor.AutoCShow(typed2, Join(habitPaths));
                    return true;
                }
            }

            // 没有分隔符、或写成 .\ ..\ 时，都按脚本所在目录解析；否则 test.txt、.\test.txt 一律补不出来
            var relative = !isDrive && (!hasSeparator || expanded.StartsWith(".", StringComparison.Ordinal));
            var query = relative ? Path.Combine(ScriptDirectory(), expanded) : expanded;
            var cands = isDrive ? CompletionSource.Drives() : CompletionSource.Paths(query);

            // 相对名只留叶子，否则候选是完整路径、不以已输入内容开头，列表会被 Scintilla 关掉；
            // 用户写成 .\ 或 ..\ 时要把这个前导补回去
            var lead = expanded.StartsWith("..\\", StringComparison.Ordinal) ? "..\\"
                     : expanded.StartsWith(".\\", StringComparison.Ordinal) ? ".\\"
                     : string.Empty;

            return ShowAutoC(token.Length, relative ? cands.Select(c => lead + LeafOf(c)) : cands.Select(Vars().Collapse), token);
        }

        /// <summary> 取最后一段名字，目录补回尾反斜杠 / Keep the leaf name and restore the trailing separator. </summary>
        private static string LeafOf(string path) =>
            path.EndsWith("\\") ? Path.GetFileName(path.TrimEnd('\\')) + "\\" : Path.GetFileName(path);

        /// <summary> 相对补全的基准目录：脚本所在目录 / Base directory for relative completion: the script folder. </summary>
        private string ScriptDirectory()
        {
            var dir = string.IsNullOrEmpty(_path) ? null : Path.GetDirectoryName(_path);
            return string.IsNullOrEmpty(dir) ? Environment.CurrentDirectory : dir;
        }

        /// <summary> 命令参数与子命令补全：net use、reg add、xcopy /e /Complete command arguments. </summary>
        private bool TryOfferArgs(string before)
        {
            var trimmed = before.TrimStart();
            if (trimmed.StartsWith("REM", StringComparison.OrdinalIgnoreCase) || trimmed.StartsWith("::")) return false;

            var cut = before.LastIndexOfAny(new[] { ' ', '\t' }) + 1;
            var token = before.Substring(cut);
            if (token.IndexOf('"') >= 0) return false;

            var cmd = CommandOf(before);
            if (cmd.Length < 2) return false;

            return ShowAutoC(token.Length, BatVocabulary.ArgsFor(cmd, token), token);
        }

        /// <summary> 取行首命令名 / Extract the leading command name of a line. </summary>
        private static string CommandOf(string before)
        {
            var t = before.TrimStart();
            if (t.StartsWith("@")) t = t.Substring(1).TrimStart();

            var end = 0;
            while (end < t.Length && (char.IsLetter(t[end]) || t[end] == '_')) end++;
            return t.Substring(0, end).ToLowerInvariant();
        }

        /// <summary> 命令、关键字与常用片段补全；片段用前缀、命令用模糊打分 / Prefix for snippets, fuzzy scoring for commands. </summary>
        private bool OfferCommands(bool force = false)
        {
            var pos = _editor.CurrentPosition;
            var len = pos - _editor.WordStartPosition(pos, true);
            if (len < 1 && !force) return false;

            var typed = len < 1 ? string.Empty : _editor.GetTextRange(pos - len, len) ?? string.Empty;

            if (!force)
            {
                var lineStart = _editor.Lines[_editor.LineFromPosition(pos)].Position;
                var before = _editor.GetTextRange(lineStart, pos - lineStart) ?? string.Empty;
                var head = before.Substring(0, Math.Max(0, before.Length - len)).TrimEnd();
                if (head.Length > 0 && !IsCommandSlot(head)) return false;
            }

            var snippets = BatSnippets.Matches(typed).Select(s => s.Code).ToList();

            // 有前缀命中就只留前缀（Scintilla 只认前缀，子序列项留在列表里会被它隐藏甚至关表）；
            // 一个前缀都没命中时才退回子序列兜底，此时列表能显示但仍会在继续输入后被过滤
            var prefixed = BatCommands.All()
                .Where(c => c.StartsWith(typed, StringComparison.OrdinalIgnoreCase))
                .ToList();
            var pool = typed.Length == 0 || prefixed.Count > 0 ? prefixed : BatCommands.All();
            var limit = typed.Length == 0 ? int.MaxValue : 60;

            // 用户常写的「命令 + 开关」组合优先（来自习惯学习）
            var habitCommands = _habits
                .Suggest(typed, HabitProfile.Kind.Command, 8)
                .ToList();

            var cands = habitCommands
                .Concat(snippets)
                .Concat(FuzzyMatcher.Rank(pool, typed, limit))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (cands.Count == 0) return false;

            _snippetCodes.Clear();
            snippets.ForEach(c => _snippetCodes.Add(c));

            _editor.AutoCShow(len, Join(cands));
            return true;
        }

        /// <summary> 判断光标是否处于命令位（行首、&、|、(、do 之后）/ Whether the caret sits in a command slot. </summary>
        private static bool IsCommandSlot(string head) =>
            head.EndsWith("&") || head.EndsWith("|") || head.EndsWith("(")
            || head.EndsWith(" do", StringComparison.OrdinalIgnoreCase);

        /// <summary> 片段被接受后选中第一个引号内的占位符，便于直接改写 / Select the first quoted placeholder after accepting a snippet. </summary>
        private void OnSnippetAccepted(object sender, AutoCSelectionEventArgs e)
        {
            var code = e.Text;
            if (string.IsNullOrEmpty(code) || !_snippetCodes.Contains(code)) return;

            var q1 = code.IndexOf('"');
            var q2 = q1 < 0 ? -1 : code.IndexOf('"', q1 + 1);
            if (q2 < 0) return;

            // 该通知在文本插入之前触发，延迟到消息循环后再定位选区
            BeginInvoke((Action)(() =>
            {
                var start = _editor.CurrentPosition - code.Length;
                if (start < 0) return;
                _editor.SetSel(start + q1 + 1, start + q2);
            }));
        }

        /// <summary> 用当前分隔符拼接候选 / Join candidates with the active separator. </summary>
        private string Join(IEnumerable<string> items) => string.Join(_editor.AutoCSeparator.ToString(), items);

        /// <summary> 显示命令语法提示 / Show the syntax call tip for the current line. </summary>
        private void ShowCallTip()
        {
            var pos = _editor.CurrentPosition;
            var index = _editor.LineFromPosition(pos);
            var tip = BatSyntaxTips.For(_editor.Lines[index].Text);
            if (tip == null) { _editor.CallTipCancel(); return; }

            // 贴在行首下方：行很长时贴在光标处会被窗口右边缘裁掉
            _editor.CallTipShow(_editor.Lines[index].Position, FormatTip(tip));
            _tipLine = index;
        }

        /// <summary> 把一行长提示折成多行，避免窗口右边缘把后半截裁掉 / Wrap a long tip into several calltip lines. </summary>
        private static string FormatTip(string tip)
        {
            if (string.IsNullOrEmpty(tip)) return tip;

            // 提示里用「空格 竖线 空格」分隔多个语法变体，正好可以一行一个
            var parts = System.Text.RegularExpressions.Regex.Split(tip, @"\s{2,}\|\s{2,}");
            var lines = new List<string>();

            foreach (var part in parts)
            {
                var text = part.Trim();
                if (text.Length == 0) continue;
                if (text.Length <= 64) { lines.Add(text); continue; }

                // 仍然太长就按空格硬折
                var current = string.Empty;
                foreach (var word in text.Split(' '))
                {
                    if (current.Length == 0) { current = word; continue; }
                    if (current.Length + 1 + word.Length > 64) { lines.Add(current); current = word; }
                    else current += " " + word;
                }

                if (current.Length > 0) lines.Add(current);
            }

            return lines.Count == 0 ? tip : string.Join("\n", lines);
        }

        /// <summary> 光标还在同一行、且该行仍匹配同一命令时，把语法提示重新亮出来 / Keep the syntax tip alive. </summary>
        private void RefreshCallTip()
        {
            if (_tipLine < 0) return;

            var line = _editor.LineFromPosition(_editor.CurrentPosition);
            if (line != _tipLine) { _editor.CallTipCancel(); _tipLine = -1; return; }

            var text = _editor.Lines[line].Text;
            if (BatSyntaxTips.For(text) == null) { _editor.CallTipCancel(); _tipLine = -1; return; }

            if (!_editor.CallTipActive)
                _editor.CallTipShow(_editor.Lines[line].Position, FormatTip(BatSyntaxTips.For(text)));
        }

        /// <summary> 打开 bat 文件 / Open a batch file. </summary>
        private void OnOpen(object sender, EventArgs e)
        {
            using (var dlg = new OpenFileDialog { Filter = "批处理文件|*.bat;*.cmd|所有文件|*.*" })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                if (!ConfirmDiscard()) return;
                LoadFile(dlg.FileName);
            }
        }

        /// <summary> 启动时按命令行参数打开文件 / Open a file passed on the command line. </summary>
        public void OpenPathOnStartup(string path)
        {
            if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path)) return;

            LoadFile(path);
        }

        /// <summary> 载入文件到编辑器，失败时保持原状 / Load a file into the editor without breaking current state. </summary>
        private void LoadFile(string path)
        {
            string text;
            try
            {
                text = File.ReadAllText(path, Encoding.GetEncoding(936));
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "打开失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _path = path;
            _ghost.Clear();
            if (_note.InsertMode) _note.Clear();
            _editor.Text = text;
            _editor.EmptyUndoBuffer();
            _breakpoints.Clear();
            _editor.SetSavePoint();
            RefreshTitle();
            UpdateStatus();
        }

        /// <summary> 丢弃或保存未提交的修改；取消返回 false / Ask about unsaved work; false means the caller must abort. </summary>
        private bool ConfirmDiscard()
        {
            if (!_editor.Modified) return true;

            var choice = MessageBox.Show(this, "当前脚本有未保存的修改，是否保存？", "batIDE",
                MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning);

            if (choice == DialogResult.Cancel) return false;
            return choice == DialogResult.No || SaveCurrent();
        }

        /// <summary> 支持把 bat 文件直接拖进窗口 / Accept a batch file dropped onto the window. </summary>
        private void AcceptDrop()
        {
            AllowDrop = true;

            DragEnter += (s, e) =>
                e.Effect = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;

            DragDrop += (s, e) =>
            {
                if (!(e.Data.GetData(DataFormats.FileDrop) is string[] files) || files.Length == 0) return;
                if (!ConfirmDiscard()) return;
                LoadFile(files[0]);
            };
        }

        /// <summary> 保存到当前路径；没有路径时先走另存为 / Save to the current path, falling back to Save As. </summary>
        private bool SaveCurrent()
        {
            if (string.IsNullOrEmpty(_path)) OnSaveAs(this, EventArgs.Empty);
            return !string.IsNullOrEmpty(_path) && !_editor.Modified;
        }

        /// <summary> 保存到当前路径 / Save to the current path. </summary>
        private void OnSave(object sender, EventArgs e) => SaveCurrent();

        /// <summary> 刷新标题栏：文件名 + 未保存标记 / Refresh the title bar with an unsaved marker. </summary>
        private void RefreshTitle()
        {
            var name = string.IsNullOrEmpty(_path) ? "未命名.bat" : Path.GetFileName(_path);
            Text = $"batIDE - {name}{(_editor.Modified ? " *" : string.Empty)}";
        }

        /// <summary> 关闭前确认未保存的修改，避免静默丢内容 / Confirm unsaved changes before closing. </summary>
        protected override void OnFormClosing(FormClosingEventArgs e) => e.Cancel = !ConfirmDiscard();

        /// <summary> 另存为 / Save as a new file. </summary>
        private void OnSaveAs(object sender, EventArgs e)
        {
            using (var dlg = new SaveFileDialog { Filter = "批处理文件|*.bat", DefaultExt = "bat" })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                _path = dlg.FileName;
                WriteFile(_path);
            }
        }

        /// <summary> 以 GBK 写盘，失败时提示并返回 false / Write with GBK, report failure instead of crashing. </summary>
        private bool WriteFile(string path)
        {
            try
            {
                _ghost.Clear();                                  // 灰字不能写进文件
                if (_note.InsertMode) _note.Clear();              // 退回流式注解同样不能写进文件
                File.WriteAllText(path, _editor.Text, Encoding.GetEncoding(936));
                _editor.SetSavePoint();
                RefreshTitle();
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "保存失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
        }

        /// <summary> 打开查找窗口 / Open the find window. </summary>
        private void OnFind(object sender, EventArgs e) => ShowFind(false);

        /// <summary> 打开查找替换窗口 / Open the find and replace window. </summary>
        private void OnReplace(object sender, EventArgs e) => ShowFind(true);

        /// <summary> 显示或复用查找窗口，焦点按模式落到查找或替换框 / Show or reuse the window and focus the right box. </summary>
        private void ShowFind(bool replaceMode)
        {
            if (_find == null || _find.IsDisposed) _find = new FindForm(_editor);

            if (!_find.Visible) _find.Show(this);
            _find.BringToFront();
            _find.FocusFind(replaceMode);
        }

        /// <summary> 双击输出里带行号的轨迹，跳到对应源码行 / Double-click a traced line in the output to jump to that source line. </summary>
        private void OnOutputDoubleClick(object sender, MouseEventArgs e)
        {
            var index = outputBox.GetCharIndexFromPosition(e.Location);
            var lineIndex = outputBox.GetLineFromCharIndex(index);
            if (lineIndex < 0 || lineIndex >= outputBox.Lines.Length) return;

            var match = Regex.Match(outputBox.Lines[lineIndex], @"第\s*(\d+)\s*行");
            if (!match.Success) return;
            if (!int.TryParse(match.Groups[1].Value, out var line)) return;
            if (line < 1 || line > _editor.Lines.Count) return;

            _ghost.Clear();
            if (_note.InsertMode) _note.Clear();

            var target = _editor.Lines[line - 1];
            _editor.SetSel(target.Position, target.Position + target.Length);
            _editor.ScrollCaret();
            _editor.Focus();
        }

        /// <summary> 退出 / Exit the application. </summary>
        private void OnExit(object sender, EventArgs e) => Close();

        /// <summary> 运行当前脚本并回显输出 / Run the current script and echo its output. </summary>
        private void OnRun(object sender, EventArgs e)
        {
            if (!PrepareAndSave(sender, e)) return;

            outputBox.Text = string.Empty;
            lblStatus.Text = "运行中...";
            Application.DoEvents();

            var source = File.ReadAllLines(_path, Encoding.GetEncoding(936));
            var summary = CommandDescriber.BuildSummary(source);
            var raw = RunCapture(_path, out var exitCode);
            outputBox.Text = summary + raw + $"[exit {exitCode}]";
            lblStatus.Text = $"运行结束，退出码 {exitCode}";
        }

        /// <summary> 调试运行：执行轨迹 + 全部断点变量快照 / Debug run with trace and snapshots at every breakpoint. </summary>
        private void OnDebugRun(object sender, EventArgs e)
        {
            if (_dbgProc != null) { DebugStop(); return; }
            if (!PrepareAndSave(sender, e)) return;

            var breaks = _breakpoints.GetLines();
            var current = _editor.LineFromPosition(_editor.CurrentPosition) + 1;
            _dbgBreaks = new HashSet<int>(breaks.Count > 0 ? breaks : new List<int> { current });
            _dbgStepMode = breaks.Count == 0;
            _dbgWatched = InteractiveDebugger.ReferencedVariables(_editor.Text);

            var text = File.ReadAllText(_path, Encoding.GetEncoding(936));
            var instrumented = InteractiveDebugger.BuildScript(text, _dbgBreaks);

            // 插桩副本必须放在脚本同目录，否则 %~dp0 会指错
            var dir = Path.GetDirectoryName(_path) ?? Path.GetTempPath();
            _dbgTempFile = Path.Combine(dir, Path.GetFileNameWithoutExtension(_path) + ".__dbg.bat");
            File.WriteAllText(_dbgTempFile, instrumented, Encoding.GetEncoding(936));

            var psi = new ProcessStartInfo("cmd.exe", $"/d /c \"{_dbgTempFile}\"")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.GetEncoding(936),
                StandardErrorEncoding = Encoding.GetEncoding(936),
                WorkingDirectory = dir
            };

            outputBox.Clear();
            AppendDebug($"===== 调试开始（断点：{string.Join(",", _dbgBreaks.OrderBy(x => x))}）=====");

            var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
            proc.OutputDataReceived += (s2, e2) => { if (e2.Data != null) OnDebugLine(e2.Data, isError: false); };
            proc.ErrorDataReceived += (s2, e2) => { if (e2.Data != null) OnDebugLine(e2.Data, isError: true); };
            proc.Exited += (s2, e2) =>
            {
                var code = -1;
                try { code = proc.ExitCode; } catch { }
                BeginInvoke(new Action(() =>
                {
                    AppendDebug($"===== 调试结束（退出码 {code}）=====");
                    lblHint.Text = $"调试结束，退出码 {code}";
                    CleanupDebug();
                }));
            };

            proc.Start();
            _dbgProc = proc;
            _dbgIn = proc.StandardInput;
            _dbgIn.AutoFlush = true;
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();

            lblStatus.Text = "调试中…（F10 单步 / F5 继续 / Shift+F5 停止）";
        }

        /// <summary> 收到一行调试输出：识别探针标记，决定暂停还是放行。 </summary>
        private void OnDebugLine(string line, bool isError)
        {
            if (_dbgDumping)
            {
                _dbgDump.Add(line);
                if (line.StartsWith(InteractiveDebugger.DumpEnd, StringComparison.Ordinal))
                {
                    _dbgDumping = false;
                    var variables = InteractiveDebugger.ParseVariables(_dbgDump);
                    ShowDebugVariables(variables);
                }
                return;
            }

            if (InteractiveDebugger.TryParseMarker(line, out var lineNo))
            {
                _dbgLine = lineNo;

                var shouldPause = _dbgPaused || _dbgStepMode || _dbgBreaks.Contains(lineNo);
                if (!shouldPause)
                {
                    SendDebug("c");                       // 不在断点上：直接放行
                    return;
                }

                _dbgPaused = true;
                _dbgStepMode = false;
                BeginInvoke(new Action(() => HighlightDebugLine(lineNo)));
                SendDebug("d");                           // 请求变量快照
                _dbgDump.Clear();
                _dbgDumping = true;
                return;
            }

            if (line.StartsWith(InteractiveDebugger.DumpEnd, StringComparison.Ordinal)) return;

            if (isError)
            {
                var text = "[stderr] " + line;
                BeginInvoke(new Action(() => AppendDebug(text)));
            }
            else
            {
                var copy = line;
                BeginInvoke(new Action(() => AppendDebug(copy)));
            }
        }

        private void ShowDebugVariables(Dictionary<string, string> variables)
        {
            var interesting = variables
                .Where(kv => _dbgWatched.Contains(kv.Key) || kv.Key.Equals("ERRORLEVEL", StringComparison.OrdinalIgnoreCase))
                .Where(kv => !kv.Key.StartsWith("__bdbg", StringComparison.OrdinalIgnoreCase))
                .OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
                .ToList();

            AppendDebug($"----- 第 {_dbgLine} 行：变量 {interesting.Count} 个 -----");
            foreach (var kv in interesting)
                AppendDebug($"    {kv.Key} = {kv.Value}");

            lblHint.Text = $"调试暂停 第 {_dbgLine} 行（F10 单步 / F5 继续 / Shift+F5 停止）";
            lblHint.ForeColor = Color.FromArgb(0x0B, 0x6B, 0xA7);
        }

        private void HighlightDebugLine(int lineNo)
        {
            if (lineNo < 1 || lineNo > _editor.Lines.Count) return;
            var line = _editor.Lines[lineNo - 1];
            _editor.GotoPosition(line.Position);
            _editor.ScrollCaret();
        }

        private void SendDebug(string command)
        {
            try { _dbgIn?.WriteLine(command); } catch { /* 进程可能已退出 */ }
        }

        private void DebugStep()
        {
            if (_dbgProc == null) return;
            _dbgPaused = false;
            _dbgStepMode = true;
            SendDebug("n");
        }

        private void DebugContinue()
        {
            if (_dbgProc == null) return;
            _dbgPaused = false;
            _dbgStepMode = false;
            SendDebug("c");
        }

        private void DebugStop()
        {
            if (_dbgProc == null) return;
            _dbgPaused = false;
            _dbgStepMode = false;
            SendDebug("q");
            try { if (!_dbgProc.HasExited) _dbgProc.Kill(); } catch { }
            CleanupDebug();
        }

        private void CleanupDebug()
        {
            _dbgProc = null;
            _dbgIn = null;
            _dbgPaused = false;
            _dbgDumping = false;
            try { if (!string.IsNullOrEmpty(_dbgTempFile)) File.Delete(_dbgTempFile); } catch { }
            _dbgTempFile = null;
            lblStatus.Text = "就绪";
        }

        /// <summary> 逐行推算当前目录并写到行尾右侧（--{D:\Test\}）/ Update per-line cwd annotations. </summary>
        private void RefreshCwdNotes(string text)
        {
            try
            {
                var map = CwdTracker.Compute(text);
                var lines = text.Replace("\r\n", "\n").Split('\n');
                var annotations = new Dictionary<int, string>();

                for (var i = 0; i < lines.Length; i++)
                {
                    if (lines[i].Trim().Length == 0) continue;
                    if (!map.TryGetValue(i + 1, out var dir)) continue;

                    // 求不出真实路径（含变量）就不显示，避免 "--{?}" 这种噪音
                    if (string.IsNullOrEmpty(dir) || dir == "?") continue;

                    var note = CwdTracker.Format(dir);
                    if (note.Length == 0) continue;
                    annotations[i + 1] = note;
                }

                _cwdNote.SetCwd(annotations);
            }
            catch
            {
                // 注解失败不影响编辑
            }
        }

        private void AppendDebug(string text)
        {
            if (outputBox.IsDisposed) return;
            outputBox.AppendText(text + Environment.NewLine);
            outputBox.SelectionStart = outputBox.TextLength;
            outputBox.ScrollToCaret();
        }

        /// <summary> 切换光标所在行断点 / Toggle the breakpoint on the caret line. </summary>
        private void OnToggleBreakpoint(object sender, EventArgs e)
        {
            var line = _editor.LineFromPosition(_editor.CurrentPosition) + 1;
            _breakpoints.Toggle(line);
            lblStatus.Text = $"断点行：{string.Join(",", _breakpoints.GetLines())}";
        }

        /// <summary> 编辑器内是否禁用输入法（只影响脚本编辑，输出框不受影响）。 </summary>
        private bool _imeDisabled;

        [DllImport("imm32.dll")]
        private static extern IntPtr ImmAssociateContext(IntPtr hWnd, IntPtr hIMC);

        /// <summary> 关闭时存着默认输入上下文，重新打开时恢复 / Saved default input context. </summary>
        private IntPtr _defaultImc;

        /// <summary> 切换「编辑时禁用输入法」：把编辑器窗口的输入上下文摘掉 / Toggle IME suppression on the editor. </summary>
        private void OnToggleIme(object sender, EventArgs e)
        {
            _imeDisabled = !_imeDisabled;
            miIme.Checked = _imeDisabled;

            var handle = _editor.Handle;
            if (handle == IntPtr.Zero) return;

            if (_imeDisabled)
            {
                // 先记住默认上下文，摘掉后编辑器进入纯英文模式；
                // 只影响编辑器窗口，系统其它窗口的输入法不变
                if (_defaultImc == IntPtr.Zero) _defaultImc = Imm32Helper.GetDefaultContext(handle);
                ImmAssociateContext(handle, IntPtr.Zero);
            }
            else
            {
                ImmAssociateContext(handle, _defaultImc);
                _defaultImc = IntPtr.Zero;
            }

            lblStatus.Text = _imeDisabled ? "输入法已禁用（编辑器纯英文输入）" : "就绪";
        }


    /// <summary> imm32 辅助：取一个「新建窗口」的默认输入上下文 / Default IME context via a throwaway window. </summary>
    internal static class Imm32Helper
    {
        [System.Runtime.InteropServices.DllImport("imm32.dll")]
        private static extern IntPtr ImmGetContext(IntPtr hWnd);

        [System.Runtime.InteropServices.DllImport("imm32.dll")]
        private static extern IntPtr ImmAssociateContext(IntPtr hWnd, IntPtr hIMC);

        [System.Runtime.InteropServices.DllImport("imm32.dll")]
        private static extern IntPtr ImmCreateContext();

        [System.Runtime.InteropServices.DllImport("imm32.dll")]
        private static extern bool ImmDestroyContext(IntPtr hIMC);

        /// <summary> 摘除编辑器上下文前调用：先拿到编辑器自己的默认上下文存下来。 </summary>
        public static IntPtr GetDefaultContext(IntPtr editorHandle)
        {
            var current = ImmGetContext(editorHandle);
            if (current != IntPtr.Zero) return current;

            // 没有现成上下文就新建一个（一直有效，直到程序退出）
            return ImmCreateContext();
        }
    }

        /// <summary> 打开语法帮助窗口 / Open the syntax help window. </summary>
        private void OnHelp(object sender, EventArgs e)
        {
            using (var form = new HelpForm())
            {
                form.ShowDialog(this);
            }
        }

        /// <summary> 当前光标处（或所在行）的问题 / The issue under the caret or on the caret line. </summary>
        private LintIssue IssueAtCaret()
        {
            if (_issues == null || _issues.Count == 0) return null;

            var pos = _editor.CurrentPosition;
            var line = _editor.LineFromPosition(pos) + 1;

            var covering = _issues.FirstOrDefault(i => pos >= i.Start && pos < i.Start + Math.Max(1, i.Length));
            if (covering != null) return covering;

            return _issues.FirstOrDefault(i => i.Line == line);
        }

        /// <summary> Ctrl+.：列出当前问题的自动修复 / List the code fixes for the issue at the caret. </summary>
        private void ShowQuickFixes()
        {
            var issue = IssueAtCaret();
            if (issue == null)
            {
                SetHint("此处没有可修复的问题", false);
                return;
            }

            var fixes = QuickFixes.For(_editor.Text, issue);
            if (fixes.Count == 0)
            {
                SetHint($"{issue.KindLabel}：{issue.Message}（没有自动修复）", true);
                return;
            }

            var menu = new ContextMenuStrip();
            menu.ShowImageMargin = false;

            foreach (var fix in fixes)
            {
                var item = new ToolStripMenuItem(fix.Title);
                item.Click += (s, e) => ApplyFix(fix);
                menu.Items.Add(item);
            }

            var caret = _editor.PointXFromPosition(_editor.CurrentPosition);
            var caretY = _editor.PointYFromPosition(_editor.CurrentPosition);
            var screen = _editor.PointToScreen(new Point(caret, caretY + _editor.Lines[0].Height));
            menu.Show(screen);
        }

        /// <summary> 应用一组文本编辑（从后往前改，避免偏移失效）/ Apply edits back-to-front. </summary>
        private void ApplyFix(QuickFixes.Fix fix)
        {
            var caretTarget = fix.Edits.Count > 0 ? fix.Edits[0].Start : _editor.CurrentPosition;

            foreach (var edit in fix.Edits.OrderByDescending(e => e.Start))
            {
                _editor.TargetStart = Math.Max(0, Math.Min(edit.Start, _editor.TextLength));
                _editor.TargetEnd = Math.Max(0, Math.Min(edit.Start + edit.Length, _editor.TextLength));
                _editor.ReplaceTarget(edit.NewText ?? string.Empty);
            }

            _editor.GotoPosition(Math.Min(caretTarget, _editor.TextLength));
            _editor.ScrollCaret();
            RunLint();
            UpdateStatus();
        }

        /// <summary> F8 / Shift+F8：在当前问题之间跳转 / Jump between issues. </summary>
        private void NavigateIssue(bool forward)
        {
            if (_issues == null || _issues.Count == 0)
            {
                SetHint("没有发现问题", false);
                return;
            }

            var ordered = _issues.OrderBy(i => i.Start).ToList();
            var pos = _editor.CurrentPosition;

            LintIssue target;
            if (forward)
                target = ordered.FirstOrDefault(i => i.Start > pos) ?? ordered.First();
            else
                target = ordered.LastOrDefault(i => i.Start < pos) ?? ordered.Last();

            _editor.GotoPosition(target.Start);
            _editor.ScrollCaret();
            SetHint($"{target.KindLabel}：{target.Message}", true);
        }

        /// <summary> 菜单事件 / Menu handlers. </summary>
        private void OnQuickFix(object sender, EventArgs e) => ShowQuickFixes();
        private void OnPrevIssue(object sender, EventArgs e) => NavigateIssue(forward: false);
        private void OnNextIssue(object sender, EventArgs e) => NavigateIssue(forward: true);

        /// <summary> 状态栏提示（有修复时追加 Ctrl+.）/ Status-bar hint. </summary>
        private void SetHint(string text, bool hasIssue)
        {
            lblHint.Text = text ?? string.Empty;
            lblHint.ForeColor = hasIssue ? Color.FromArgb(0xC0, 0x39, 0x2B) : Color.Gray;
        }

        /// <summary> 处理全局快捷键 F2 切换断点 / Handle F2 to toggle breakpoints. </summary>
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.F)) { ShowFind(false); return true; }
            if (keyData == (Keys.Control | Keys.H)) { ShowFind(true); return true; }

            if (keyData == Keys.F2)
            {
                OnToggleBreakpoint(this, EventArgs.Empty);
                return true;
            }

            // Ctrl+. 快速修复（等价于 VS 的灯泡快捷键）
            if (keyData == (Keys.Control | Keys.OemPeriod) || keyData == (Keys.Control | Keys.Decimal))
            {
                ShowQuickFixes();
                return true;
            }

            // 调试会话中的控制键
            if (_dbgProc != null)
            {
                if (keyData == Keys.F10) { DebugStep(); return true; }
                if (keyData == Keys.F5) { DebugContinue(); return true; }
                if (keyData == (Keys.Shift | Keys.F5)) { DebugStop(); return true; }
                if (keyData == Keys.F9) { DebugStop(); return true; }
            }

            // F8 / Shift+F8 跳到下一处 / 上一处问题
            if (keyData == Keys.F8) { NavigateIssue(forward: true); return true; }
            if (keyData == (Keys.Shift | Keys.F8)) { NavigateIssue(forward: false); return true; }

            if (keyData == Keys.Tab && _ghost.HasGhost && !_editor.AutoCActive)
            {
                _ghost.Accept();
                return true;
            }

            if (keyData == (Keys.Control | Keys.Space))
            {
                _ghost.Clear();
                OfferCommands(true);
                return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        /// <summary> 确认路径并保存，失败返回 false / Ensure a path and save, false on failure. </summary>
        private bool PrepareAndSave(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(_path)) OnSaveAs(sender, e);
            return !string.IsNullOrEmpty(_path) && WriteFile(_path);
        }

        /// <summary> 执行脚本并捕获输出 / Run a script and capture its output. </summary>
        private string RunCapture(string path, out int exitCode, string workDir = null)
        {
            var psi = new ProcessStartInfo("cmd.exe", $"/c \"{path}\"")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.GetEncoding(936),
                StandardErrorEncoding = Encoding.GetEncoding(936)
            };

            if (!string.IsNullOrEmpty(workDir)) psi.WorkingDirectory = workDir;

            using (var p = new Process { StartInfo = psi })
            {
                p.Start();
                var stdout = p.StandardOutput.ReadToEndAsync();
                var stderr = p.StandardError.ReadToEndAsync();
                p.WaitForExit();
                exitCode = p.ExitCode;
                return stdout.Result + stderr.Result;
            }
        }

    }
}
