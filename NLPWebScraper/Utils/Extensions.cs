﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NLPWebScraper
{
    public static class Extensions
    {
        public static int GetMedian(this List<int> sourceNumbers)
        {
            if (sourceNumbers == null || sourceNumbers.Count == 0)
                throw new System.Exception("Median of empty array not defined.");

            int[] sortedPNumbers = sourceNumbers.ToArray();
            Array.Sort(sortedPNumbers);

            int size = sortedPNumbers.Length;
            int mid = size / 2;
            int median = (size % 2 != 0) ? sortedPNumbers[mid] : (sortedPNumbers[mid] + sortedPNumbers[mid - 1]) / 2;
            return median;
        }

        public static float GetNodeTextDensity(this AngleSharp.Dom.IElement element)
        {
            return ((float)element.TextContent.Length) / ((float)element.InnerHtml.Length);
        }

        public static float GetNodeHyperlinkDensity(this AngleSharp.Dom.IElement element)
        {
            return ((float)element.BaseUri.Length) / ((float)element.InnerHtml.Length);
        }

        public static bool IsSimilarWith(this Tuple<AngleSharp.Dom.IElement, float, float> element, Tuple<AngleSharp.Dom.IElement, float, float> toCompare, float epsilon)
        {
            return Math.Abs((element.Item2 - toCompare.Item2) * 0.2) + Math.Abs((element.Item3 - toCompare.Item3) * 0.8) < epsilon;
        }
    }
}