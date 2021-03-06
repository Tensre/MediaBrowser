﻿using MediaBrowser.Common.IO;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Logging;
using System.IO;
using System.Threading;

namespace MediaBrowser.Providers.Music
{
    class AlbumXmlProvider : BaseXmlProvider<MusicAlbum>
    {
        private readonly ILogger _logger;

        public AlbumXmlProvider(IFileSystem fileSystem, ILogger logger)
            : base(fileSystem)
        {
            _logger = logger;
        }

        protected override void Fetch(LocalMetadataResult<MusicAlbum> result, string path, CancellationToken cancellationToken)
        {
            new BaseItemXmlParser<MusicAlbum>(_logger).Fetch(result.Item, path, cancellationToken);
        }

        protected override FileInfo GetXmlFile(ItemInfo info, IDirectoryService directoryService)
        {
            return directoryService.GetFile(Path.Combine(info.Path, "album.xml"));
        }
    }
}
