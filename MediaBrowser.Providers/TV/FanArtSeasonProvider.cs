﻿using MediaBrowser.Common.IO;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using MediaBrowser.Providers.Music;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

namespace MediaBrowser.Providers.TV
{
    public class FanartSeasonProvider : IRemoteImageProvider, IHasOrder, IHasChangeMonitor
    {
        private readonly CultureInfo _usCulture = new CultureInfo("en-US");
        private readonly IServerConfigurationManager _config;
        private readonly IHttpClient _httpClient;
        private readonly IFileSystem _fileSystem;

        public FanartSeasonProvider(IServerConfigurationManager config, IHttpClient httpClient, IFileSystem fileSystem)
        {
            _config = config;
            _httpClient = httpClient;
            _fileSystem = fileSystem;
        }

        public string Name
        {
            get { return ProviderName; }
        }

        public static string ProviderName
        {
            get { return "FanArt"; }
        }

        public bool Supports(IHasImages item)
        {
            return item is Season;
        }

        public IEnumerable<ImageType> GetSupportedImages(IHasImages item)
        {
            return new List<ImageType>
            {
                ImageType.Backdrop, 
                ImageType.Thumb
            };
        }

        public async Task<IEnumerable<RemoteImageInfo>> GetImages(IHasImages item, CancellationToken cancellationToken)
        {
            var list = new List<RemoteImageInfo>();

            var season = (Season)item;
            var series = season.Series;

            if (series != null)
            {
                var id = series.GetProviderId(MetadataProviders.Tvdb);

                if (!string.IsNullOrEmpty(id) && season.IndexNumber.HasValue)
                {
                    await FanartSeriesProvider.Current.EnsureSeriesXml(id, cancellationToken).ConfigureAwait(false);

                    var xmlPath = FanartSeriesProvider.Current.GetFanartXmlPath(id);

                    try
                    {
                        AddImages(list, season.IndexNumber.Value, xmlPath, cancellationToken);
                    }
                    catch (FileNotFoundException)
                    {
                        // No biggie. Don't blow up
                    }
                }
            }

            var language = item.GetPreferredMetadataLanguage();

            var isLanguageEn = string.Equals(language, "en", StringComparison.OrdinalIgnoreCase);

            // Sort first by width to prioritize HD versions
            return list.OrderByDescending(i => i.Width ?? 0)
                .ThenByDescending(i =>
                {
                    if (string.Equals(language, i.Language, StringComparison.OrdinalIgnoreCase))
                    {
                        return 3;
                    }
                    if (!isLanguageEn)
                    {
                        if (string.Equals("en", i.Language, StringComparison.OrdinalIgnoreCase))
                        {
                            return 2;
                        }
                    }
                    if (string.IsNullOrEmpty(i.Language))
                    {
                        return isLanguageEn ? 3 : 2;
                    }
                    return 0;
                })
                .ThenByDescending(i => i.CommunityRating ?? 0)
                .ThenByDescending(i => i.VoteCount ?? 0);
        }

        private void AddImages(List<RemoteImageInfo> list, int seasonNumber, string xmlPath, CancellationToken cancellationToken)
        {
            using (var streamReader = new StreamReader(xmlPath, Encoding.UTF8))
            {
                // Use XmlReader for best performance
                using (var reader = XmlReader.Create(streamReader, new XmlReaderSettings
                {
                    CheckCharacters = false,
                    IgnoreProcessingInstructions = true,
                    IgnoreComments = true,
                    ValidationType = ValidationType.None
                }))
                {
                    reader.MoveToContent();

                    // Loop through each element
                    while (reader.Read())
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        if (reader.NodeType == XmlNodeType.Element)
                        {
                            switch (reader.Name)
                            {
                                case "series":
                                    {
                                        using (var subReader = reader.ReadSubtree())
                                        {
                                            AddImages(list, subReader, seasonNumber, cancellationToken);
                                        }
                                        break;
                                    }

                                default:
                                    reader.Skip();
                                    break;
                            }
                        }
                    }
                }
            }
        }

        private void AddImages(List<RemoteImageInfo> list, XmlReader reader, int seasonNumber, CancellationToken cancellationToken)
        {
            reader.MoveToContent();

            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.Element)
                {
                    switch (reader.Name)
                    {
                        case "seasonthumbs":
                            {
                                using (var subReader = reader.ReadSubtree())
                                {
                                    PopulateImageCategory(list, subReader, cancellationToken, ImageType.Thumb, 500, 281, seasonNumber);
                                }
                                break;
                            }
                        case "showbackgrounds":
                            {
                                using (var subReader = reader.ReadSubtree())
                                {
                                    PopulateImageCategory(list, subReader, cancellationToken, ImageType.Backdrop, 1920, 1080, seasonNumber);
                                }
                                break;
                            }
                        default:
                            {
                                using (reader.ReadSubtree())
                                {
                                }
                                break;
                            }
                    }
                }
            }
        }

        private void PopulateImageCategory(List<RemoteImageInfo> list, XmlReader reader, CancellationToken cancellationToken, ImageType type, int width, int height, int seasonNumber)
        {
            reader.MoveToContent();

            while (reader.Read())
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (reader.NodeType == XmlNodeType.Element)
                {
                    switch (reader.Name)
                    {
                        case "seasonthumb":
                        case "showbackground":
                            {
                                var url = reader.GetAttribute("url");
                                var season = reader.GetAttribute("season");

                                int imageSeasonNumber;

                                if (!string.IsNullOrEmpty(url) &&
                                    !string.IsNullOrEmpty(season) &&
                                    int.TryParse(season, NumberStyles.Any, _usCulture, out imageSeasonNumber) &&
                                    seasonNumber == imageSeasonNumber)
                                {
                                    var likesString = reader.GetAttribute("likes");
                                    int likes;

                                    var info = new RemoteImageInfo
                                    {
                                        RatingType = RatingType.Likes,
                                        Type = type,
                                        Width = width,
                                        Height = height,
                                        ProviderName = Name,
                                        Url = url,
                                        Language = reader.GetAttribute("lang")
                                    };

                                    if (!string.IsNullOrEmpty(likesString) && int.TryParse(likesString, NumberStyles.Any, _usCulture, out likes))
                                    {
                                        info.CommunityRating = likes;
                                    }

                                    list.Add(info);
                                }

                                break;
                            }
                        default:
                            reader.Skip();
                            break;
                    }
                }
            }
        }

        public int Order
        {
            get { return 1; }
        }

        public Task<HttpResponseInfo> GetImageResponse(string url, CancellationToken cancellationToken)
        {
            return _httpClient.GetResponse(new HttpRequestOptions
            {
                CancellationToken = cancellationToken,
                Url = url,
                ResourcePool = FanartArtistProvider.Current.FanArtResourcePool
            });
        }

        public bool HasChanged(IHasMetadata item, IDirectoryService directoryService, DateTime date)
        {
            if (!_config.Configuration.EnableFanArtUpdates)
            {
                return false;
            }
            
            var season = (Season)item;
            var series = season.Series;

            if (series == null)
            {
                return false;
            }

            var tvdbId = series.GetProviderId(MetadataProviders.Tvdb);

            if (!String.IsNullOrEmpty(tvdbId))
            {
                // Process images
                var imagesXmlPath = FanartSeriesProvider.Current.GetFanartXmlPath(tvdbId);

                var fileInfo = new FileInfo(imagesXmlPath);

                return !fileInfo.Exists || _fileSystem.GetLastWriteTimeUtc(fileInfo) > date;
            }

            return false;
        }
    }
}
