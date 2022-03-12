// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using HtmlReaderWriter;

namespace Microsoft.Docs.Build;

internal class PdfBuilder
{
    private readonly ErrorBuilder _errors;
    private readonly TocLoader _tocLoader;
    private readonly Output _output;
    private readonly DocumentProvider _documentProvider;

    public PdfBuilder(
        ErrorBuilder errors, TocLoader tocLoader, Output output, DocumentProvider documentProvider)
    {
        _errors = errors;
        _tocLoader = tocLoader;
        _output = output;
        _documentProvider = documentProvider;
    }

    public void BuildPdf(FilePath[] tocs)
    {
        using var scope = Progress.Start($"Building PDF");

        ParallelUtility.ForEach(scope, _errors, tocs, toc => BuildPdf(scope, toc));
    }

    private void BuildPdf(LogScope scope, FilePath toc)
    {
        var (node, _, _, _) = _tocLoader.Load(toc);
        var nodes = new List<TocNode>();
        FlattenToc(nodes, node);

        var htmls = new string?[nodes.Count];
        ParallelUtility.ForEach(scope, _errors, nodes, (node, i) => htmls[i] = BuildPdfHtml(node));

        var outputPath = Path.ChangeExtension(_documentProvider.GetOutputPath(toc), ".pdf.html");
        using var writer = new StreamWriter(_output.WriteStream(outputPath));

        foreach (var html in htmls)
        {
            writer.Write(html);
        }

        static void FlattenToc(List<TocNode> nodes, TocNode node)
        {
            nodes.Add(node);
            foreach (var item in node.Items)
            {
                FlattenToc(nodes, item);
            }
        }
    }

    private string? BuildPdfHtml(TocNode node)
    {
        if (node.Document is null)
        {
            return null;
        }

        var htmlPath = Path.Combine(_output.OutputPath, _documentProvider.GetOutputPath(node.Document));
        if (!File.Exists(htmlPath))
        {
            return null;
        }

        var pageId = ToPageId(_documentProvider.GetSiteUrl(node.Document));
        var sitePath = Path.GetDirectoryName(_documentProvider.GetSitePath(node.Document)) ?? ".";
        var html = File.ReadAllText(htmlPath);

        return HtmlUtility.TransformHtml(html, TransformPdfHtml);

        static string ToPageId(string url)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(url));
        }

        void TransformPdfHtml(ref HtmlReader reader, ref HtmlWriter writer, ref HtmlToken token)
        {
            if (token.Type == HtmlTokenType.StartTag)
            {
                foreach (ref var attribute in token.Attributes.Span)
                {
                    // Prepend page id to id
                    if (attribute.NameIs("id"))
                    {
                        attribute = attribute.WithValue(pageId + attribute.Value);
                    }

                    // Adjust link URL to point to page id
                    if (HtmlUtility.IsLink(ref token, attribute, out var _, out var _))
                    {
                        var link = attribute.Value.Span.ToString();
                        var linkType = UrlUtility.GetLinkType(link);
                        if (linkType == LinkType.RelativePath || linkType == LinkType.SelfBookmark)
                        {
                            var (path, query, fragment) = UrlUtility.SplitUrl(link);
                            var targetUrl = UrlUtility.Combine(sitePath, path);
                            var targetPageId = ToPageId(targetUrl);
                            var anchor = string.Concat("#", targetPageId, fragment.TrimStart('#'));
                            attribute = attribute.WithValue(anchor);
                        }
                    }
                }

                // Add page id to main
                if (token.NameIs("main"))
                {
                    token.SetAttributeValue("id", pageId);
                }
            }
        }
    }
}
