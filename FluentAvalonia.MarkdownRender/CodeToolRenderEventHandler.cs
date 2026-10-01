using Avalonia.Controls;
using Markdig.Syntax;

namespace FluentAvalonia.MarkdownRender;

public delegate void CodeToolRenderEventHandler(StackPanel headerPanel, StackPanel stackPanel,
    FencedCodeBlock fencedCodeBlock);