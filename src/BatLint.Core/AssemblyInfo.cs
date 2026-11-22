using System.Runtime.CompilerServices;

// 内核类型保持 internal，只对本解决方案内的 UI 与 CLI 开放
[assembly: InternalsVisibleTo("batIDE")]
[assembly: InternalsVisibleTo("batlint")]
