﻿using System.Collections.Generic;
using System.Linq;
using NLog;
using NzbDrone.Common.Composition;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.SkyhookNotifications;
using NzbDrone.Core.ThingiProvider;

namespace NzbDrone.Core.Indexers
{
    public interface IIndexerFactory : IProviderFactory<IIndexer, IndexerDefinition>
    {
        List<IIndexer> RssEnabled(bool filterBlockedIndexers = true);
        List<IIndexer> SearchEnabled(bool filterBlockedIndexers = true);
    }

    public class IndexerFactory : ProviderFactory<IIndexer, IndexerDefinition>, IIndexerFactory
    {
        private readonly ISubstituteIndexerUrl _substituteIndexerUrl;
        private readonly IIndexerStatusService _indexerStatusService;
        private readonly Logger _logger;

        public IndexerFactory(ISubstituteIndexerUrl replaceIndexerUrl,
                              IIndexerStatusService indexerStatusService,
                              IIndexerRepository providerRepository,
                              IEnumerable<IIndexer> providers,
                              IContainer container,
                              IEventAggregator eventAggregator,
                              Logger logger)
            : base(providerRepository, providers, container, eventAggregator, logger)
        {
            _substituteIndexerUrl = replaceIndexerUrl;
            _indexerStatusService = indexerStatusService;
            _logger = logger;
        }

        protected override List<IndexerDefinition> Active()
        {
            return base.Active().Where(c => c.Enable).ToList();
        }

        public override void SetProviderCharacteristics(IIndexer provider, IndexerDefinition definition)
        {
            base.SetProviderCharacteristics(provider, definition);

            definition.Protocol = provider.Protocol;
            definition.SupportsRss = provider.SupportsRss;
            definition.SupportsSearch = provider.SupportsSearch;
        }

        public List<IIndexer> RssEnabled(bool filterBlockedIndexers = true)
        {
            var enabledIndexers = GetAvailableProviders().Where(n => ((IndexerDefinition)n.Definition).EnableRss);

            if (filterBlockedIndexers)
            {
                return FilterBlockedIndexers(SubstituteIndexerUrls(enabledIndexers)).ToList();
            }

            return enabledIndexers.ToList();
        }

        public List<IIndexer> SearchEnabled(bool filterBlockedIndexers = true)
        {
            var enabledIndexers = GetAvailableProviders().Where(n => ((IndexerDefinition)n.Definition).EnableSearch);

            if (filterBlockedIndexers)
            {
                return FilterBlockedIndexers(SubstituteIndexerUrls(enabledIndexers)).ToList();
            }

            return enabledIndexers.ToList();
        }

        private IEnumerable<IIndexer> SubstituteIndexerUrls(IEnumerable<IIndexer> indexers)
        {
            foreach (var indexer in indexers)
            {
                var settings = (IIndexerSettings)indexer.Definition.Settings;
                if (settings.BaseUrl.IsNotNullOrWhiteSpace())
                {
                    var newBaseUrl = _substituteIndexerUrl.SubstituteUrl(settings.BaseUrl);
                    if (newBaseUrl != settings.BaseUrl)
                    {
                        _logger.Debug("Substituted indexer {0} url {1} with {2} since services blacklisted it.", indexer.Definition.Name, settings.BaseUrl, newBaseUrl);
                        settings.BaseUrl = newBaseUrl;
                    }
                }

                yield return indexer;
            }
        }

        private IEnumerable<IIndexer> FilterBlockedIndexers(IEnumerable<IIndexer> indexers)
        {
            var blockedIndexers = _indexerStatusService.GetBlockedIndexers().ToDictionary(v => v.IndexerId, v => v);

            foreach (var indexer in indexers)
            {
                IndexerStatus blockedIndexerStatus;
                if (blockedIndexers.TryGetValue(indexer.Definition.Id, out blockedIndexerStatus))
                {
                    _logger.Debug("Temporarily ignoring indexer {0} till {1} due to recent failures.", indexer.Definition.Name, blockedIndexerStatus.DisabledTill.Value.ToLocalTime());
                    continue;
                }

                yield return indexer;
            }
        }
    }
}
