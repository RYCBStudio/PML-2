// 程序集级标注：告诉 HeadlessUnitTestSession 用哪个 AppBuilder 启动 headless 运行时。
using Avalonia.Headless;

[assembly: AvaloniaTestApplication(typeof(P3TestAppBuilder))]
