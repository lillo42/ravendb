﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Raven.Client;
using Raven.Client.Documents.Replication;
using Raven.Client.Documents.Replication.Messages;
using Raven.Client.Http;
using Raven.Client.Server.Commands;
using Raven.Server.Documents;
using Raven.Server.Documents.Replication;
using Sparrow.Json;

namespace Raven.Server.Utils
{
    internal static class ReplicationUtils
    {
        public static NodeTopologyInfo GetLocalTopology(
            DocumentDatabase database,
            ReplicationDocument replicationDocument)
        {
            var topologyInfo = new NodeTopologyInfo { DatabaseId = database.DbId.ToString() };
            topologyInfo.InitializeOSInformation();

            var replicationLoader = database.ReplicationLoader;

            GetLocalIncomingTopology(replicationLoader, topologyInfo);

            foreach (var destination in replicationDocument.Destinations)
            {
                OutgoingReplicationHandler outgoingHandler;
                ReplicationLoader.ConnectionShutdownInfo connectionFailureInfo;

                if (TryGetActiveDestination(destination, replicationLoader.OutgoingHandlers, out outgoingHandler))
                {

                    topologyInfo.Outgoing.Add(
                        new ActiveNodeStatus
                        {
                            DbId = outgoingHandler.DestinationDbId,
                            IsCurrentlyConnected = true,
                            Database = destination.Database,
                            Url = destination.Url,
                            SpecifiedCollections = destination.SpecifiedCollections ?? new Dictionary<string, string>(),
                            LastDocumentEtag = outgoingHandler._lastSentDocumentEtag,
                            LastIndexTransformerEtag = outgoingHandler._lastSentIndexOrTransformerEtag,
                            LastHeartbeatTicks = outgoingHandler.LastHeartbeatTicks,
                            NodeStatus = ActiveNodeStatus.Status.Online
                        });

                }
                else if (replicationLoader.OutgoingFailureInfo.TryGetValue(destination, out connectionFailureInfo))
                {
                    topologyInfo.Offline.Add(
                        new InactiveNodeStatus
                        {
                            Database = destination.Database,
                            Url = destination.Url,
                            Exception = connectionFailureInfo.LastException?.ToString(),
                            Message = connectionFailureInfo.LastException?.Message
                        });
                }
                else
                {
                    topologyInfo.Offline.Add(
                        new InactiveNodeStatus
                        {
                            Database = destination.Database,
                            Url = destination.Url,
                            Exception = destination.Disabled ? "Replication destination has been disabled" : null
                        });
                }
            }

            return topologyInfo;
        }

        public static void GetLocalIncomingTopology(ReplicationLoader replicationLoader, NodeTopologyInfo topologyInfo)
        {
            foreach (var incomingHandler in replicationLoader.IncomingHandlers)
            {
                topologyInfo.Incoming.Add(
                    new ActiveNodeStatus
                    {
                        DbId = incomingHandler.ConnectionInfo.SourceDatabaseId,
                        Database = incomingHandler.ConnectionInfo.SourceDatabaseName,
                        Url = new UriBuilder(incomingHandler.ConnectionInfo.SourceUrl)
                        {
                            Host = incomingHandler.ConnectionInfo.RemoteIp
                        }.Uri.ToString(),
                        IsCurrentlyConnected = true,
                        NodeStatus = ActiveNodeStatus.Status.Online,
                        LastDocumentEtag = incomingHandler.LastDocumentEtag,
                        LastIndexTransformerEtag = incomingHandler.LastIndexOrTransformerEtag,
                        LastHeartbeatTicks = incomingHandler.LastHeartbeatTicks
                    });
            }
        }

        public static bool TryGetActiveDestination(ReplicationDestination destination,
            IEnumerable<OutgoingReplicationHandler> outgoingReplicationHandlers,
            out OutgoingReplicationHandler handler)
        {
            handler = null;
            foreach (var outgoing in outgoingReplicationHandlers)
            {
                if (outgoing.Destination.Url.Equals(destination.Url, StringComparison.OrdinalIgnoreCase) &&
                    outgoing.Destination.Database.Equals(destination.Database, StringComparison.OrdinalIgnoreCase))
                {
                    handler = outgoing;
                    return true;
                }
            }

            return false;
        }

        public static TcpConnectionInfo GetTcpInfo(string url, string databaseName, string apiKey)
        {
            JsonOperationContext context;
            using (var requestExecuter = RequestExecutor.CreateForSingleNode(url, databaseName, apiKey))
            using (requestExecuter.ContextPool.AllocateOperationContext(out context))
            {
                var getTcpInfoCommand = new GetTcpInfoCommand();
                requestExecuter.Execute(getTcpInfoCommand, context);

                return getTcpInfoCommand.Result;
            }
        }

        public static async Task<TcpConnectionInfo> GetTcpInfoAsync(string url, string databaseName, string apiKey)
        {
            JsonOperationContext context;
            using (var requestExecuter = RequestExecutor.CreateForSingleNode(url, databaseName, apiKey))
            using (requestExecuter.ContextPool.AllocateOperationContext(out context))
            {
                var getTcpInfoCommand = new GetTcpInfoCommand();
                await requestExecuter.ExecuteAsync(getTcpInfoCommand, context);

                return getTcpInfoCommand.Result;
            }
        }

        public static void EnsureCollectionTag(BlittableJsonReaderObject obj, string collection)
        {
            string actualCollection;
            BlittableJsonReaderObject metadata;
            if (obj.TryGet(Constants.Documents.Metadata.Key, out metadata) == false ||
                metadata.TryGet(Constants.Documents.Metadata.Collection, out actualCollection) == false ||
                actualCollection != collection)
            {
                if (collection == CollectionName.EmptyCollection)
                    return;

                ThrowInvalidCollectionAfterResolve(collection, null);
            }
        }

        private static void ThrowInvalidCollectionAfterResolve(string collection, string actual)
        {
            throw new InvalidOperationException(
                "Resolving script did not setup the appropriate '@collection'. Expeted " + collection + " but got " +
                actual);
        }
    }
}