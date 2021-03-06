﻿using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Providers;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Model.Library;

namespace MediaBrowser.Controller.LiveTv
{
    public interface ILiveTvRecording : IHasImages, IHasMediaStreams
    {
        string ServiceName { get; set; }

        string MediaType { get; }

        RecordingInfo RecordingInfo { get; set; }

        string GetClientTypeName();

        string GetUserDataKey();

        bool IsParentalAllowed(User user);

        Task RefreshMetadata(MetadataRefreshOptions options, CancellationToken cancellationToken);

        PlayAccess GetPlayAccess(User user);
    }
}
