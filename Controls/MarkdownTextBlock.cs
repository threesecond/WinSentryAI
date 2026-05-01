using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using MdBlock = Markdig.Syntax.Block;
using MdInline = Markdig.Syntax.Inlines.Inline;

namespace WinSentryAI.Controls
{
    public class MarkdownTextBlock : TextBlock
    {
        private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
            .DisableHtml()
            .Build();

        public static readonly DependencyProperty MarkdownProperty =
            DependencyProperty.Register(
                nameof(Markdown),
                typeof(string),
                typeof(MarkdownTextBlock),
                new PropertyMetadata(string.Empty, OnMarkdownChanged));

        public string Markdown
        {
            get => (string)GetValue(MarkdownProperty);
            set => SetValue(MarkdownProperty, value);
        }

        private static void OnMarkdownChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is MarkdownTextBlock textBlock)
            {
                textBlock.RenderMarkdown(e.NewValue as string ?? string.Empty);
            }
        }

        private void RenderMarkdown(string markdown)
        {
            Inlines.Clear();
            if (string.IsNullOrWhiteSpace(markdown))
            {
                return;
            }

            var document = global::Markdig.Markdown.Parse(markdown, Pipeline);
            var isFirstBlock = true;

            foreach (var block in document)
            {
                RenderBlock(block, 0, ref isFirstBlock);
            }
        }

        private void RenderBlock(MdBlock block, int listDepth, ref bool isFirstBlock)
        {
            switch (block)
            {
                case ParagraphBlock paragraph:
                    AddBlockBreak(ref isFirstBlock);
                    RenderInlineContainer(paragraph.Inline, isStrong: false);
                    break;

                case HeadingBlock heading:
                    AddBlockBreak(ref isFirstBlock);
                    RenderInlineContainer(heading.Inline, isStrong: true, fontSizeDelta: HeadingFontSizeDelta(heading.Level));
                    break;

                case ListBlock list:
                    RenderList(list, listDepth, ref isFirstBlock);
                    break;

                case CodeBlock code:
                    AddBlockBreak(ref isFirstBlock);
                    AddCodeRun(code.Lines.ToString().TrimEnd());
                    break;

                case QuoteBlock quote:
                    foreach (var child in quote)
                    {
                        RenderBlock(child, listDepth, ref isFirstBlock);
                    }
                    break;

                case ThematicBreakBlock:
                case HtmlBlock:
                    break;

                case ContainerBlock container:
                    foreach (var child in container)
                    {
                        RenderBlock(child, listDepth, ref isFirstBlock);
                    }
                    break;

                case LeafBlock leaf:
                    AddBlockBreak(ref isFirstBlock);
                    RenderInlineContainer(leaf.Inline, isStrong: false);
                    break;
            }
        }

        private void RenderList(ListBlock list, int listDepth, ref bool isFirstBlock)
        {
            var itemNumber = int.TryParse(list.OrderedStart, out var parsedStart) && parsedStart > 0
                ? parsedStart
                : 1;
            foreach (var item in list.OfType<ListItemBlock>())
            {
                AddBlockBreak(ref isFirstBlock);
                AddRun(new string(' ', Math.Min(listDepth, 3) * 2), false);
                AddRun(list.IsOrdered ? $"{itemNumber++}. " : "\u2022 ", false);

                var isFirstChild = true;
                foreach (var child in item)
                {
                    if (!isFirstChild)
                    {
                        Inlines.Add(new LineBreak());
                    }

                    if (child is ParagraphBlock paragraph)
                    {
                        RenderInlineContainer(paragraph.Inline, isStrong: false);
                    }
                    else
                    {
                        var nestedFirst = true;
                        RenderBlock(child, listDepth + 1, ref nestedFirst);
                    }

                    isFirstChild = false;
                }
            }
        }

        private void RenderInlineContainer(ContainerInline? container, bool isStrong, double fontSizeDelta = 0)
        {
            if (container == null)
            {
                return;
            }

            foreach (var inline in container)
            {
                RenderInline(inline, isStrong, fontSizeDelta);
            }
        }

        private void RenderInline(MdInline inline, bool isStrong, double fontSizeDelta)
        {
            switch (inline)
            {
                case LiteralInline literal:
                    AddRun(literal.Content.ToString(), isStrong, fontSizeDelta);
                    break;

                case CodeInline code:
                    AddCodeRun(code.Content);
                    break;

                case EmphasisInline emphasis:
                    var emphasisStrong = isStrong || emphasis.DelimiterCount >= 2;
                    foreach (var child in emphasis)
                    {
                        RenderInline(child, emphasisStrong, fontSizeDelta);
                    }
                    break;

                case LinkInline link:
                    RenderLink(link, isStrong, fontSizeDelta);
                    break;

                case LineBreakInline:
                    Inlines.Add(new LineBreak());
                    break;

                case HtmlInline:
                    break;

                case ContainerInline container:
                    RenderInlineContainer(container, isStrong, fontSizeDelta);
                    break;
            }
        }

        private void RenderLink(LinkInline link, bool isStrong, double fontSizeDelta)
        {
            if (link.IsImage)
            {
                var altText = GetInlineText(link);
                if (!string.IsNullOrWhiteSpace(altText))
                {
                    AddRun(altText, isStrong, fontSizeDelta);
                }
                return;
            }

            var beforeCount = Inlines.Count;
            RenderInlineContainer(link, isStrong, fontSizeDelta);

            if (!string.IsNullOrWhiteSpace(link.Url) && Inlines.Count == beforeCount)
            {
                AddRun(link.Url, isStrong, fontSizeDelta);
            }
        }

        private void AddBlockBreak(ref bool isFirstBlock)
        {
            if (isFirstBlock)
            {
                isFirstBlock = false;
                return;
            }

            Inlines.Add(new LineBreak());
            Inlines.Add(new LineBreak());
        }

        private void AddRun(string text, bool isStrong, double fontSizeDelta = 0)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            Inlines.Add(new Run(text)
            {
                FontWeight = isStrong ? FontWeights.SemiBold : FontWeights.Normal,
                FontSize = Math.Max(10, FontSize + fontSizeDelta)
            });
        }

        private void AddCodeRun(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            Inlines.Add(new Run(text)
            {
                FontFamily = new FontFamily("Consolas"),
                Background = TryFindResource("AppHoverBrush") as Brush,
                FontSize = Math.Max(10, FontSize - 1)
            });
        }

        private static double HeadingFontSizeDelta(int level) => level switch
        {
            <= 1 => 3,
            2 => 2,
            _ => 1
        };

        private static string GetInlineText(ContainerInline container)
        {
            return string.Concat(container.Select(inline => inline switch
            {
                LiteralInline literal => literal.Content.ToString(),
                CodeInline code => code.Content,
                ContainerInline nested => GetInlineText(nested),
                _ => string.Empty
            }));
        }
    }
}
