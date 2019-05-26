﻿using iTunesPodcastFinder.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;

namespace iTunesPodcastFinder.Helpers
{
    internal static class XmlHelper
    {
        public static PodcastRequestResult ParsePodcast(string xml)
        {
            // Load xml
            XmlDocument xmlDocument = new XmlDocument();
            xmlDocument.LoadXml(xml);
            // Get feedType
            FeedType feedType = GetFeedType(xmlDocument);

            switch (feedType)
            {
                case FeedType.Atom:
                    return ParseAtom(xmlDocument);
                case FeedType.Rss1:
                    return ParseRss1(xmlDocument);
                case FeedType.Rss2:
                    return ParseRss2(xmlDocument);
                default:
                    // Impossible situation
                    throw new Exception();
            }
        }

        private static FeedType GetFeedType(XmlDocument xmlDocument)
        {
            string rssVersion = xmlDocument.GetElementsByTagName("rss").Item(0)?.Attributes["version"].Value;
            switch (rssVersion)
            {
                case "1.0":
                    return FeedType.Rss1;
                case "2.0":
                    return FeedType.Rss2;
                default:
                    return FeedType.Atom;
            }
        }

        // ATOM
        private static PodcastRequestResult ParseAtom(XmlDocument xmlDocument)
        {
            XmlElement feedNode = xmlDocument["feed"];

            Podcast podcast = new Podcast();
            podcast.Name = GetXmlElementValue(feedNode, "title");
            podcast.ArtWork = GetXmlElementValue(feedNode, "icon");
            XmlNodeList entries = feedNode.GetElementsByTagName("entry");
            podcast.EpisodesCount = entries.Count;
            podcast.InnerXml = feedNode.InnerXml;
            podcast.FeedType = FeedType.Atom;
            return new PodcastRequestResult(podcast, GetAtomEpisodes(entries));
        }
        private static IEnumerable<PodcastEpisode> GetAtomEpisodes(XmlNodeList entries)
        {
            int episodeNumber = entries.Count;
            foreach (XmlNode entry in entries)
            {
                PodcastEpisode episode = new PodcastEpisode();
                episode.EpisodeNumber = episodeNumber--;
                episode.Title = GetXmlElementValue(entry, "title");
                episode.Summary = GetXmlElementValue(entry, "summary");
                DateTime.TryParse(GetXmlElementValue(entry, "updated"), out DateTime pub);
                episode.PublishedDate = pub;
                episode.FileUrl = new Uri(GetXmlAttribute(entry["link"], "href"));
                episode.InnerXml = entry.InnerXml;
                yield return episode;
            }
        }

        // RSS1.0
        private static PodcastRequestResult ParseRss1(XmlDocument xmlDocument)
        {
            XmlElement channel = xmlDocument.GetElementsByTagName("channel").Item(0) as XmlElement;

            Podcast podcast = new Podcast();
            podcast.Name = GetXmlElementValue(channel, "title");
            podcast.Summary = GetXmlElementValue(channel, "description");
            podcast.ItunesLink = GetXmlElementValue(channel, "link");
            var entries = channel.GetElementsByTagName("item");
            podcast.EpisodesCount = entries.Count;
            podcast.InnerXml = channel.InnerXml;
            podcast.FeedType = FeedType.Rss1;
            return new PodcastRequestResult(podcast, GetRSS1Episodes(entries));
        }
        private static IEnumerable<PodcastEpisode> GetRSS1Episodes(XmlNodeList entries)
        {
            int episodeNumber = entries.Count;
            foreach (XmlNode entry in entries)
            {
                PodcastEpisode episode = new PodcastEpisode();
                episode.EpisodeNumber = episodeNumber--;
                episode.Editor = GetXmlElementValue(entry, "author");
                episode.Title = GetXmlElementValue(entry, "title");
                episode.Summary = GetXmlElementValue(entry, "description");
                episode.FileUrl = new Uri(GetXmlElementValue(entry, "link"));
                DateTime.TryParse(GetXmlElementValue(entry, "pubDate"), out DateTime pubDate);
                episode.PublishedDate = pubDate;
                episode.InnerXml = entry.InnerXml;
                yield return episode;
            }
        }

        // RSS2.0
        private static PodcastRequestResult ParseRss2(XmlDocument xmlDocument)
        {
            XmlElement channel = xmlDocument.GetElementsByTagName("channel").Item(0) as XmlElement;

            Podcast podcast = new Podcast();
            podcast.Name = GetXmlElementValue(channel, "title");
            podcast.Editor = channel.GetElementsByTagName("itunes:author")?.Item(0)?.InnerText;
            podcast.ItunesLink = GetXmlElementValue(channel, "link");
            podcast.Summary = GetXmlElementValue(channel, "description");
            DateTime.TryParse(GetXmlElementValue(channel, "pubDate"), out DateTime pub);
            podcast.ReleaseDate = pub;
            string image = channel.GetElementsByTagName("itunes:image")?.Item(0)?.Attributes.Item(0)?.Value;
            podcast.Genre = channel.GetElementsByTagName("itunes:category")?.Item(0)?.Attributes.Item(0)?.Value;
            podcast.InnerXml = channel.InnerXml;
            podcast.FeedType = FeedType.Rss2;
            if (image == "")
            {
                var imageNodes = channel.GetElementsByTagName("image");
                if (imageNodes.Count > 0)
                {
                    var imageNode = imageNodes[0];
                    image = GetXmlElementValue(imageNode, "url");
                }
            }
            podcast.ArtWork = image;
            XmlNodeList entries = channel.GetElementsByTagName("item");
            podcast.EpisodesCount = entries.Count;
            return new PodcastRequestResult(podcast, GetRSS2Episodes(entries, podcast.Editor));

        }

        private static IEnumerable<PodcastEpisode> GetRSS2Episodes(XmlNodeList entries, string editor)
        {
            int episodeNumber = entries.Count;
            foreach (XmlNode entry in entries)
            {
                PodcastEpisode episode = new PodcastEpisode();
                episode.EpisodeNumber = episodeNumber--;
                episode.Editor = editor;
                episode.Title = GetXmlElementValue(entry, "title");
                episode.Summary = GetXmlElementValue(entry, "description");
                string link = GetXmlElementValue(entry, "link");
                if (entry["enclosure"] != null)
                {
                    link = GetXmlAttribute(entry["enclosure"], "url");
                }
                episode.FileUrl = new Uri(link);
                string date = GetXmlElementValue(entry, "pubDate");
                DateTime.TryParse(date, out DateTime pubDate);
                episode.PublishedDate = pubDate;
                string durationString = GetXmlElementValue(entry, "itunes:duration");
                if (int.TryParse(durationString, out int duration))
                    episode.Duration = TimeSpan.FromSeconds(duration);
                else if (TimeSpan.TryParse(durationString, out TimeSpan durationTS))
                    episode.Duration = durationTS;
                else
                    episode.Duration = default;
                episode.InnerXml = entry.InnerXml;
                yield return episode;
            }
        }

        private static string GetXmlElementValue(XmlNode parentNode, string elementName)
        {
            string value = string.Empty;
            if (parentNode[elementName] != null)
                value = parentNode[elementName].InnerText;
            return value;
        }
        private static string GetXmlAttribute(XmlNode xmlNode, string attributeName)
        {
            string attribute = string.Empty;
            if (xmlNode != null && xmlNode.Attributes != null && xmlNode.Attributes[attributeName] != null)
            {
                string value = xmlNode.Attributes[attributeName].Value;
                if (!string.IsNullOrEmpty(value))
                    attribute = value;
            }
            return attribute;
        }

    }
}