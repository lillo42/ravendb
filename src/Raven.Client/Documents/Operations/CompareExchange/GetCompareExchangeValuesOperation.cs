﻿using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using Raven.Client.Documents.Conventions;
using Raven.Client.Http;
using Raven.Client.Util;
using Sparrow.Json;

namespace Raven.Client.Documents.Operations.CompareExchange
{
    public class GetCompareExchangeValuesOperation<T> : IOperation<Dictionary<string, CompareExchangeValue<T>>>
    {
        private readonly string[] _keys;

        private readonly string _startWith;
        private readonly int? _start;
        private readonly int? _pageSize;
        
        public GetCompareExchangeValuesOperation(string[] keys)
        {
            if (keys == null || keys.Length == 0)
                throw new ArgumentNullException(nameof(keys));

            _keys = keys;
        }
        
        public GetCompareExchangeValuesOperation(string startWith, int? start = null, int? pageSize = null)
        {
            _startWith = startWith;
            _start = start;
            _pageSize = pageSize;
        }

        public RavenCommand<Dictionary<string, CompareExchangeValue<T>>> GetCommand(IDocumentStore store, DocumentConventions conventions, JsonOperationContext context, HttpCache cache)
        {
            return new GetCompareExchangeValuesCommand(this, conventions);
        }

        private class GetCompareExchangeValuesCommand : RavenCommand<Dictionary<string, CompareExchangeValue<T>>>
        {
            private readonly GetCompareExchangeValuesOperation<T> _operation;
            private readonly DocumentConventions _conventions;

            public GetCompareExchangeValuesCommand(GetCompareExchangeValuesOperation<T> operation, DocumentConventions conventions)
            {
                _operation = operation;
                _conventions = conventions;
            }

            public override bool IsReadRequest => true;

            public override HttpRequestMessage CreateRequest(JsonOperationContext ctx, ServerNode node, out string url)
            {
                var pathBuilder = new StringBuilder(node.Url);
                pathBuilder.Append("/databases/")
                    .Append(node.Database)
                    .Append("/cmpxchg?");

                if (_operation._keys != null)
                {
                    foreach (var key in _operation._keys)
                    {
                        pathBuilder.Append("&key=").Append(Uri.EscapeDataString(key));
                    }
                }
                else
                {
                    if (string.IsNullOrEmpty(_operation._startWith) == false)
                        pathBuilder.Append("&startsWith=").Append(Uri.EscapeDataString(_operation._startWith));
                    if (_operation._start.HasValue)
                        pathBuilder.Append("&start=").Append(_operation._start);
                    if (_operation._pageSize.HasValue)
                        pathBuilder.Append("&pageSize=").Append(_operation._pageSize);
                }

                url = pathBuilder.ToString();
                var request = new HttpRequestMessage
                {
                    Method = HttpMethods.Get,
                };
                return request;
            }

            public override void SetResponse(JsonOperationContext context, BlittableJsonReaderObject response, bool fromCache)
            {   
                Result = CompareExchangeValueResultParser<T>.GetValues(context, response, _conventions);
            }
        }
    }
}