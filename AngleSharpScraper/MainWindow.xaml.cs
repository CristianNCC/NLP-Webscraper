﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Threading;
using System.Net.Http;
using AngleSharp.Html.Parser;
using AngleSharp.Html.Dom;
using System.IO;
using AngleSharp.Dom;
using AngleSharp.Text;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Diagnostics;
using System.Windows.Navigation;

namespace AngleSharpScraper
{
    public partial class MainWindow : Window
    {
        private string siteUrl = "https://thehackernews.com/";
        private int numberOfPages = 50;

        private string ArticleTitle { get; set; }
        private string Url { get; set; }
        public string[] QueryTerms { get; set; }

        public Dictionary<string, List<Tuple<string, string>>> termToScrapeDictionary = new Dictionary<string, List<Tuple<string, string>>>();

        private OpenNLP.Tools.SentenceDetect.MaximumEntropySentenceDetector mSentenceDetector;
        private OpenNLP.Tools.Tokenize.EnglishMaximumEntropyTokenizer mTokenizer;
        private OpenNLP.Tools.PosTagger.EnglishMaximumEntropyPosTagger mPosTagger;
        private OpenNLP.Tools.Chunker.EnglishTreebankChunker mChunker;

        private string[] SplitSentences(string paragraph)
        {
            if (mSentenceDetector == null)
            {
                mSentenceDetector = new OpenNLP.Tools.SentenceDetect.EnglishMaximumEntropySentenceDetector("EnglishSD.nbin");
            }

            return mSentenceDetector.SentenceDetect(paragraph);
        }

        private string[] TokenizeSentence(string sentence)
        {
            if (mTokenizer == null)
            {
                mTokenizer = new OpenNLP.Tools.Tokenize.EnglishMaximumEntropyTokenizer("EnglishTok.nbin");
            }

            return mTokenizer.Tokenize(sentence);
        }

        private string[] PosTagTokens(string[] tokens)
        {
            if (mPosTagger == null)
            {
                mPosTagger = new OpenNLP.Tools.PosTagger.EnglishMaximumEntropyPosTagger("EnglishPOS.nbin");
            }

            return mPosTagger.Tag(tokens);
        }

        private string ChunkTokensPostag(string[] tokens, string[] postags)
        {
            if (mChunker == null)
            {
                mChunker = new OpenNLP.Tools.Chunker.EnglishTreebankChunker("EnglishChunk.nbin");
            }

            return mChunker.GetChunks(tokens, postags);
        }

        private SharpEntropy.GisModel TrainLanguageModel(string trainingDataFile)
        {
            System.IO.StreamReader trainingStreamReader = new System.IO.StreamReader(trainingDataFile);
            SharpEntropy.ITrainingEventReader eventReader = new SharpEntropy.BasicEventReader(new SharpEntropy.PlainTextByLineDataReader(trainingStreamReader));
            SharpEntropy.GisTrainer trainer = new SharpEntropy.GisTrainer();
            trainer.TrainModel(eventReader);
            return new SharpEntropy.GisModel(trainer);
        }

        public void POSTagger_Method(string sent)
        {
            File.WriteAllText("POSTagged.txt", sent + "\n\n");
            string[] split_sentences = SplitSentences(sent);
            foreach (string sentence in split_sentences)
            {
                File.AppendAllText("POSTagged.txt", sentence + "\n");
                string[] tokens = TokenizeSentence(sentence);
                string[] tags = PosTagTokens(tokens);
                string chunkPostag = ChunkTokensPostag(tokens, tags);

                for (int currentTag = 0; currentTag < tags.Length; currentTag++)
                {
                    File.AppendAllText("POSTagged.txt", tokens[currentTag] + " - " + tags[currentTag] + "\n\n");
                }

                File.AppendAllText("POSTagged.txt", chunkPostag);
                File.AppendAllText("POSTagged.txt", "\n\n");
            }
        }

        internal async void ScrapeWebsite()
        {
            CancellationTokenSource cancellationToken = new CancellationTokenSource();
            HttpClient httpClient = new HttpClient();
            HtmlParser parser = new HtmlParser();

            resultsStackPanel.Children.Clear();

            for (int iPageIdx = 0; iPageIdx < numberOfPages; iPageIdx++)
            {
                HttpResponseMessage request = await httpClient.GetAsync(siteUrl);
                cancellationToken.Token.ThrowIfCancellationRequested();

                Stream response = await request.Content.ReadAsStreamAsync();
                cancellationToken.Token.ThrowIfCancellationRequested();

                IHtmlDocument document = parser.ParseDocument(response);
                GetScrapeResults(document);

                siteUrl = document.All.Where(x => x.ClassName == "blog-pager-older-link-mobile")
                    .FirstOrDefault()?
                    .OuterHtml.ReplaceFirst("<a class=\"blog-pager-older-link-mobile\" href=\"", "")
                    .ReplaceFirst("\" id", "*")
                    .Split('*').FirstOrDefault();

                if (string.IsNullOrEmpty(siteUrl))
                    break;
            }

            foreach (var termToScrape in termToScrapeDictionary)
            {
                List<Tuple<string, string>> termResults = termToScrape.Value;

                GroupBox termGroupBox = new GroupBox()
                {
                    Header = termToScrape.Key,
                    Content = new StackPanel()
                    {
                        Orientation = Orientation.Vertical,
                        Margin = new Thickness(5, 5, 5, 5)
                    }
                };

                resultsStackPanel.Children.Add(termGroupBox);

                for (int iTermResult = 0; iTermResult < termResults.Count; iTermResult++)
                {
                    TextBlock title = new TextBlock()
                    {
                        Text = termToScrapeDictionary[termToScrape.Key][iTermResult].Item1
                    };

                    (termGroupBox.Content as StackPanel).Children.Add(title);

                    Hyperlink hyperlink = new Hyperlink();
                    hyperlink.Inlines.Add(termToScrapeDictionary[termToScrape.Key][iTermResult].Item2);
                    hyperlink.NavigateUri = new Uri(termToScrapeDictionary[termToScrape.Key][iTermResult].Item2);
                    hyperlink.RequestNavigate += Hyperlink_RequestNavigate;

                    TextBlock urlTextBlock = new TextBlock();
                    urlTextBlock.Inlines.Add(hyperlink);
                    urlTextBlock.Margin = new Thickness(5, 5, 5, 10);

                    (termGroupBox.Content as StackPanel).Children.Add(urlTextBlock);
                }
            }

            spinnerControl.Visibility = System.Windows.Visibility.Collapsed;
            httpClient.Dispose();
            cancellationToken.Dispose();
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));
            e.Handled = true;
        }

        private void GetScrapeResults(IHtmlDocument document)
        {
            List<IElement> articleLink = new List<IElement>();
            foreach (var term in QueryTerms)
            {
                articleLink = document.All.Where(x => x.ClassName == "story-link" && (x.InnerHtml.Contains(term) || 
                        x.InnerHtml.Contains(term.ToLower()))).ToList();

                FillResultsDictionary(term, articleLink);
            }
        }

        public void FillResultsDictionary(string term, IEnumerable<IElement> articleLink)
        {
            foreach (var result in articleLink)
            {
                CleanUpResults(result);
                if (!string.IsNullOrWhiteSpace(ArticleTitle) && !string.IsNullOrWhiteSpace(Url))
                {
                    string currentLine = $"{ArticleTitle}{Environment.NewLine} " +
                        $"- {Url}{Environment.NewLine}{Environment.NewLine}";

                    if (!termToScrapeDictionary.ContainsKey(term))
                        termToScrapeDictionary[term] = new List<Tuple<string, string>>();

                    termToScrapeDictionary[term].Add(new Tuple<string, string>(ArticleTitle, Url));
                }
            }
        }

        private void CleanUpResults(IElement result)
        {
            string htmlResult = result.OuterHtml.ReplaceFirst("<a class=\"story-link\" href=\"", "");
            htmlResult = htmlResult.ReplaceFirst("\">", "");
            htmlResult = htmlResult.ReplaceFirst("\n<div class=\"clear home-post-box cf\">\n<div class=\"home-img clear\">\n<div class=\"img-ratio\"><img alt=\"", " * ");
            htmlResult = htmlResult.ReplaceFirst("\" class=\"home-img-src lazyload\"", "*");       
            SplitResults(htmlResult);
        }

        private void SplitResults(string htmlResult)
        {
            string[] splitResults = htmlResult.Split('*');
            Url = splitResults[0];
            ArticleTitle = splitResults[1];
        }

        public MainWindow()
        {
            InitializeComponent();
            POSTagger_Method(File.ReadAllText("Testing NLP.txt"));
        }

        private void ScrapWebsiteEvent(object sender, RoutedEventArgs e)
        {
            spinnerControl.Visibility = System.Windows.Visibility.Visible;

            QueryTerms = queryTermsTextBox.Text.Split(';');
            ScrapeWebsite();
        }
    }
}
